DNS BLOCKER AND WEB DASHBOARD
DNS server application written in C# (.NET) that blocks domain name resolution based on a blocklist and provides a real-time web dashboard for monitoring network activity.
FEATURES
* DNS Blocking: Intercepts DNS queries on port 53 and blocks domains matching an internal list by responding with 0.0.0.0.
* Upstream Forwarding: For non-blocked domains, the query is forwarded to Google DNS (8.8.8.8) for resolution.
* Real-Time Dashboard: An ASP.NET Core web interface provides live statistics and recent log activity.
* Detailed Logging: Logs all DNS activity (Allowed/Blocked, Client IP, Domain, Resolved IP) to a file.
* Automatic Log Clearing: The activity log is reset every time the application starts to ensure fresh data.

GETTING STARTED
PREREQUISITES
* .NET SDK: You must have the .NET 8.0 SDK (or newer) installed.
* Permissions: Running a service on port 53 often requires administrative/root privileges.
RUNNING THE APPLICATION
Run the Server: dotnet run
The console will show a message confirming that the log file has been cleared and the server is running.
VIEWING THE DASHBOARD
Open your web browser and navigate to the application's URL, typically:
https://localhost:65315 or http://localhost:65316
TESTING THE DNS BLOCKER
Query your local server directly using nslookup (or a similar tool):
1. Test a Blocked Domain (e.g., tracking.net): nslookup tracking.net 127.0.0.1
               Expected result: Address: 0.0.0.0. Logged as Blocked.
2. Test an Allowed Domain (e.g., https://www.google.com/url?sa=E&source=gmail&q=google.com):nslookup google.com 127.0.0.1
Expected result: Actual IP addresses. Logged as Allowed with the resolved IP.

ARCHITECTURE OVERVIEW
COMPONENTFILES INVOLVEDROLEDNS EngineDnsServer.csListens on port 53, manages the blocklist, performs DNS packet parsing (query and response), and logs activity.Data AccessLogDataAccess.cs, LogEntry.csReads and parses the network_activity.log file to calculate statistics, top domains, and recent log entries.Web APIApiController.csExposes data endpoints (e.g., /api/logs, /api/stats/live) that the dashboard frontend calls to retrieve information.Dashboard UIindex.cshtmlThe frontend (HTML/JavaScript) that continuously polls the API to display the dashboard in real-time.
KEY IMPLEMENTATION DETAILS
DNS PACKET PARSING
* DnsServer.cs includes helper methods (GetDomainNameFromQuery and GetResponseIp) to decode the raw binary data of DNS query and response packets, ensuring accurate logging of domain names and resolved IP addresses.

BLOCKLIST MANAGEMENT
* The blocklist is currently hardcoded inside the _blocklist HashSet<string> in the DnsServer.cs file. To change the blocked domains, you must edit this file and recompile the application.
DATA STORAGE AND LOGGING
* All DNS activity is logged to a comma-separated file located at ./data/network_activity.log.
* The system uses thread synchronization (locking) on both file reading (LogDataAccess.cs) and writing (DnsServer.cs) to prevent data corruption when the web dashboard accesses the log file simultaneously with the DNS server.

