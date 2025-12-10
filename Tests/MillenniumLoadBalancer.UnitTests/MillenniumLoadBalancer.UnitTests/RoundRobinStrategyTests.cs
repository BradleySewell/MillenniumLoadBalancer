using MillenniumLoadBalancer.App.Core.Strategies;
using MillenniumLoadBalancer.App.Core.Interfaces;
using Moq;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class RoundRobinStrategyTests
{
    [TestMethod]
    public void SelectBackend_WithHealthyBackends_ReturnsBackendsInRoundRobinOrder()
    {
        
        var strategy = new RoundRobinStrategy();
        var backend1 = new Mock<IBackendService>();
        backend1.Setup(b => b.IsHealthy).Returns(true);
        backend1.Setup(b => b.Address).Returns("127.0.0.1");
        backend1.Setup(b => b.Port).Returns(8080);

        var backend2 = new Mock<IBackendService>();
        backend2.Setup(b => b.IsHealthy).Returns(true);
        backend2.Setup(b => b.Address).Returns("127.0.0.1");
        backend2.Setup(b => b.Port).Returns(8081);

        var backend3 = new Mock<IBackendService>();
        backend3.Setup(b => b.IsHealthy).Returns(true);
        backend3.Setup(b => b.Address).Returns("127.0.0.1");
        backend3.Setup(b => b.Port).Returns(8082);

        var backends = new[] { backend1.Object, backend2.Object, backend3.Object };

        // First round
        var selected1 = strategy.SelectBackend(backends);
        Assert.AreEqual(8080, selected1!.Port);

        var selected2 = strategy.SelectBackend(backends);
        Assert.AreEqual(8081, selected2!.Port);

        var selected3 = strategy.SelectBackend(backends);
        Assert.AreEqual(8082, selected3!.Port);

        // Second round (should wrap around)
        var selected4 = strategy.SelectBackend(backends);
        Assert.AreEqual(8080, selected4!.Port);
    }

    [TestMethod]
    public void SelectBackend_WithNoHealthyBackends_ReturnsNull()
    {
        
        var strategy = new RoundRobinStrategy();
        var backend1 = new Mock<IBackendService>();
        backend1.Setup(b => b.IsHealthy).Returns(false);

        var backend2 = new Mock<IBackendService>();
        backend2.Setup(b => b.IsHealthy).Returns(false);

        var backends = new[] { backend1.Object, backend2.Object };

        
        var result = strategy.SelectBackend(backends);

        
        Assert.IsNull(result);
    }

    [TestMethod]
    public void SelectBackend_WithMixedHealthyAndUnhealthyBackends_OnlySelectsHealthyOnes()
    {
        
        var strategy = new RoundRobinStrategy();
        var healthyBackend1 = new Mock<IBackendService>();
        healthyBackend1.Setup(b => b.IsHealthy).Returns(true);
        healthyBackend1.Setup(b => b.Port).Returns(8080);

        var unhealthyBackend = new Mock<IBackendService>();
        unhealthyBackend.Setup(b => b.IsHealthy).Returns(false);

        var healthyBackend2 = new Mock<IBackendService>();
        healthyBackend2.Setup(b => b.IsHealthy).Returns(true);
        healthyBackend2.Setup(b => b.Port).Returns(8082);

        var backends = new[] { healthyBackend1.Object, unhealthyBackend.Object, healthyBackend2.Object };

        
        var selected1 = strategy.SelectBackend(backends);
        var selected2 = strategy.SelectBackend(backends);
        var selected3 = strategy.SelectBackend(backends);

        
        Assert.IsNotNull(selected1);
        Assert.IsNotNull(selected2);
        Assert.IsNotNull(selected3);
        Assert.AreEqual(8080, selected1.Port);
        Assert.AreEqual(8082, selected2.Port);
        Assert.AreEqual(8080, selected3.Port); // Should wrap around
    }

    [TestMethod]
    public void SelectBackend_WithEmptyBackendList_ReturnsNull()
    {
        
        var strategy = new RoundRobinStrategy();
        var backends = Enumerable.Empty<IBackendService>();

        
        var result = strategy.SelectBackend(backends);

        
        Assert.IsNull(result);
    }

    [TestMethod]
    public void SelectBackend_IsThreadSafe()
    {
        
        var strategy = new RoundRobinStrategy();
        var backends = Enumerable.Range(0, 3)
            .Select(i =>
            {
                var backend = new Mock<IBackendService>();
                backend.Setup(b => b.IsHealthy).Returns(true);
                backend.Setup(b => b.Port).Returns(8080 + i);
                return backend.Object;
            })
            .ToArray();

        // Run multiple selections concurrently
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => strategy.SelectBackend(backends)))
            .ToArray();

        Task.WaitAll(tasks);

        // All selections should be non-null (no exceptions thrown)
        Assert.IsTrue(tasks.All(t => t.Result != null));
    }
}
