using DashboardWeb.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DashboardWeb.Services
{
    public interface ILogDataAccess
    {
        List<LogEntry> GetRecentLogs(int count = 50);
        DashboardStats GetCurrentStats();
        Dictionary<DateTime, int> GetQueriesByHour(int hours = 24);
    }

    public class LogDataAccess : ILogDataAccess
    {
        private const string LogFileName = "network_activity.log";
        private static string GetLogPath() => Path.Combine(AppContext.BaseDirectory, "data", LogFileName);

        private static readonly object FileLock = new object();

        public LogDataAccess() { }

        public List<LogEntry> GetRecentLogs(int count = 50)
        {
            var allLogs = LoadAllLogs();
            return allLogs.OrderByDescending(l => l.Timestamp).Take(count).ToList();
        }

        private List<LogEntry> LoadAllLogs()
        {
            string logPath = GetLogPath();
            var logs = new List<LogEntry>();

            if (!File.Exists(logPath))
            {
                return logs;
            }
            lock (FileLock)
            {
                try
                {
                    var lines = File.ReadAllLines(logPath);
                    foreach (var line in lines)
                    {
                        var entry = LogParser.ParseLogLine(line);
                        if (entry != null)
                        {
                            logs.Add(entry);
                        }
                    }
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[Dashboard Error] Failed to read log file: {ex.Message}");
                }
            }

            return logs;
        }

        public DashboardStats GetCurrentStats()
        {
            var allLogs = LoadAllLogs();
            var total = allLogs.Count;
            var blocked = allLogs.Count(l => l.Status == "Blocked");
            double percentage = total == 0 ? 0 : (double)blocked / total * 100;

            var topDomains = allLogs
                .Where(l => l.Status == "Blocked")
                .GroupBy(l => l.Domain)
                .Select(g => new { Domain = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToDictionary(x => x.Domain, x => x.Count);

            return new DashboardStats
            {
                TotalQueryCount = total,
                TotalBlockedCount = blocked,
                TotalAllowedCount = total - blocked,
                BlockedPercentage = Math.Round(percentage, 2),
                TopBlockedDomains = topDomains
            };
        }

        public Dictionary<DateTime, int> GetQueriesByHour(int hours = 24)
        {
            var cutoffTime = DateTime.Now.AddHours(-hours);
            var allLogs = LoadAllLogs();

            var hourlyData = allLogs
                .Where(l => l.Timestamp >= cutoffTime)
                .GroupBy(l => new DateTime(l.Timestamp.Year, l.Timestamp.Month, l.Timestamp.Day, l.Timestamp.Hour, 0, 0))
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );

            var sortedData = new Dictionary<DateTime, int>();
            for (int i = hours; i >= 0; i--)
            {
                var hour = DateTime.Now.AddHours(-i).ToLocalTime();
                var key = new DateTime(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0);

                if (hourlyData.TryGetValue(key, out int count))
                {
                    sortedData.Add(key, count);
                }
                else
                {
                    sortedData.Add(key, 0);
                }
            }

            return sortedData;
        }
    }
    public static class LogParser
    {
        public static LogEntry? ParseLogLine(string line)
        {
            var parts = line.Replace('\0', ' ').Split(',');
            if (parts.Length < 5) return null;

            try
            {
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.ParseExact(parts[0].Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    ClientIP = parts[1].Trim(),
                    Domain = parts[2].Trim(),
                    Status = parts[3].Trim(),
                    ResponseIP = parts[4].Trim()
                };
                return logEntry;
            }
            catch 
            {
                return null;
            }
        }
    }
}