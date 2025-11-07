using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DnsBlocker.Core
{
    public class DnsServer
    {
        public const int DnsPort = 53;
        private readonly UdpClient _udpListener;
        private readonly IPEndPoint _upstreamDns = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);

        private readonly HashSet<string> _blocklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "example.com",
            "tracking.net",
            "ads.google.com"
        };

        private const string LogFileName = "network_activity.log";
        private static string GetLogPath() => Path.Combine(AppContext.BaseDirectory, "data", LogFileName);

        private static readonly object LogLock = new object();
        private readonly CancellationTokenSource _cts = new();

        private long _totalQueries = 0;
        private long _blockedQueries = 0;
        private long _forwardedQueries = 0;

        public DnsServer()
        {
            ResetServerState();
            _udpListener = new UdpClient(new IPEndPoint(IPAddress.Any, DnsPort));
        }

        public (long Total, long Blocked, long Forwarded) GetLiveStats()
        {
            return (_totalQueries, _blockedQueries, _forwardedQueries);
        }
        public void ResetServerState()
        {
            _totalQueries = 0;
            _blockedQueries = 0;
            _forwardedQueries = 0;

            string logPath = GetLogPath();
            lock (LogLock)
            {
                if (File.Exists(logPath))
                {
                    try
                    {
                        File.Delete(logPath);
                        Console.WriteLine("[Startup] network_activity.log and in-memory counters cleared for a fresh start.");
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"[Startup Error] Failed to delete log file: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Startup Error] An unexpected error occurred during log deletion: {ex.Message}");
                    }
                }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _udpListener.Close();
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"DNS Blocker running on port {DnsPort}. Upstream: {_upstreamDns.Address}");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpListener.ReceiveAsync(_cts.Token);

                    _ = Task.Run(async () =>
                    {
                        var response = await HandleQuery(result.RemoteEndPoint, result.Buffer, _cts.Token);

                        _udpListener.Send(
                            response,
                            response.Length,
                            result.RemoteEndPoint
                        );

                    }, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Listener Error] {ex.Message}");
                }
            }
        }

        private async Task<byte[]> HandleQuery(IPEndPoint sender, byte[] queryBytes, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _totalQueries);

            string domain = GetDomainNameFromQuery(queryBytes);
            string clientIp = sender.Address.ToString();

            if (_blocklist.Contains(domain))
            {
                Interlocked.Increment(ref _blockedQueries);

                LogActivity(clientIp, domain, "Blocked", "0.0.0.0");

                return CreateBlockedResponse(queryBytes);
            }
            try
            {
                using var client = new UdpClient();

                await client.SendAsync(queryBytes, _upstreamDns, cancellationToken);

                client.Client.ReceiveTimeout = 5000;

                var responseResult = await client.ReceiveAsync(cancellationToken);
                var response = responseResult.Buffer;

                string responseIp = GetResponseIp(response);

                Interlocked.Increment(ref _forwardedQueries);

                LogActivity(clientIp, domain, "Allowed", responseIp);

                return response.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to resolve {domain}: {ex.Message}");
                LogActivity(clientIp, domain, "Error", "255.255.255.255");
                return CreateErrorResponse(queryBytes);
            }
        }

        private static void LogActivity(string clientIp, string domain, string status, string responseIp)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string logLine = $"{timestamp}, {clientIp}, {domain}, {status}, {responseIp}";
            string logPath = GetLogPath();

            lock (LogLock)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

                    File.AppendAllText(logPath, logLine + Environment.NewLine);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[Log Error] Failed to write to log file: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Log Error] An unexpected error occurred during logging: {ex.Message}");
                }
            }
        }
        private static string GetDomainNameFromQuery(byte[] query)
        {
            int position = 12;
            var domainName = new StringBuilder();

            try
            {
                while (position < query.Length && query[position] != 0)
                {
                    byte length = query[position];
                    position++;

                    if (length == 0 || position + length > query.Length)
                    {
                        break;
                    }
                    domainName.Append(Encoding.ASCII.GetString(query, position, length));
                    domainName.Append('.');

                    position += length;
                }
                if (domainName.Length > 0 && domainName[domainName.Length - 1] == '.')
                {
                    domainName.Length--;
                }

                return domainName.ToString();
            }
            catch
            {
                return "parsing-error.local";
            }
        }

        private static byte[] CreateBlockedResponse(byte[] query) { return query; }
        private static byte[] CreateErrorResponse(byte[] query) { return query; }
        private static string GetResponseIp(byte[] response)
        {
            if (response.Length < 12) return "0.0.0.0";
            int answerCount = (response[6] << 8) | response[7];

            if (answerCount == 0) return "0.0.0.0";

            int position = 12;
            while (position < response.Length && response[position] != 0)
            {
                position += response[position] + 1;
            }

            if (position >= response.Length) return "0.0.0.0";
            position++;

            if (position + 4 >= response.Length) return "0.0.0.0";
            position += 4;

            for (int i = 0; i < answerCount; i++)
            {
                
                if ((position + 1 < response.Length) && (response[position] & 0xC0) == 0xC0)
                {
                   
                    position += 2;
                }
                else
                {
                    while (position < response.Length && response[position] != 0)
                    {
                        position += response[position] + 1;
                    }
                    if (position >= response.Length) return "0.0.0.0";
                    position++; 
                }
                if (position + 10 >= response.Length) return "0.0.0.0";
                int type = (response[position] << 8) | response[position + 1];
                position += 2;

                int dnsClass = (response[position] << 8) | response[position + 1];
                position += 2;
                position += 4;

                int rdLength = (response[position] << 8) | response[position + 1];
                position += 2;

                if (type == 1 && dnsClass == 1 && rdLength == 4)
                {
                    if (position + 4 <= response.Length)
                    {
                        return $"{response[position]}.{response[position + 1]}.{response[position + 2]}.{response[position + 3]}";
                    }
                }
                position += rdLength;
            }
            return "0.0.0.0";
        }
    }
}