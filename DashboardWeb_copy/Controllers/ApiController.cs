using DashboardWeb.Models;
using DashboardWeb.Services;
using DnsBlocker.Core;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System;
using System.Linq;

namespace DashboardWeb.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly ILogDataAccess _logDataAccess;
        private readonly DnsServer _dnsServer;

        public ApiController(ILogDataAccess logDataAccess, DnsServer dnsServer)
        {
            _logDataAccess = logDataAccess;
            _dnsServer = dnsServer;
        }

        [HttpGet("logs")]
        public IEnumerable<LogEntry> GetRecentLogs()
        {
            return _logDataAccess.GetRecentLogs(count: 10);
        }

        [HttpGet("stats/file")]
        public IActionResult GetDashboardStatsFromFiles()
        {
            var stats = _logDataAccess.GetCurrentStats();
            return Ok(stats);
        }

        [HttpGet("stats/live")]
        public IActionResult GetRealTimeStats()
        {
            var liveStats = _dnsServer.GetLiveStats();

            double total = liveStats.Total == 0 ? 1 : liveStats.Total;
            double blockedPercentage = (liveStats.Blocked / total) * 100.0;

            return Ok(new
            {
                totalQueries = liveStats.Total,
                blockedQueries = liveStats.Blocked,
                forwardedQueries = liveStats.Forwarded,
                blockedPercentage = Math.Round(blockedPercentage, 2)
            });
        }
        [HttpGet("stats/hourly")]
        public IActionResult GetHourlyQueryData()
        {
            var hourlyData = _logDataAccess.GetQueriesByHour(hours: 24);
            return Ok(new
            {
                labels = hourlyData.Keys.Select(k => k.ToShortTimeString()).ToList(),
                data = hourlyData.Values.ToList()
            });
        }
    }
}