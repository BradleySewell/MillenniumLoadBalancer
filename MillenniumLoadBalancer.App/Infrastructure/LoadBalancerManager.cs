using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure.Factories;

namespace MillenniumLoadBalancer.App.Infrastructure;

/// <summary>
/// Manager for managing and running load balancers.
/// </summary>
public class LoadBalancerManager : ILoadBalancerManager
{
    private readonly IConfiguration _configuration;
    private readonly ILoadBalancerFactory _loadBalancerFactory;
    private readonly ILogger<LoadBalancerManager> _hostLogger;
    private List<ILoadBalancer> _loadBalancers = new();

    public LoadBalancerManager(
        IConfiguration configuration,
        ILoadBalancerFactory loadBalancerFactory,
        ILogger<LoadBalancerManager> hostLogger)
    {
        _configuration = configuration;
        _loadBalancerFactory = loadBalancerFactory;
        _hostLogger = hostLogger;
    }


    public void Initialize()
    {
        Console.WriteLine("+-------------------------------------------------------------+");
        Console.WriteLine("|                 MILLENNIUM LOAD BALANCER                    |");
        Console.WriteLine("|                             V1                              |");
        Console.WriteLine("+-------------------------------------------------------------+");

        var loadBalancerOptions = _configuration.GetSection("LoadBalancer").Get<LoadBalancerOptions>()
            ?? throw new InvalidOperationException("LoadBalancer configuration is missing");

        _loadBalancers = loadBalancerOptions.Listeners
            .Select(listenerConfig => _loadBalancerFactory.Create(listenerConfig))
            .ToList();
    }

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        _hostLogger.LogInformation("Starting all load balancers...");

        foreach (var loadBalancer in _loadBalancers)
        {
            try
            {
                _hostLogger.LogInformation($"Starting '{loadBalancer.Name}' load balancer...");
                await loadBalancer.StartAsync(cancellationToken);
                _hostLogger.LogInformation($"Started '{loadBalancer.Name}' load balancer successfully.");

            }
            catch (Exception ex)
            {
                _hostLogger.LogError($"Failed to start a load balancer: {ex.Message}");
                throw;
            }
        }

        _hostLogger.LogInformation("All Load balancers started. Press Ctrl+C to stop.");
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        _hostLogger.LogInformation("Shutting down all load balancers...");

        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        shutdownCts.CancelAfter(TimeSpan.FromSeconds(ServiceConfiguration.ShutdownTimeout));

        foreach (var loadBalancer in _loadBalancers)
        {
            try
            {
                _hostLogger.LogInformation($"Stopping '{loadBalancer.Name}' load balancer...");
                await loadBalancer.StopAsync(shutdownCts.Token);
                _hostLogger.LogInformation($"Stopped '{loadBalancer.Name}' load balancer successfully.");

            }
            catch (Exception ex)
            {
                _hostLogger.LogWarning($"Error stopping a load balancer: {ex.Message}");
            }
        }

        _hostLogger.LogInformation("All Load balancers stopped.");
    }
}

