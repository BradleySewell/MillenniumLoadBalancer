using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Interfaces;
using System.Net.Sockets;

namespace MillenniumLoadBalancer.App.Core.Services;

internal class BackendHealthCheckService : IBackendHealthCheckService
{
    private readonly ILogger<BackendHealthCheckService> _logger;

    public BackendHealthCheckService(ILogger<BackendHealthCheckService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CheckHealthAsync(IBackendService backend, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            using var testClient = new TcpClient();
            await testClient.ConnectAsync(backend.Address, backend.Port, connectCts.Token);

            if (!testClient.Connected)
            {
                _logger.LogDebug($"Backend {backend.Address}:{backend.Port} connection not established");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            _logger.LogDebug(ex, $"Backend {backend.Address}:{backend.Port} health check failed");

            return false;
        }
    }
}
