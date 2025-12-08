using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;
using MillenniumLoadBalancer.App.Infrastructure.Factories;
using Moq;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class LoadBalancerFactoryTests
{
    private Mock<ILoadBalancingStrategyFactory> _strategyFactoryMock = null!;
    private Mock<IConnectionForwarderFactory> _forwarderFactoryMock = null!;
    private Mock<IBackendServiceFactory> _backendServiceFactoryMock = null!;
    private Mock<IBackendHealthCheckService> _healthCheckServiceMock = null!;
    private Mock<ILogger<LoadBalancer>> _loggerMock = null!;
    private LoadBalancerFactory _factory = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _strategyFactoryMock = new Mock<ILoadBalancingStrategyFactory>();
        _forwarderFactoryMock = new Mock<IConnectionForwarderFactory>();
        _backendServiceFactoryMock = new Mock<IBackendServiceFactory>();
        _healthCheckServiceMock = new Mock<IBackendHealthCheckService>();
        _loggerMock = new Mock<ILogger<LoadBalancer>>();

        _factory = new LoadBalancerFactory(
            _strategyFactoryMock.Object,
            _forwarderFactoryMock.Object,
            _backendServiceFactoryMock.Object,
            _healthCheckServiceMock.Object,
            _loggerMock.Object);
    }

    [TestMethod]
    public void Create_WithValidConfiguration_ReturnsLoadBalancer()
    {
        
        var config = CreateValidConfiguration();
        SetupMocks();

        
        var loadBalancer = _factory.Create(config);

        
        Assert.IsNotNull(loadBalancer);
        Assert.IsInstanceOfType(loadBalancer, typeof(ILoadBalancer));
        Assert.AreEqual("TestLB", loadBalancer.Name);
    }

    [TestMethod]
    public void Create_WithNullConfiguration_ThrowsArgumentNullException()
    {
        
        Assert.ThrowsExactly<ArgumentNullException>(
            () => _factory.Create(null!));
    }

    [TestMethod]
    public void Create_WithNullName_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.Name = null!;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("name") || ex.Message.Contains("Name"));
    }

    [TestMethod]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.Name = "";

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("name") || ex.Message.Contains("Name"));
    }

    [TestMethod]
    public void Create_WithNullListenAddress_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.ListenAddress = null!;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("address") || ex.Message.Contains("Address"));
    }

    [TestMethod]
    public void Create_WithInvalidListenAddress_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.ListenAddress = "invalid-ip";

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("Invalid") || ex.Message.Contains("address"));
    }

    [TestMethod]
    public void Create_WithInvalidListenPort_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.ListenPort = 0;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("port") || ex.Message.Contains("Port"));
    }

    [TestMethod]
    public void Create_WithPortTooHigh_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.ListenPort = 65536;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("port") || ex.Message.Contains("Port"));
    }

    [TestMethod]
    public void Create_WithNullBackends_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.Backends = null!;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("backend") || ex.Message.Contains("Backend"));
    }

    [TestMethod]
    public void Create_WithEmptyBackends_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.Backends = new List<BackendConfiguration>();

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("backend") || ex.Message.Contains("Backend"));
    }

    [TestMethod]
    public void Create_WithInvalidBackendAddress_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.Backends[0].Address = "invalid-ip";

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("address") || ex.Message.Contains("Address"));
    }

    [TestMethod]
    public void Create_WithInvalidBackendPort_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.Backends[0].Port = 0;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("port") || ex.Message.Contains("Port"));
    }

    [TestMethod]
    public void Create_WithInvalidConnectionTimeout_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.ConnectionTimeoutSeconds = 0;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("timeout") || ex.Message.Contains("Timeout"));
    }

    [TestMethod]
    public void Create_WithInvalidSendTimeout_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.SendTimeoutSeconds = -1;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("timeout") || ex.Message.Contains("Timeout"));
    }

    [TestMethod]
    public void Create_WithInvalidReceiveTimeout_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.ReceiveTimeoutSeconds = 0;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("timeout") || ex.Message.Contains("Timeout"));
    }

    [TestMethod]
    public void Create_WithInvalidRecoveryCheckInterval_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.RecoveryCheckIntervalSeconds = -1;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("interval") || ex.Message.Contains("Interval"));
    }

    [TestMethod]
    public void Create_WithInvalidRecoveryDelay_ThrowsArgumentException()
    {
        
        var config = CreateValidConfiguration();
        config.RecoveryDelaySeconds = 0;

        
        var ex = Assert.ThrowsExactly<ArgumentException>(
            () => _factory.Create(config));
        
        Assert.IsTrue(ex.Message.Contains("delay") || ex.Message.Contains("Delay"));
    }

    [TestMethod]
    public void Create_CallsBackendServiceFactoryForEachBackend()
    {
        
        var config = CreateValidConfiguration();
        config.Backends = new List<BackendConfiguration>
        {
            new() { Address = "127.0.0.1", Port = 8080 },
            new() { Address = "127.0.0.1", Port = 8081 },
            new() { Address = "127.0.0.1", Port = 8082 }
        };
        SetupMocks();

        
        _factory.Create(config);

        
        _backendServiceFactoryMock.Verify(
            f => f.Create(It.IsAny<string>(), It.IsAny<int>()),
            Times.Exactly(3));
    }

    private ListenerConfiguration CreateValidConfiguration()
    {
        return new ListenerConfiguration
        {
            Name = "TestLB",
            ListenAddress = "127.0.0.1",
            ListenPort = 8080,
            Protocol = "TCP",
            Strategy = "RoundRobin",
            Backends = new List<BackendConfiguration>
            {
                new() { Address = "127.0.0.1", Port = 9000 }
            },
            ConnectionTimeoutSeconds = 10,
            SendTimeoutSeconds = 30,
            ReceiveTimeoutSeconds = 30,
            RecoveryCheckIntervalSeconds = 10,
            RecoveryDelaySeconds = 30
        };
    }

    private void SetupMocks()
    {
        _strategyFactoryMock.Setup(f => f.Create(It.IsAny<string>()))
            .Returns(new Mock<ILoadBalancingStrategy>().Object);

        _forwarderFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new Mock<IConnectionForwarder>().Object);

        _backendServiceFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string addr, int port) => new Mock<IBackendService>().Object);
    }
}
