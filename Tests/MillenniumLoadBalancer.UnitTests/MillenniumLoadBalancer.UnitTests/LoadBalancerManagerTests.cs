using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Configuration;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;
using Moq;
using System.Collections.Generic;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class LoadBalancerManagerTests
{
    private Mock<ILoadBalancerFactory> _loadBalancerFactoryMock = null!;
    private Mock<ILogger<LoadBalancerManager>> _loggerMock = null!;
    private LoadBalancerManager _manager = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _loadBalancerFactoryMock = new Mock<ILoadBalancerFactory>();
        _loggerMock = new Mock<ILogger<LoadBalancerManager>>();
    }

    private IConfiguration CreateConfiguration(LoadBalancerOptions? options)
    {
        var configDict = new Dictionary<string, string?>();

        if (options != null)
        {
            for (int i = 0; i < options.Listeners.Count; i++)
            {
                var listener = options.Listeners[i];
                configDict[$"LoadBalancer:Listeners:{i}:Name"] = listener.Name;
                configDict[$"LoadBalancer:Listeners:{i}:Protocol"] = listener.Protocol;
                configDict[$"LoadBalancer:Listeners:{i}:Strategy"] = listener.Strategy;
                configDict[$"LoadBalancer:Listeners:{i}:ListenAddress"] = listener.ListenAddress;
                configDict[$"LoadBalancer:Listeners:{i}:ListenPort"] = listener.ListenPort.ToString();
                configDict[$"LoadBalancer:Listeners:{i}:RecoveryCheckIntervalSeconds"] = listener.RecoveryCheckIntervalSeconds.ToString();
                configDict[$"LoadBalancer:Listeners:{i}:RecoveryDelaySeconds"] = listener.RecoveryDelaySeconds.ToString();
                configDict[$"LoadBalancer:Listeners:{i}:ConnectionTimeoutSeconds"] = listener.ConnectionTimeoutSeconds.ToString();
                configDict[$"LoadBalancer:Listeners:{i}:SendTimeoutSeconds"] = listener.SendTimeoutSeconds.ToString();
                configDict[$"LoadBalancer:Listeners:{i}:ReceiveTimeoutSeconds"] = listener.ReceiveTimeoutSeconds.ToString();

                for (int j = 0; j < listener.Backends.Count; j++)
                {
                    var backend = listener.Backends[j];
                    configDict[$"LoadBalancer:Listeners:{i}:Backends:{j}:Address"] = backend.Address;
                    configDict[$"LoadBalancer:Listeners:{i}:Backends:{j}:Port"] = backend.Port.ToString();
                }
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    [TestMethod]
    public void Initialize_WithValidConfiguration_CreatesLoadBalancers()
    {
        
        var options = new LoadBalancerOptions
        {
            Listeners = new List<ListenerConfiguration>
            {
                new()
                {
                    Name = "LB1",
                    ListenAddress = "127.0.0.1",
                    ListenPort = 8080,
                    Backends = new List<BackendConfiguration>
                    {
                        new() { Address = "127.0.0.1", Port = 9000 }
                    }
                },
                new()
                {
                    Name = "LB2",
                    ListenAddress = "127.0.0.1",
                    ListenPort = 8081,
                    Backends = new List<BackendConfiguration>
                    {
                        new() { Address = "127.0.0.1", Port = 9001 }
                    }
                }
            }
        };

        var configuration = CreateConfiguration(options);
        _manager = new LoadBalancerManager(
            configuration,
            _loadBalancerFactoryMock.Object,
            _loggerMock.Object);

        var loadBalancer1Mock = new Mock<ILoadBalancer>();
        loadBalancer1Mock.Setup(lb => lb.Name).Returns("LB1");
        var loadBalancer2Mock = new Mock<ILoadBalancer>();
        loadBalancer2Mock.Setup(lb => lb.Name).Returns("LB2");

        _loadBalancerFactoryMock.Setup(f => f.Create(It.Is<ListenerConfiguration>(c => c.Name == "LB1")))
            .Returns(loadBalancer1Mock.Object);
        _loadBalancerFactoryMock.Setup(f => f.Create(It.Is<ListenerConfiguration>(c => c.Name == "LB2")))
            .Returns(loadBalancer2Mock.Object);

        
        _manager.Initialize();

        
        _loadBalancerFactoryMock.Verify(
            f => f.Create(It.IsAny<ListenerConfiguration>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public void Initialize_WithMissingConfiguration_ThrowsInvalidOperationException()
    {
        
        var configuration = CreateConfiguration(null);
        _manager = new LoadBalancerManager(
            configuration,
            _loadBalancerFactoryMock.Object,
            _loggerMock.Object);

        
        Assert.ThrowsExactly<InvalidOperationException>(() => _manager.Initialize());
    }

    [TestMethod]
    public async Task StartAllAsync_StartsAllLoadBalancers()
    {
        
        var options = new LoadBalancerOptions
        {
            Listeners = new List<ListenerConfiguration>
            {
                new()
                {
                    Name = "LB1",
                    ListenAddress = "127.0.0.1",
                    ListenPort = 8080,
                    Backends = new List<BackendConfiguration>
                    {
                        new() { Address = "127.0.0.1", Port = 9000 }
                    }
                }
            }
        };

        var configuration = CreateConfiguration(options);
        _manager = new LoadBalancerManager(
            configuration,
            _loadBalancerFactoryMock.Object,
            _loggerMock.Object);

        var loadBalancerMock = new Mock<ILoadBalancer>();
        loadBalancerMock.Setup(lb => lb.Name).Returns("LB1");
        _loadBalancerFactoryMock.Setup(f => f.Create(It.IsAny<ListenerConfiguration>()))
            .Returns(loadBalancerMock.Object);

        _manager.Initialize();

        
        await _manager.StartAllAsync();

        
        loadBalancerMock.Verify(
            lb => lb.StartAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        await _manager.StopAllAsync();
    }

    [TestMethod]
    public async Task StartAllAsync_WhenLoadBalancerFails_ThrowsException()
    {
        
        var options = new LoadBalancerOptions
        {
            Listeners = new List<ListenerConfiguration>
            {
                new()
                {
                    Name = "LB1",
                    ListenAddress = "127.0.0.1",
                    ListenPort = 8080,
                    Backends = new List<BackendConfiguration>
                    {
                        new() { Address = "127.0.0.1", Port = 9000 }
                    }
                }
            }
        };

        var configuration = CreateConfiguration(options);
        _manager = new LoadBalancerManager(
            configuration,
            _loadBalancerFactoryMock.Object,
            _loggerMock.Object);

        var loadBalancerMock = new Mock<ILoadBalancer>();
        loadBalancerMock.Setup(lb => lb.Name).Returns("LB1");
        loadBalancerMock.Setup(lb => lb.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Start failed"));

        _loadBalancerFactoryMock.Setup(f => f.Create(It.IsAny<ListenerConfiguration>()))
            .Returns(loadBalancerMock.Object);

        _manager.Initialize();

        
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _manager.StartAllAsync());
    }

    [TestMethod]
    public async Task StopAllAsync_StopsAllLoadBalancers()
    {
        
        var options = new LoadBalancerOptions
        {
            Listeners = new List<ListenerConfiguration>
            {
                new()
                {
                    Name = "LB1",
                    ListenAddress = "127.0.0.1",
                    ListenPort = 8080,
                    Backends = new List<BackendConfiguration>
                    {
                        new() { Address = "127.0.0.1", Port = 9000 }
                    }
                }
            }
        };

        var configuration = CreateConfiguration(options);
        _manager = new LoadBalancerManager(
            configuration,
            _loadBalancerFactoryMock.Object,
            _loggerMock.Object);

        var loadBalancerMock = new Mock<ILoadBalancer>();
        loadBalancerMock.Setup(lb => lb.Name).Returns("LB1");
        _loadBalancerFactoryMock.Setup(f => f.Create(It.IsAny<ListenerConfiguration>()))
            .Returns(loadBalancerMock.Object);

        _manager.Initialize();
        await _manager.StartAllAsync();

        
        await _manager.StopAllAsync();

        
        loadBalancerMock.Verify(
            lb => lb.StopAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task StopAllAsync_WhenLoadBalancerFails_ContinuesStoppingOthers()
    {
        
        var options = new LoadBalancerOptions
        {
            Listeners = new List<ListenerConfiguration>
            {
                new()
                {
                    Name = "LB1",
                    ListenAddress = "127.0.0.1",
                    ListenPort = 8080,
                    Backends = new List<BackendConfiguration>
                    {
                        new() { Address = "127.0.0.1", Port = 9000 }
                    }
                },
                new()
                {
                    Name = "LB2",
                    ListenAddress = "127.0.0.1",
                    ListenPort = 8081,
                    Backends = new List<BackendConfiguration>
                    {
                        new() { Address = "127.0.0.1", Port = 9001 }
                    }
                }
            }
        };

        var configuration = CreateConfiguration(options);
        _manager = new LoadBalancerManager(
            configuration,
            _loadBalancerFactoryMock.Object,
            _loggerMock.Object);

        var loadBalancer1Mock = new Mock<ILoadBalancer>();
        loadBalancer1Mock.Setup(lb => lb.Name).Returns("LB1");
        loadBalancer1Mock.Setup(lb => lb.StopAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stop failed"));

        var loadBalancer2Mock = new Mock<ILoadBalancer>();
        loadBalancer2Mock.Setup(lb => lb.Name).Returns("LB2");

        _loadBalancerFactoryMock.Setup(f => f.Create(It.Is<ListenerConfiguration>(c => c.Name == "LB1")))
            .Returns(loadBalancer1Mock.Object);
        _loadBalancerFactoryMock.Setup(f => f.Create(It.Is<ListenerConfiguration>(c => c.Name == "LB2")))
            .Returns(loadBalancer2Mock.Object);

        _manager.Initialize();
        await _manager.StartAllAsync();

        
        await _manager.StopAllAsync();

        // Both should be called even though one fails
        loadBalancer1Mock.Verify(
            lb => lb.StopAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        loadBalancer2Mock.Verify(
            lb => lb.StopAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
