using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text;
using System.Threading;

namespace DNSM1;

public class Program
{
    // thread-safe counters
    private static long totalQueries = 0;
    private static long blockedQueries = 0;
    private static long forwardedQueries = 0;
    private static long failedQueries = 0;

    public static async Task Main(string[] args)
    {
        var listenIPv4 = "0.0.0.0";
        var port = 5300;
        var blocklistPath = "blocklist.txt";

        var blocked = LoadBlocklist(blocklistPath);
        Console.WriteLine($"[+] Loaded {blocked.Count} blocked domains");
        WatchBlocklist(blocklistPath, blocked);

        using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        sock.Bind(new IPEndPoint(IPAddress.Parse(listenIPv4), port));
        Console.WriteLine($"[+] Listening on UDP://{listenIPv4}:{port}");

        // background reporter
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(60));
                PrintStats();
            }
        });

        var buf = new byte[5120];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        // main loop
        while (true)
        {
            try
            {
                int len = sock.ReceiveFrom(buf, ref remote);
                if (len < 12) continue;
                Interlocked.Increment(ref totalQueries);

                // Parse QNAME
                int idx = 12;
                var labels = new List<string>();
                while (true)
                {
                    if (idx >= len) break;
                    byte labLen = buf[idx++];
                    if (labLen == 0) break;
                    if (labLen > 63 || idx + labLen > len)
                    {
                        labels.Clear();
                        break;
                    }
                    labels.Add(Encoding.ASCII.GetString(buf, idx, labLen));
                    idx += labLen;
                }

                if (labels.Count == 0 || idx + 4 > len) continue;
                ushort qtype = ReadU16(buf, idx); idx += 2;
                _ = ReadU16(buf, idx); idx += 2;

                string qname = string.Join('.', labels).ToLowerInvariant();
                string typeStr = qtype switch { 1 => "A", 28 => "AAAA", _ => qtype.ToString() };

                Console.WriteLine($"{DateTime.Now:HH:mm:ss} {remote} -> {qname} ({typeStr})");

                byte[] response;

                // --- Blocklist check ---
                if (blocked.Contains(qname))
                {
                    Interlocked.Increment(ref blockedQueries);
                    response = BuildBlockedResponse(buf, len);
                    Console.WriteLine($"[x] BLOCKED {qname}");
                }
                else
                {
                    try
                    {
                        response = await ForwardToUpstreamAsync(buf, len);
                        Interlocked.Increment(ref forwardedQueries);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failedQueries);
                        Console.WriteLine($"[!] Upstream error: {ex.Message}");
                        response = BuildServfail(buf, len);
                    }
                }

                sock.SendTo(response, remote);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] General error: {ex.Message}");
            }
        }
    }

    // ------------------- Helpers -------------------

    static void PrintStats()
    {
        Console.WriteLine($"\n=== DNS Stats @ {DateTime.Now:HH:mm:ss} ===");
        Console.WriteLine($"Total queries  : {Interlocked.Read(ref totalQueries)}");
        Console.WriteLine($"Blocked queries: {Interlocked.Read(ref blockedQueries)}");
        Console.WriteLine($"Forwarded      : {Interlocked.Read(ref forwardedQueries)}");
        Console.WriteLine($"Upstream fails : {Interlocked.Read(ref failedQueries)}");
        Console.WriteLine("=====================================\n");
    }

    static HashSet<string> LoadBlocklist(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllLines(path, new[] { "ads.google.com", "doubleclick.net", "facebook.com" });
            Console.WriteLine("[i] Created default blocklist.txt");
        }
        return File.ReadAllLines(path)
            .Select(l => l.Trim().ToLowerInvariant())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToHashSet();
    }

    static void WatchBlocklist(string path, HashSet<string> blocked)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var file = Path.GetFileName(path);

        var watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };

        watcher.Changed += (_, __) => Reload();
        watcher.Created += (_, __) => Reload();
        watcher.Renamed += (_, __) => Reload();

        watcher.EnableRaisingEvents = true;
        Console.WriteLine($"[i] Watching {path} for changes...");

        void Reload()
        {
            try
            {
                Thread.Sleep(500);
                var newSet = File.ReadAllLines(path)
                    .Select(l => l.Trim().ToLowerInvariant())
                    .Where(l => l.Length > 0 && !l.StartsWith('#'))
                    .ToHashSet();

                lock (blocked)
                {
                    blocked.Clear();
                    foreach (var d in newSet) blocked.Add(d);
                }
                Console.WriteLine($"[+] Reloaded {blocked.Count} blocked domains");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Reload failed: {ex.Message}");
            }
        }
    }

    static async Task<byte[]> ForwardToUpstreamAsync(byte[] request, int length)
    {
        using var udp = new UdpClient();
        await udp.SendAsync(request, length, "1.1.1.1", 53);
        var result = await udp.ReceiveAsync();
        return result.Buffer;
    }

    static byte[] BuildServfail(byte[] request, int len)
    {
        var resp = new byte[len];
        Array.Copy(request, resp, len);
        resp[2] |= 0x80; // QR=1
        resp[3] |= 0x02; // RCODE=2
        return resp;
    }

    static byte[] BuildBlockedResponse(byte[] request, int len)
    {
        ushort id = ReadU16(request, 0);
        int i = 12;
        while (i < len && request[i] != 0) i += request[i] + 1;
        i += 5; // 0 + QTYPE(2) + QCLASS(2)
        int qEnd = i;

        var resp = new List<byte>();
        resp.AddRange(request[..2]);                // ID
        resp.AddRange(new byte[] { 0x81, 0x80 });  // standard response
        resp.AddRange(new byte[] { 0x00, 0x01 });  // QDCOUNT
        resp.AddRange(new byte[] { 0x00, 0x01 });  // ANCOUNT
        resp.AddRange(new byte[] { 0x00, 0x00 });  // NSCOUNT
        resp.AddRange(new byte[] { 0x00, 0x00 });  // ARCOUNT
        resp.AddRange(request[12..qEnd]);          // Question
        resp.AddRange(new byte[] { 0xC0, 0x0C });  // pointer
        resp.AddRange(new byte[] { 0x00, 0x01 });  // Type A
        resp.AddRange(new byte[] { 0x00, 0x01 });  // Class IN
        resp.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x3C }); // TTL 60
        resp.AddRange(new byte[] { 0x00, 0x04 });  // length
        resp.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // 0.0.0.0
        return resp.ToArray();
    }

    static ushort ReadU16(byte[] b, int o) =>
        BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o, 2));
}
