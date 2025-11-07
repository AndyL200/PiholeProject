using System;

namespace DashboardWeb.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string ClientIP { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty; 
        public string Status { get; set; } = string.Empty; // Allowed or Block
        public string ResponseIP { get; set; } = string.Empty; // IP returned 0.0.0.0 for blocked
    }
}
