using MillenniumLoadBalancer.App.Core.Entities;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class BackendServiceTests
{
    [TestMethod]
    public void Constructor_SetsAddressAndPort()
    {

        var backend = new BackendService("127.0.0.1", 8080);

        
        Assert.AreEqual("127.0.0.1", backend.Address);
        Assert.AreEqual(8080, backend.Port);
    }

    [TestMethod]
    public void IsHealthy_Initially_ReturnsFalse()
    {

        var backend = new BackendService("127.0.0.1", 8080);

        
        Assert.IsFalse(backend.IsHealthy);
    }

    [TestMethod]
    public void LastFailure_Initially_ReturnsNull()
    {

        var backend = new BackendService("127.0.0.1", 8080);

        
        Assert.IsNull(backend.LastFailure);
    }

    [TestMethod]
    public void MarkUnhealthy_SetsIsHealthyToFalse()
    {
        
        var backend = new BackendService("127.0.0.1", 8080);

        
        backend.MarkUnhealthy();

        
        Assert.IsFalse(backend.IsHealthy);
    }

    [TestMethod]
    public void MarkUnhealthy_SetsLastFailure()
    {
        
        var backend = new BackendService("127.0.0.1", 8080);
        var beforeMark = DateTime.UtcNow;

        
        backend.MarkUnhealthy();
        var afterMark = DateTime.UtcNow;

        
        Assert.IsNotNull(backend.LastFailure);
        Assert.IsTrue(backend.LastFailure >= beforeMark);
        Assert.IsTrue(backend.LastFailure <= afterMark);
    }

    [TestMethod]
    public void MarkHealthy_SetsIsHealthyToTrue()
    {
        
        var backend = new BackendService("127.0.0.1", 8080);
        backend.MarkUnhealthy();
        Assert.IsFalse(backend.IsHealthy);

        
        backend.MarkHealthy();

        
        Assert.IsTrue(backend.IsHealthy);
    }

    [TestMethod]
    public void MarkHealthy_ClearsLastFailure()
    {
        
        var backend = new BackendService("127.0.0.1", 8080);
        backend.MarkUnhealthy();
        Assert.IsNotNull(backend.LastFailure);

        
        backend.MarkHealthy();

        
        Assert.IsNull(backend.LastFailure);
    }

    [TestMethod]
    public void MarkUnhealthy_ThenMarkHealthy_CanToggleMultipleTimes()
    {
        
        var backend = new BackendService("127.0.0.1", 8080);

        
        for (int i = 0; i < 5; i++)
        {
            backend.MarkUnhealthy();
            Assert.IsFalse(backend.IsHealthy);
            Assert.IsNotNull(backend.LastFailure);

            backend.MarkHealthy();
            Assert.IsTrue(backend.IsHealthy);
            Assert.IsNull(backend.LastFailure);
        }
    }

    [TestMethod]
    public void LastFailure_IsThreadSafe()
    {
        
        var backend = new BackendService("127.0.0.1", 8080);

        // Access LastFailure from multiple threads
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    backend.MarkUnhealthy();
                    var failure = backend.LastFailure;
                    backend.MarkHealthy();
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        // No exceptions thrown, final state should be healthy
        Assert.IsTrue(backend.IsHealthy);
    }

    [TestMethod]
    public void Constructor_WithTlsEnabled_SetsTlsProperties()
    {
        
        var backend = new BackendService("127.0.0.1", 8080, enableTls: true, validateCertificate: false);

        
        Assert.IsTrue(backend.EnableTls);
        Assert.IsFalse(backend.ValidateCertificate);
    }

    [TestMethod]
    public void Constructor_DefaultTlsSettings_AreCorrect()
    {
        
        var backend = new BackendService("127.0.0.1", 8080);

        
        Assert.IsFalse(backend.EnableTls);
        Assert.IsTrue(backend.ValidateCertificate);
    }
}
