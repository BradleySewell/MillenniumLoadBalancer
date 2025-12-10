using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Core.Statistics;
using System.Collections.Concurrent;

namespace MillenniumLoadBalancer.App.Core.Services;

/// <summary>
/// Thread-safe connection tracker implementation.
/// </summary>
internal class ConnectionTracker : IConnectionTracker
{
    private class LoadBalancerStatsInternal
    {
        public string Name { get; set; } = string.Empty;
        public long ConnectionsAccepted;
        public long ConnectionsForwarded;
        public long ConnectionsFailed;
        public long ActiveConnections;
        public ConcurrentDictionary<string, BackendStatsInternal> Backends = new();
    }

    private class BackendStatsInternal
    {
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public long ConnectionsForwarded;
        public long ConnectionsFailed;
        public bool IsHealthy;
    }

    private readonly ConcurrentDictionary<string, LoadBalancerStatsInternal> _loadBalancers = new();
    private long _totalConnectionsAccepted;
    private long _totalConnectionsForwarded;
    private long _totalConnectionsFailed;
    private long _totalActiveConnections;

    public void RecordConnectionAccepted(string loadBalancerName, string clientEndpoint)
    {
        var stats = _loadBalancers.GetOrAdd(loadBalancerName, _ => new LoadBalancerStatsInternal { Name = loadBalancerName });
        Interlocked.Increment(ref stats.ConnectionsAccepted);
        Interlocked.Increment(ref _totalConnectionsAccepted);
        Interlocked.Increment(ref stats.ActiveConnections);
        Interlocked.Increment(ref _totalActiveConnections);
    }

    public void RecordConnectionForwarded(string loadBalancerName, string backendAddress, int backendPort)
    {
        var stats = _loadBalancers.GetOrAdd(loadBalancerName, _ => new LoadBalancerStatsInternal { Name = loadBalancerName });
        var backendKey = $"{backendAddress}:{backendPort}";
        var backendStats = stats.Backends.GetOrAdd(backendKey, _ => new BackendStatsInternal 
        { 
            Address = backendAddress, 
            Port = backendPort 
        });
        
        Interlocked.Increment(ref backendStats.ConnectionsForwarded);
        Interlocked.Increment(ref stats.ConnectionsForwarded);
        Interlocked.Increment(ref _totalConnectionsForwarded);
    }

    public void RecordConnectionFailed(string loadBalancerName, string? backendAddress = null, int? backendPort = null)
    {
        var stats = _loadBalancers.GetOrAdd(loadBalancerName, _ => new LoadBalancerStatsInternal { Name = loadBalancerName });
        
        if (backendAddress != null && backendPort.HasValue)
        {
            var backendKey = $"{backendAddress}:{backendPort.Value}";
            var backendStats = stats.Backends.GetOrAdd(backendKey, _ => new BackendStatsInternal 
            { 
                Address = backendAddress, 
                Port = backendPort.Value 
            });
            Interlocked.Increment(ref backendStats.ConnectionsFailed);
        }
        
        Interlocked.Increment(ref stats.ConnectionsFailed);
        Interlocked.Increment(ref _totalConnectionsFailed);
        // dont decrement active connections here - RecordConnectionClosed handles that
    }

    public void RecordConnectionClosed(string loadBalancerName)
    {
        if (_loadBalancers.TryGetValue(loadBalancerName, out var stats))
        {
            Interlocked.Decrement(ref stats.ActiveConnections);
            Interlocked.Decrement(ref _totalActiveConnections);
        }
    }

    public void UpdateBackendHealth(string loadBalancerName, string backendAddress, int backendPort, bool isHealthy)
    {
        var stats = _loadBalancers.GetOrAdd(loadBalancerName, _ => new LoadBalancerStatsInternal { Name = loadBalancerName });
        var backendKey = $"{backendAddress}:{backendPort}";
        var backendStats = stats.Backends.GetOrAdd(backendKey, _ => new BackendStatsInternal 
        { 
            Address = backendAddress, 
            Port = backendPort 
        });
        backendStats.IsHealthy = isHealthy;
    }

    public ConnectionStatistics GetStatistics()
    {
        var snapshot = new ConnectionStatistics
        {
            TotalConnectionsAccepted = _totalConnectionsAccepted,
            TotalConnectionsForwarded = _totalConnectionsForwarded,
            TotalConnectionsFailed = _totalConnectionsFailed,
            TotalActiveConnections = _totalActiveConnections
        };

        foreach (var kvp in _loadBalancers)
        {
            var lbStats = new LoadBalancerStats
            {
                Name = kvp.Value.Name,
                ConnectionsAccepted = kvp.Value.ConnectionsAccepted,
                ConnectionsForwarded = kvp.Value.ConnectionsForwarded,
                ConnectionsFailed = kvp.Value.ConnectionsFailed,
                ActiveConnections = kvp.Value.ActiveConnections
            };

            foreach (var backendKvp in kvp.Value.Backends)
            {
                lbStats.Backends[backendKvp.Key] = new BackendStats
                {
                    Address = backendKvp.Value.Address,
                    Port = backendKvp.Value.Port,
                    ConnectionsForwarded = backendKvp.Value.ConnectionsForwarded,
                    ConnectionsFailed = backendKvp.Value.ConnectionsFailed,
                    IsHealthy = backendKvp.Value.IsHealthy
                };
            }

            snapshot.LoadBalancers[kvp.Key] = lbStats;
        }

        return snapshot;
    }

    public void Reset()
    {
        _loadBalancers.Clear();
        _totalConnectionsAccepted = 0;
        _totalConnectionsForwarded = 0;
        _totalConnectionsFailed = 0;
        _totalActiveConnections = 0;
    }
}

