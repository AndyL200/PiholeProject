using System.Collections.Generic;

namespace DashboardWeb.Models
{
    public class DashboardStats
    {
        public int TotalQueryCount { get; set; }
        public int TotalBlockedCount { get; set; }
        public int TotalAllowedCount { get; set; }
        public double BlockedPercentage { get; set; }
        public Dictionary<string, int> TopBlockedDomains { get; set; } = new Dictionary<string, int>();
    }
}
