using System.Net.Http;
using System.Text.RegularExpressions;

class BlocklistUpdater
{
    static async Task Main(string[] args)
    {
        string blocklistPath = "/home/admin/Network Programming/Rapberry Pi Project/building the server/pihole-lite/src/PiholeLite/blocklist.txt";
        Console.WriteLine("[i] Updating blocklist from public sources...");
        string[] urls =
        {
            "https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts",
            "https://adaway.org/hosts.txt",
            "https://v.firebog.net/hosts/Easyprivacy.txt"
        };

        var domains = new HashSet<string>();
        using var client = new HttpClient();

        foreach (var url in urls)
        {
            try
            {
                Console.WriteLine($"[→] Fetching {url}");
                var text = await client.GetStringAsync(url);

                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                    var match = Regex.Match(trimmed, @"^(?:0\.0\.0\.0|127\.0\.0\.1)\s+([a-zA-Z0-9.-]+)$");
                    if (match.Success)
                        domains.Add(match.Groups[1].Value.ToLowerInvariant());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Failed to fetch {url}: {ex.Message}");
            }
        }

        try
        {
            await File.WriteAllLinesAsync(blocklistPath, domains.OrderBy(d => d));
            Console.WriteLine($"[+] Wrote {domains.Count} unique domains to {blocklistPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Could not write file: {ex.Message}");
        }
    }
}
