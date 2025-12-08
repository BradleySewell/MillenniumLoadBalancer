using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;
using Moq;
using System.Net;
using System.Net.Sockets;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class LoadBalancerTests : IDisposable
{
    private Mock<ILoadBalancingStrategy> _strategyMock = null!;
    private Mock<IConnectionForwarder> _forwarderMock = null!;
    private Mock<IBackendHealthCheckService> _healthCheckServiceMock = null!;
    private Mock<ILogger<LoadBalancer>> _loggerMock = null!;
    private List<IBackendService> _backends = null!;
    private LoadBalancer _loadBalancer = null!;
    private int _listenPort = 0;

    [TestInitialize]
    public void TestInitialize()
    {
        _strategyMock = new Mock<ILoadBalancingStrategy>();
        _forwarderMock = new Mock<IConnectionForwarder>();
        _healthCheckServiceMock = new Mock<IBackendHealthCheckService>();
        _loggerMock = new Mock<ILogger<LoadBalancer>>();

        // Find an available port
        using var tempListener = new TcpListener(IPAddress.Loopback, 0);
        tempListener.Start();
        _listenPort = ((IPEndPoint)tempListener.LocalEndpoint).Port;
        tempListener.Stop();

        _backends = new List<IBackendService>
        {
            new BackendService("127.0.0.1", 9000),
            new BackendService("127.0.0.1", 9001)
        };

        _loadBalancer = new LoadBalancer(
            "TestLB",
            "127.0.0.1",
            _listenPort,
            _backends,
            _strategyMock.Object,
            _forwarderMock.Object,
            _healthCheckServiceMock.Object,
            recoveryCheckIntervalSeconds: 1,
            recoveryDelaySeconds: 1,
            _loggerMock.Object);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _loadBalancer?.Dispose();
    }

    public void Dispose()
    {
        TestCleanup();
    }

    [TestMethod]
    public void Name_ReturnsConfiguredName()
    {
        
        Assert.AreEqual("TestLB", _loadBalancer.Name);
    }

    [TestMethod]
    public async Task StartAsync_StartsListener()
    {
        
        await _loadBalancer.StartAsync();
        await Task.Delay(100); // Give it time to start

        // Try to connect to verify listener is running
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync("127.0.0.1", _listenPort);
            Assert.IsTrue(client.Connected);
        }
        finally
        {
            await _loadBalancer.StopAsync();
        }
    }

    [TestMethod]
    public async Task StartAsync_WithInvalidAddress_ThrowsInvalidOperationException()
    {
        
        var invalidLB = new LoadBalancer(
            "TestLB",
            "invalid-ip",
            _listenPort,
            _backends,
            _strategyMock.Object,
            _forwarderMock.Object,
            _healthCheckServiceMock.Object,
            recoveryCheckIntervalSeconds: 1,
            recoveryDelaySeconds: 1,
            _loggerMock.Object);

        try
        {
            
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => invalidLB.StartAsync());
        }
        finally
        {
            invalidLB.Dispose();
        }
    }

    [TestMethod]
    public async Task StopAsync_StopsListener()
    {
        
        await _loadBalancer.StartAsync();
        await Task.Delay(100);

        
        await _loadBalancer.StopAsync();
        await Task.Delay(100);

        //Try to connect should fail
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync("127.0.0.1", _listenPort);
            Assert.Fail("Connection should have failed");
        }
        catch (SocketException)
        {
            // Expected - listener is stopped
        }
    }

    [TestMethod]
    public async Task StartAsync_ThenStopAsync_CanBeRestarted()
    {
        
        await _loadBalancer.StartAsync();
        await Task.Delay(100);
        await _loadBalancer.StopAsync();
        await Task.Delay(100);
        await _loadBalancer.StartAsync();
        await Task.Delay(100);

        // Should be able to connect
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync("127.0.0.1", _listenPort);
            Assert.IsTrue(client.Connected);
        }
        finally
        {
            await _loadBalancer.StopAsync();
        }
    }

    [TestMethod]
    public async Task Dispose_StopsListener()
    {
        
        await _loadBalancer.StartAsync();
        await Task.Delay(100);

        
        _loadBalancer.Dispose();
        await Task.Delay(100);

        
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync("127.0.0.1", _listenPort);
            Assert.Fail("Connection should have failed");
        }
        catch (SocketException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task StartAsync_StartsRecoveryCheckLoop()
    {
        
        // Find a unique port for this test
        int testPort;
        using (var tempListener = new TcpListener(IPAddress.Loopback, 0))
        {
            tempListener.Start();
            testPort = ((IPEndPoint)tempListener.LocalEndpoint).Port;
            tempListener.Stop();
        }

        var unhealthyBackend = new BackendService("127.0.0.1", 9999);
        unhealthyBackend.MarkUnhealthy();
        unhealthyBackend.MarkUnhealthy(); // Set LastFailure

        _healthCheckServiceMock.Setup(h => h.CheckHealthAsync(
            It.IsAny<IBackendService>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var backendsWithUnhealthy = new List<IBackendService> { unhealthyBackend };
        var lb = new LoadBalancer(
            "TestLB",
            "127.0.0.1",
            testPort,
            backendsWithUnhealthy,
            _strategyMock.Object,
            _forwarderMock.Object,
            _healthCheckServiceMock.Object,
            recoveryCheckIntervalSeconds: 1,
            recoveryDelaySeconds: 0, // No delay for testing
            _loggerMock.Object);

        try
        {
            
            await lb.StartAsync();
            await Task.Delay(1500); // Wait for recovery check to run

            
            _healthCheckServiceMock.Verify(
                h => h.CheckHealthAsync(
                    It.IsAny<IBackendService>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }
        finally
        {
            await lb.StopAsync();
            lb.Dispose();
        }
    }
}
