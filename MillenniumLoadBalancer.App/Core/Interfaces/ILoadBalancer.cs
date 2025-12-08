namespace MillenniumLoadBalancer.App.Core.Interfaces;

public interface ILoadBalancer
{
    string Name { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

