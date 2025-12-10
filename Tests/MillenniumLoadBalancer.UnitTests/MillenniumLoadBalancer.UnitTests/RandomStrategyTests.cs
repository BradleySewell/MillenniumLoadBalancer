using MillenniumLoadBalancer.App.Core.Strategies;
using MillenniumLoadBalancer.App.Core.Interfaces;
using Moq;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class RandomStrategyTests
{
    [TestMethod]
    public void SelectBackend_WithHealthyBackends_ReturnsBackend()
    {
        var strategy = new RandomStrategy();
        var backend1 = new Mock<IBackendService>();
        backend1.Setup(b => b.IsHealthy).Returns(true);
        backend1.Setup(b => b.Address).Returns("127.0.0.1");
        backend1.Setup(b => b.Port).Returns(8080);

        var backend2 = new Mock<IBackendService>();
        backend2.Setup(b => b.IsHealthy).Returns(true);
        backend2.Setup(b => b.Address).Returns("127.0.0.1");
        backend2.Setup(b => b.Port).Returns(8081);

        var backends = new[] { backend1.Object, backend2.Object };

        var result = strategy.SelectBackend(backends);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Port == 8080 || result.Port == 8081);
    }

    [TestMethod]
    public void SelectBackend_WithNoHealthyBackends_ReturnsNull()
    {
        var strategy = new RandomStrategy();
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
        var strategy = new RandomStrategy();
        var healthyBackend1 = new Mock<IBackendService>();
        healthyBackend1.Setup(b => b.IsHealthy).Returns(true);
        healthyBackend1.Setup(b => b.Port).Returns(8080);

        var unhealthyBackend = new Mock<IBackendService>();
        unhealthyBackend.Setup(b => b.IsHealthy).Returns(false);

        var healthyBackend2 = new Mock<IBackendService>();
        healthyBackend2.Setup(b => b.IsHealthy).Returns(true);
        healthyBackend2.Setup(b => b.Port).Returns(8082);

        var backends = new[] { healthyBackend1.Object, unhealthyBackend.Object, healthyBackend2.Object };

        var result = strategy.SelectBackend(backends);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Port == 8080 || result.Port == 8082);
    }

    [TestMethod]
    public void SelectBackend_WithEmptyBackendList_ReturnsNull()
    {
        var strategy = new RandomStrategy();
        var backends = Enumerable.Empty<IBackendService>();

        var result = strategy.SelectBackend(backends);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void SelectBackend_WithMultipleCalls_SelectsDifferentBackends()
    {
        var strategy = new RandomStrategy();
        var backends = Enumerable.Range(0, 10)
            .Select(i =>
            {
                var backend = new Mock<IBackendService>();
                backend.Setup(b => b.IsHealthy).Returns(true);
                backend.Setup(b => b.Port).Returns(8080 + i);
                return backend.Object;
            })
            .ToArray();

        var results = Enumerable.Range(0, 100)
            .Select(_ => strategy.SelectBackend(backends))
            .ToList();

        var uniquePorts = results.Select(r => r!.Port).Distinct().ToList();
        
        // With 100 calls and 10 backends, we should get more than 1 unique selection
        // This is a probabilistic test, but with 100 calls it should be very unlikely to only get 1 unique backend
        // Note: Using Assert.IsTrue instead of Assert.IsGreaterThan to avoid parameter order confusion
        var uniqueCount = uniquePorts.Count;
        Assert.IsTrue(uniqueCount > 1, $"Random strategy should select different backends across multiple calls. Got {uniqueCount} unique backends out of 100 calls with 10 available backends. Expected more than 1 unique backend.");
    }
}
