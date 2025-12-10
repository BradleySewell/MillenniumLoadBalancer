using MillenniumLoadBalancer.App.Core.Statistics;

namespace MillenniumLoadBalancer.App.Core.Interfaces;

/// <summary>
/// Tracks connection statistics for load balancers.
/// </summary>
public interface IConnectionTracker
{
    /// <summary>
    /// Records a new incoming connection.
    /// </summary>
    void RecordConnectionAccepted(string loadBalancerName, string clientEndpoint);
    
    /// <summary>
    /// Records a connection forwarded to a backend.
    /// </summary>
    void RecordConnectionForwarded(string loadBalancerName, string backendAddress, int backendPort);
    
    /// <summary>
    /// Records a connection failure.
    /// </summary>
    void RecordConnectionFailed(string loadBalancerName, string? backendAddress = null, int? backendPort = null);
    
    /// <summary>
    /// Records a connection closed.
    /// </summary>
    void RecordConnectionClosed(string loadBalancerName);
    
    /// <summary>
    /// Updates backend health status.
    /// </summary>
    void UpdateBackendHealth(string loadBalancerName, string backendAddress, int backendPort, bool isHealthy);
    
    /// <summary>
    /// Gets current connection statistics.
    /// </summary>
    ConnectionStatistics GetStatistics();
    
    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

