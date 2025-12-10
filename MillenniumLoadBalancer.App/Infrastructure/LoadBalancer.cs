using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace MillenniumLoadBalancer.App.Infrastructure;

internal class LoadBalancer : ILoadBalancer, IDisposable
{
    private const int HealthCheckTimeoutSeconds = 5;

    private readonly string _name;
    private readonly string _listenAddress;
    private readonly int _listenPort;
    private readonly IEnumerable<IBackendService> _backends;
    private readonly ILoadBalancingStrategy _strategy;
    private readonly IConnectionForwarder _forwarder;
    private readonly IBackendHealthCheckService _healthCheckService;
    private readonly int _recoveryCheckIntervalSeconds;
    private readonly int _recoveryDelaySeconds;
    private readonly ILogger<LoadBalancer> _logger;
    private readonly IConnectionTracker? _connectionTracker;
    private readonly IVisualConsoleService? _visualConsoleService;
    
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _recoveryCheckTask;
    private Task? _acceptConnectionsTask;

    public LoadBalancer(
        string name,
        string listenAddress,
        int listenPort,
        IEnumerable<IBackendService> backends,
        ILoadBalancingStrategy strategy,
        IConnectionForwarder forwarder,
        IBackendHealthCheckService healthCheckService,
        int recoveryCheckIntervalSeconds,
        int recoveryDelaySeconds,
        ILogger<LoadBalancer> logger,
        IConnectionTracker? connectionTracker = null,
        IVisualConsoleService? visualConsoleService = null)
    {
        _name = name;
        _listenAddress = listenAddress;
        _listenPort = listenPort;
        _backends = backends;
        _strategy = strategy;
        _forwarder = forwarder;
        _healthCheckService = healthCheckService;
        _recoveryCheckIntervalSeconds = recoveryCheckIntervalSeconds;
        _recoveryDelaySeconds = recoveryDelaySeconds;
        _logger = logger;
        _connectionTracker = connectionTracker;
        _visualConsoleService = visualConsoleService;
    }

    public string Name => _name;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            if (!IPAddress.TryParse(_listenAddress, out var ipAddress))
            {
                throw new InvalidOperationException($"Invalid listen address: {_listenAddress}.");
            }
            
            _listener = new TcpListener(ipAddress, _listenPort);
            _listener.Start();

            _logger.LogInformation($"Load balancer '{_name}' listening on {_listenAddress}:{_listenPort}");

            _recoveryCheckTask = RecoveryCheckLoopAsync(_cancellationTokenSource.Token);
            _acceptConnectionsTask = AcceptConnectionsLoopAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _cancellationTokenSource?.Dispose();
            _listener?.Stop();
            _logger.LogError($"Failed to start load balancer '{_name}' on {_listenAddress}:{_listenPort} - {ex.Message}");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"Stopping load balancer '{_name}' on {_listenAddress}:{_listenPort}");
        
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch (Exception e)
        {
            _logger.LogDebug($"Error stopping load balancer, load balancer may not have started. {e.Message}");
        }
        
        try
        {
            _listener?.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error stopping listener for load balancer '{_name}': {ex.Message}");
        }

        try
        {
            if (_recoveryCheckTask != null)
            {
                await _recoveryCheckTask.WaitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error stopping recovery check task for load balancer '{_name}': {ex.Message}");
        }

        try
        {
            if (_acceptConnectionsTask != null)
            {
                await _acceptConnectionsTask.WaitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error stopping accept connections task for load balancer '{_name}': {ex.Message}");
        }

        _cancellationTokenSource?.Dispose();
    }

    public void Dispose()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch
        {
        }
        
        try
        {
            _listener?.Stop();
        }
        catch
        {
        }
        
        try
        {
            _cancellationTokenSource?.Dispose();
        }
        catch
        {
        }
    }

    private async Task RecoveryCheckLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Run first health check immediately before entering the delay loop
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var unhealthyBackends = _backends
                    .Where(b => !b.IsHealthy && 
                                (!b.LastFailure.HasValue || 
                                 (now - b.LastFailure.Value).TotalSeconds >= _recoveryDelaySeconds))
                    .ToList();
                
                foreach (var backend in unhealthyBackends)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        var isFirstCheck = !backend.LastFailure.HasValue;
                        var isHealthy = await _healthCheckService.CheckHealthAsync(backend, HealthCheckTimeoutSeconds, cancellationToken);
                        
                        if (isHealthy)
                        {
                            backend.MarkHealthy();
                            _connectionTracker?.UpdateBackendHealth(_name, backend.Address, backend.Port, true);
                            if (isFirstCheck)
                            {
                                _logger.LogInformation($"Load balancer '{_name}': Backend {backend.Address}:{backend.Port} is healthy");
                            }
                            else
                            {
                                _logger.LogInformation($"Load balancer '{_name}': Backend {backend.Address}:{backend.Port} recovered and is now healthy");
                            }
                        }
                        else
                        {
                            backend.MarkUnhealthy();
                            _connectionTracker?.UpdateBackendHealth(_name, backend.Address, backend.Port, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        _logger.LogDebug(ex, $"Load balancer '{_name}': Backend {backend.Address}:{backend.Port} still unhealthy");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_recoveryCheckIntervalSeconds), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            if (!(ex is OperationCanceledException))
            {
                _logger.LogError($"Recovery check loop error for load balancer '{_name}': {ex.Message}");
            }
        }
    }

    private async Task AcceptConnectionsLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener!.AcceptTcpClientAsync(cancellationToken);
                    var clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    _connectionTracker?.RecordConnectionAccepted(_name, clientEndpoint);
                    _logger.LogInformation($"Load balancer '{_name}': New connection accepted from {clientEndpoint}");
                    
                    _ = Task.Run(() => HandleConnectionAsync(tcpClient, clientEndpoint, cancellationToken), cancellationToken);
                }
                catch (Exception ex)
                {                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    _logger.LogWarning($"Load balancer '{_name}': Error accepting connection: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            if (!(ex is OperationCanceledException))
            {
                _logger.LogError($"Accept connections loop error for load balancer '{_name}': {ex.Message}");
            }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, string clientEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var backendCount = _backends.Count(); //never retry more than total backends
                var attemptCount = 0;

                while (_backends.Any(b => b.IsHealthy) && attemptCount < backendCount && !cancellationToken.IsCancellationRequested)
                {
                    attemptCount++;
                    var selectedBackend = _strategy.SelectBackend(_backends);
                    
                    if (selectedBackend == null)
                    {
                        break;
                    }

                    _logger.LogDebug($"Load balancer '{_name}': Client {clientEndpoint} - Attempting connection to backend {selectedBackend.Address}:{selectedBackend.Port} (attempt {attemptCount}/{backendCount})");

                    try
                    {
                        var connected = await _forwarder.ForwardAsync(stream, selectedBackend, cancellationToken);

                        if (connected)
                        {
                            _connectionTracker?.RecordConnectionForwarded(_name, selectedBackend.Address, selectedBackend.Port);
                            _logger.LogInformation($"Load balancer '{_name}': Client {clientEndpoint} - Successfully forwarded connection to backend {selectedBackend.Address}:{selectedBackend.Port}");
                            // Connection completed successfully, mark as closed
                            _connectionTracker?.RecordConnectionClosed(_name);
                            return;
                        }
                        else
                        {
                            selectedBackend.MarkUnhealthy();
                            _connectionTracker?.UpdateBackendHealth(_name, selectedBackend.Address, selectedBackend.Port, false);
                            _connectionTracker?.RecordConnectionFailed(_name, selectedBackend.Address, selectedBackend.Port);
                            _logger.LogWarning($"Load balancer '{_name}': Client {clientEndpoint} - Backend {selectedBackend.Address}:{selectedBackend.Port} connection failed, marking as unhealthy and trying next backend");
                            // Don't close connection here - will retry with another backend
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Exceptions here are data transfer failures (connection succeeded but transfer failed), These should NOT retry with another backend.
                        selectedBackend.MarkUnhealthy();
                        _connectionTracker?.UpdateBackendHealth(_name, selectedBackend.Address, selectedBackend.Port, false);
                        _connectionTracker?.RecordConnectionFailed(_name, selectedBackend.Address, selectedBackend.Port);
                        _connectionTracker?.RecordConnectionClosed(_name);
                        _logger.LogWarning($"Load balancer '{_name}': Client {clientEndpoint} - Backend {selectedBackend.Address}:{selectedBackend.Port} data transfer failed after successful connection, marking as unhealthy: {ex.Message}");
                        return;
                    }
                }

                
                _connectionTracker?.RecordConnectionFailed(_name);
                _connectionTracker?.RecordConnectionClosed(_name);
                _logger.LogWarning($"Load balancer '{_name}': Client {clientEndpoint} - All backends failed to connect, closing connection");
            }
        }
        catch (Exception ex)
        {
            _connectionTracker?.RecordConnectionFailed(_name);
            _connectionTracker?.RecordConnectionClosed(_name);
            _logger.LogDebug(ex, $"Load balancer '{_name}': Client {clientEndpoint} - Connection handling error");
        }
    }
}

