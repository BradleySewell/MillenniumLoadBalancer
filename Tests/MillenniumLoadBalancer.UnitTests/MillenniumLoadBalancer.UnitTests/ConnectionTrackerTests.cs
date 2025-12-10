using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Core.Services;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class ConnectionTrackerTests
{
    [TestMethod]
    public void RecordConnectionAccepted_IncrementsCounters()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(1, stats.TotalConnectionsAccepted);
        Assert.AreEqual(1, stats.TotalActiveConnections);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].ConnectionsAccepted);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].ActiveConnections);
    }

    [TestMethod]
    public void RecordConnectionForwarded_IncrementsCounters()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8080);
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(1, stats.TotalConnectionsForwarded);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].ConnectionsForwarded);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].Backends["192.168.1.1:8080"].ConnectionsForwarded);
    }

    [TestMethod]
    public void RecordConnectionFailed_IncrementsFailedCounters()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        tracker.RecordConnectionFailed("LB1", "192.168.1.1", 8080);
        tracker.RecordConnectionClosed("LB1");
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(1, stats.TotalConnectionsFailed);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].ConnectionsFailed);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].Backends["192.168.1.1:8080"].ConnectionsFailed);
        Assert.AreEqual(0, stats.TotalActiveConnections);
    }

    [TestMethod]
    public void RecordConnectionClosed_DecrementsActiveConnections()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        Assert.AreEqual(1, tracker.GetStatistics().TotalActiveConnections);
        
        tracker.RecordConnectionClosed("LB1");
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(0, stats.TotalActiveConnections);
        Assert.AreEqual(0, stats.LoadBalancers["LB1"].ActiveConnections);
    }

    [TestMethod]
    public void UpdateBackendHealth_UpdatesHealthStatus()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8080);
        tracker.UpdateBackendHealth("LB1", "192.168.1.1", 8080, true);
        
        var stats = tracker.GetStatistics();
        Assert.IsTrue(stats.LoadBalancers["LB1"].Backends["192.168.1.1:8080"].IsHealthy);
        
        tracker.UpdateBackendHealth("LB1", "192.168.1.1", 8080, false);
        
        stats = tracker.GetStatistics();
        Assert.IsFalse(stats.LoadBalancers["LB1"].Backends["192.168.1.1:8080"].IsHealthy);
    }

    [TestMethod]
    public void GetStatistics_ReturnsSnapshot()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8080);
        
        var stats1 = tracker.GetStatistics();
        var stats2 = tracker.GetStatistics();
        
        // Both snapshots should have same values
        Assert.AreEqual(stats1.TotalConnectionsAccepted, stats2.TotalConnectionsAccepted);
        Assert.AreEqual(stats1.TotalConnectionsForwarded, stats2.TotalConnectionsForwarded);
    }

    [TestMethod]
    public void GetStatistics_WithMultipleLoadBalancers_ReturnsAll()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        tracker.RecordConnectionAccepted("LB2", "127.0.0.1:12346");
        tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8080);
        tracker.RecordConnectionForwarded("LB2", "192.168.1.2", 8080);
        
        var stats = tracker.GetStatistics();
        
        Assert.HasCount(2, stats.LoadBalancers);
        Assert.IsTrue(stats.LoadBalancers.ContainsKey("LB1"));
        Assert.IsTrue(stats.LoadBalancers.ContainsKey("LB2"));
        Assert.AreEqual(2, stats.TotalConnectionsAccepted);
        Assert.AreEqual(2, stats.TotalConnectionsForwarded);
    }

    [TestMethod]
    public void GetStatistics_WithMultipleBackends_ReturnsAll()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8080);
        tracker.RecordConnectionForwarded("LB1", "192.168.1.2", 8080);
        tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8081);
        
        var stats = tracker.GetStatistics();
        
        Assert.HasCount(3, stats.LoadBalancers["LB1"].Backends);
        Assert.IsTrue(stats.LoadBalancers["LB1"].Backends.ContainsKey("192.168.1.1:8080"));
        Assert.IsTrue(stats.LoadBalancers["LB1"].Backends.ContainsKey("192.168.1.2:8080"));
        Assert.IsTrue(stats.LoadBalancers["LB1"].Backends.ContainsKey("192.168.1.1:8081"));
    }

    [TestMethod]
    public void Reset_ClearsAllStatistics()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8080);
        tracker.RecordConnectionFailed("LB1", "192.168.1.1", 8080);
        
        tracker.Reset();
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(0, stats.TotalConnectionsAccepted);
        Assert.AreEqual(0, stats.TotalConnectionsForwarded);
        Assert.AreEqual(0, stats.TotalConnectionsFailed);
        Assert.AreEqual(0, stats.TotalActiveConnections);
        Assert.IsEmpty(stats.LoadBalancers);
    }

    [TestMethod]
    public void RecordConnectionAccepted_IsThreadSafe()
    {
        var tracker = new ConnectionTracker();
        
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    tracker.RecordConnectionAccepted("LB1", $"127.0.0.1:{i}");
                }
            }))
            .ToArray();
        
        Task.WaitAll(tasks);
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(1000, stats.TotalConnectionsAccepted);
        Assert.AreEqual(1000, stats.TotalActiveConnections);
    }

    [TestMethod]
    public void RecordConnectionForwarded_IsThreadSafe()
    {
        var tracker = new ConnectionTracker();
        
        var tasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() =>
            {
                tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
                tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8080);
            }))
            .ToArray();
        
        Task.WaitAll(tasks);
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(50, stats.TotalConnectionsForwarded);
        Assert.AreEqual(50, stats.LoadBalancers["LB1"].Backends["192.168.1.1:8080"].ConnectionsForwarded);
    }

    [TestMethod]
    public void RecordConnectionFailed_WithoutBackend_OnlyIncrementsGlobalFailed()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        tracker.RecordConnectionFailed("LB1");
        tracker.RecordConnectionClosed("LB1");
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(1, stats.TotalConnectionsFailed);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].ConnectionsFailed);
        Assert.IsEmpty(stats.LoadBalancers["LB1"].Backends);
    }

    [TestMethod]
    public void RecordConnectionFailed_WithBackend_IncrementsBothGlobalAndBackendFailed()
    {
        var tracker = new ConnectionTracker();
        
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        tracker.RecordConnectionFailed("LB1", "192.168.1.1", 8080);
        tracker.RecordConnectionClosed("LB1");
        
        var stats = tracker.GetStatistics();
        Assert.AreEqual(1, stats.TotalConnectionsFailed);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].ConnectionsFailed);
        Assert.AreEqual(1, stats.LoadBalancers["LB1"].Backends["192.168.1.1:8080"].ConnectionsFailed);
    }

    [TestMethod]
    public void ConnectionLifecycle_TracksCompleteFlow()
    {
        var tracker = new ConnectionTracker();
        
        // Connection accepted
        tracker.RecordConnectionAccepted("LB1", "127.0.0.1:12345");
        var stats = tracker.GetStatistics();
        Assert.AreEqual(1, stats.TotalConnectionsAccepted);
        Assert.AreEqual(1, stats.TotalActiveConnections);
        
        // Connection forwarded
        tracker.RecordConnectionForwarded("LB1", "192.168.1.1", 8080);
        stats = tracker.GetStatistics();
        Assert.AreEqual(1, stats.TotalConnectionsForwarded);
        
        // Connection closed
        tracker.RecordConnectionClosed("LB1");
        stats = tracker.GetStatistics();
        Assert.AreEqual(0, stats.TotalActiveConnections);
    }

    [TestMethod]
    public void UpdateBackendHealth_ForNonExistentBackend_CreatesBackend()
    {
        var tracker = new ConnectionTracker();
        
        tracker.UpdateBackendHealth("LB1", "192.168.1.1", 8080, true);
        
        var stats = tracker.GetStatistics();
        Assert.IsTrue(stats.LoadBalancers["LB1"].Backends.ContainsKey("192.168.1.1:8080"));
        Assert.IsTrue(stats.LoadBalancers["LB1"].Backends["192.168.1.1:8080"].IsHealthy);
    }
}

