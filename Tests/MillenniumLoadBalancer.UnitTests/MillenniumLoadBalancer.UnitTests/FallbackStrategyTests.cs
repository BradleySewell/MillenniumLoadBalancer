using MillenniumLoadBalancer.App.Core.Strategies;
using MillenniumLoadBalancer.App.Core.Interfaces;
using Moq;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class FallbackStrategyTests
{
    [TestMethod]
    public void SelectBackend_WithHealthyBackends_ReturnsFirstHealthyBackend()
    {
        var strategy = new FallbackStrategy();
        var backend1 = new Mock<IBackendService>();
        backend1.Setup(b => b.IsHealthy).Returns(true);
        backend1.Setup(b => b.Address).Returns("127.0.0.1");
        backend1.Setup(b => b.Port).Returns(8080);

        var backend2 = new Mock<IBackendService>();
        backend2.Setup(b => b.IsHealthy).Returns(true);
        backend2.Setup(b => b.Address).Returns("127.0.0.1");
        backend2.Setup(b => b.Port).Returns(8081);

        var backends = new[] { backend1.Object, backend2.Object };

        var result1 = strategy.SelectBackend(backends);
        var result2 = strategy.SelectBackend(backends);
        var result3 = strategy.SelectBackend(backends);

        Assert.IsNotNull(result1);
        Assert.AreEqual(8080, result1.Port);
        Assert.AreEqual(8080, result2!.Port);
        Assert.AreEqual(8080, result3!.Port);
    }

    [TestMethod]
    public void SelectBackend_WhenFirstBackendBecomesUnhealthy_MovesToNextHealthy()
    {
        var strategy = new FallbackStrategy();
        var backend1 = new Mock<IBackendService>();
        backend1.Setup(b => b.IsHealthy).Returns(true);
        backend1.Setup(b => b.Port).Returns(8080);

        var backend2 = new Mock<IBackendService>();
        backend2.Setup(b => b.IsHealthy).Returns(true);
        backend2.Setup(b => b.Port).Returns(8081);

        var backend3 = new Mock<IBackendService>();
        backend3.Setup(b => b.IsHealthy).Returns(true);
        backend3.Setup(b => b.Port).Returns(8082);

        var backends = new[] { backend1.Object, backend2.Object, backend3.Object };

        var result1 = strategy.SelectBackend(backends);
        Assert.AreEqual(8080, result1!.Port);

        backend1.Setup(b => b.IsHealthy).Returns(false);

        var result2 = strategy.SelectBackend(backends);
        Assert.AreEqual(8081, result2!.Port);

        var result3 = strategy.SelectBackend(backends);
        Assert.AreEqual(8081, result3!.Port);
    }

    [TestMethod]
    public void SelectBackend_WhenCurrentBackendBecomesUnhealthy_SkipsUnhealthyBackends()
    {
        var strategy = new FallbackStrategy();
        var backend1 = new Mock<IBackendService>();
        backend1.Setup(b => b.IsHealthy).Returns(true);
        backend1.Setup(b => b.Port).Returns(8080);

        var backend2 = new Mock<IBackendService>();
        backend2.Setup(b => b.IsHealthy).Returns(false);
        backend2.Setup(b => b.Port).Returns(8081);

        var backend3 = new Mock<IBackendService>();
        backend3.Setup(b => b.IsHealthy).Returns(true);
        backend3.Setup(b => b.Port).Returns(8082);

        var backends = new[] { backend1.Object, backend2.Object, backend3.Object };

        var result1 = strategy.SelectBackend(backends);
        Assert.AreEqual(8080, result1!.Port);

        backend1.Setup(b => b.IsHealthy).Returns(false);

        var result2 = strategy.SelectBackend(backends);
        Assert.AreEqual(8082, result2!.Port);
    }

    [TestMethod]
    public void SelectBackend_WithNoHealthyBackends_ReturnsNull()
    {
        var strategy = new FallbackStrategy();
        var backend1 = new Mock<IBackendService>();
        backend1.Setup(b => b.IsHealthy).Returns(false);

        var backend2 = new Mock<IBackendService>();
        backend2.Setup(b => b.IsHealthy).Returns(false);

        var backends = new[] { backend1.Object, backend2.Object };

        var result = strategy.SelectBackend(backends);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void SelectBackend_WithEmptyBackendList_ReturnsNull()
    {
        var strategy = new FallbackStrategy();
        var backends = Enumerable.Empty<IBackendService>();

        var result = strategy.SelectBackend(backends);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void SelectBackend_WhenFirstBackendRecovers_ReturnsToFirstBackend()
    {
        var strategy = new FallbackStrategy();
        var backend1 = new Mock<IBackendService>();
        backend1.Setup(b => b.IsHealthy).Returns(true);
        backend1.Setup(b => b.Port).Returns(8080);

        var backend2 = new Mock<IBackendService>();
        backend2.Setup(b => b.IsHealthy).Returns(true);
        backend2.Setup(b => b.Port).Returns(8081);

        var backends = new[] { backend1.Object, backend2.Object };

        var result1 = strategy.SelectBackend(backends);
        Assert.AreEqual(8080, result1!.Port);

        backend1.Setup(b => b.IsHealthy).Returns(false);
        var result2 = strategy.SelectBackend(backends);
        Assert.AreEqual(8081, result2!.Port);

        backend1.Setup(b => b.IsHealthy).Returns(true);
        var result3 = strategy.SelectBackend(backends);
        Assert.AreEqual(8080, result3!.Port);
    }

    [TestMethod]
    public void SelectBackend_IsThreadSafe()
    {
        var strategy = new FallbackStrategy();
        var backends = Enumerable.Range(0, 3)
            .Select(i =>
            {
                var backend = new Mock<IBackendService>();
                backend.Setup(b => b.IsHealthy).Returns(true);
                backend.Setup(b => b.Port).Returns(8080 + i);
                return backend.Object;
            })
            .ToArray();

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => strategy.SelectBackend(backends)))
            .ToArray();

        Task.WaitAll(tasks);

        Assert.IsTrue(tasks.All(t => t.Result != null));
    }
}
