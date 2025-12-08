namespace MillenniumLoadBalancer.App.Core.Interfaces;

/// <summary>
/// Interface for managing and running load balancers.
/// </summary>
public interface ILoadBalancerManager
{
    /// <summary>
    /// Initializes the load balancer manager with configuration.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Starts all configured load balancers.
    /// </summary>
    Task StartAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all configured load balancers.
    /// </summary>
    Task StopAllAsync(CancellationToken cancellationToken = default);
}

