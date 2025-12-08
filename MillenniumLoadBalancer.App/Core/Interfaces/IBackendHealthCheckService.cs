namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface IBackendHealthCheckService
{
    Task<bool> CheckHealthAsync(IBackendService backend, int timeoutSeconds, CancellationToken cancellationToken = default);
}
