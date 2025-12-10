using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;

namespace MillenniumLoadBalancer.App.Infrastructure.Factories;

internal class LoadBalancerFactory : ILoadBalancerFactory
{
    private readonly ILoadBalancingStrategyFactory _strategyFactory;
    private readonly IConnectionForwarderFactory _forwarderFactory;
    private readonly IBackendServiceFactory _backendServiceFactory;
    private readonly IBackendHealthCheckService _healthCheckService;
    private readonly IConnectionTracker? _connectionTracker;
    private readonly IVisualConsoleService? _visualConsoleService;
    private readonly ILogger<LoadBalancer> _logger;

    public LoadBalancerFactory(
        ILoadBalancingStrategyFactory strategyFactory,
        IConnectionForwarderFactory forwarderFactory,
        IBackendServiceFactory backendServiceFactory,
        IBackendHealthCheckService healthCheckService,
        ILogger<LoadBalancer> logger,
        IConnectionTracker? connectionTracker = null,
        IVisualConsoleService? visualConsoleService = null)
    {
        _strategyFactory = strategyFactory;
        _forwarderFactory = forwarderFactory;
        _backendServiceFactory = backendServiceFactory;
        _healthCheckService = healthCheckService;
        _connectionTracker = connectionTracker;
        _visualConsoleService = visualConsoleService;
        _logger = logger;
    }

    public ILoadBalancer Create(ListenerConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        
        if (string.IsNullOrWhiteSpace(configuration.Name))
            throw new ArgumentException("Listener name cannot be null or empty", nameof(configuration));
        
        if (string.IsNullOrWhiteSpace(configuration.ListenAddress))
            throw new ArgumentException("Listen address cannot be null or empty", nameof(configuration));
        
        if (!System.Net.IPAddress.TryParse(configuration.ListenAddress, out _))
            throw new ArgumentException($"Invalid listen address: {configuration.ListenAddress}. Must be a valid IP address", nameof(configuration));
        
        if (configuration.ListenPort <= 0 || configuration.ListenPort > 65535)
            throw new ArgumentException($"Invalid listen port: {configuration.ListenPort}. Port must be between 1 and 65535", nameof(configuration));
        
        if (configuration.Backends == null || configuration.Backends.Count == 0)
            throw new ArgumentException("At least one backend must be configured", nameof(configuration));
        
        if (configuration.ConnectionTimeoutSeconds <= 0)
            throw new ArgumentException($"Connection timeout must be greater than 0, got {configuration.ConnectionTimeoutSeconds}", nameof(configuration));
        
        if (configuration.SendTimeoutSeconds <= 0)
            throw new ArgumentException($"Send timeout must be greater than 0, got {configuration.SendTimeoutSeconds}", nameof(configuration));
        
        if (configuration.ReceiveTimeoutSeconds <= 0)
            throw new ArgumentException($"Receive timeout must be greater than 0, got {configuration.ReceiveTimeoutSeconds}", nameof(configuration));
        
        if (configuration.RecoveryCheckIntervalSeconds <= 0)
            throw new ArgumentException($"Recovery check interval must be greater than 0, got {configuration.RecoveryCheckIntervalSeconds}", nameof(configuration));
        
        if (configuration.RecoveryDelaySeconds <= 0)
            throw new ArgumentException($"Recovery delay must be greater than 0, got {configuration.RecoveryDelaySeconds}", nameof(configuration));

        var backends = configuration.Backends
            .Select(b =>
            {
                if (string.IsNullOrWhiteSpace(b.Address))
                    throw new ArgumentException("Backend address cannot be null or empty", nameof(configuration));
                if (!System.Net.IPAddress.TryParse(b.Address, out _))
                    throw new ArgumentException($"Invalid backend address: {b.Address}. Must be a valid IP address", nameof(configuration));
                if (b.Port <= 0 || b.Port > 65535)
                    throw new ArgumentException($"Invalid backend port: {b.Port}. Port must be between 1 and 65535", nameof(configuration));
                return _backendServiceFactory.Create(b.Address, b.Port, b.EnableTls, b.ValidateCertificate);
            })
            .ToList();

        var forwarder = _forwarderFactory.Create(
            configuration.Protocol,
            configuration.ConnectionTimeoutSeconds,
            configuration.SendTimeoutSeconds,
            configuration.ReceiveTimeoutSeconds);

        var strategy = _strategyFactory.Create(configuration.Strategy);

        return new LoadBalancer(
            configuration.Name,
            configuration.ListenAddress,
            configuration.ListenPort,
            backends,
            strategy,
            forwarder,
            _healthCheckService,
            configuration.RecoveryCheckIntervalSeconds,
            configuration.RecoveryDelaySeconds,
            _logger,
            _connectionTracker,
            _visualConsoleService);
    }
}

