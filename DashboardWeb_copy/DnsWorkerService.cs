using DnsBlocker.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

// Place this file next to Program.cs or in the Services folder
public class DnsWorkerService : BackgroundService
{
    private readonly DnsServer _dnsServer;

    public DnsWorkerService(DnsServer dnsServer)
    {
        _dnsServer = dnsServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _dnsServer.StartAsync();
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // This method will be called when the ASP.NET Core Host shuts down.
        // It calls the Stop() method we added to DnsServer.cs.
        _dnsServer.Stop();
        return base.StopAsync(cancellationToken);
    }
}