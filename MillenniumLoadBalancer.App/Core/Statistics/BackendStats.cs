namespace MillenniumLoadBalancer.App.Core.Statistics;

/// <summary>
/// Statistics for a single backend.
/// </summary>
public class BackendStats
{
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public long ConnectionsForwarded { get; set; }
    public long ConnectionsFailed { get; set; }
    public bool IsHealthy { get; set; }
}

