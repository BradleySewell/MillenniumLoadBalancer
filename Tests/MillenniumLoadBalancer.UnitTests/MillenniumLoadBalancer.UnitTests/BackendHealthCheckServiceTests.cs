using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;
using Moq;
using System.Net;
using System.Net.Sockets;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class BackendHealthCheckServiceTests
{
    private Mock<ILogger<BackendHealthCheckService>> _loggerMock = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _loggerMock = new Mock<ILogger<BackendHealthCheckService>>();
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithReachableBackend_ReturnsTrue()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        // Start a simple TCP listener on a random port
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        
        backend.Setup(b => b.Address).Returns(endpoint.Address.ToString());
        backend.Setup(b => b.Port).Returns(endpoint.Port);

        
        var result = await service.CheckHealthAsync(backend.Object, timeoutSeconds: 5);

        
        Assert.IsTrue(result);
        
        listener.Stop();
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithUnreachableBackend_ReturnsFalse()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        // Use a port that's unlikely to be listening (but valid)
        backend.Setup(b => b.Address).Returns("127.0.0.1");
        backend.Setup(b => b.Port).Returns(65535); // High port that's likely not in use

        
        var result = await service.CheckHealthAsync(backend.Object, timeoutSeconds: 1);

        
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithInvalidAddress_ReturnsFalse()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        backend.Setup(b => b.Address).Returns("192.168.255.255");
        backend.Setup(b => b.Port).Returns(8080);

        
        var result = await service.CheckHealthAsync(backend.Object, timeoutSeconds: 1);

        
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithTimeout_ReturnsFalse()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        backend.Setup(b => b.Address).Returns("192.168.255.255");
        backend.Setup(b => b.Port).Returns(8080);

        
        var result = await service.CheckHealthAsync(backend.Object, timeoutSeconds: 1);

        
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CheckHealthAsync_WhenCancelled_ReturnsFalse()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        backend.Setup(b => b.Address).Returns("127.0.0.1");
        backend.Setup(b => b.Port).Returns(8080);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        
        var result = await service.CheckHealthAsync(backend.Object, timeoutSeconds: 5, cts.Token);

        
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CheckHealthAsync_LogsDebug_WhenCheckFails()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        backend.Setup(b => b.Address).Returns("127.0.0.1");
        backend.Setup(b => b.Port).Returns(99999); // Invalid port

        
        await service.CheckHealthAsync(backend.Object, timeoutSeconds: 1);

        
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithTlsEnabled_ButNonTlsBackend_ReturnsFalse()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        // Start a simple TCP listener (non-TLS)
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        
        backend.Setup(b => b.Address).Returns(endpoint.Address.ToString());
        backend.Setup(b => b.Port).Returns(endpoint.Port);
        backend.Setup(b => b.EnableTls).Returns(true);
        backend.Setup(b => b.ValidateCertificate).Returns(false);

        
        var result = await service.CheckHealthAsync(backend.Object, timeoutSeconds: 5);

        
        // TLS handshake should fail on a non-TLS server
        Assert.IsFalse(result);
        
        listener.Stop();
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithTlsDisabled_DoesNotPerformTlsHandshake()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        // Start a simple TCP listener
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        
        backend.Setup(b => b.Address).Returns(endpoint.Address.ToString());
        backend.Setup(b => b.Port).Returns(endpoint.Port);
        backend.Setup(b => b.EnableTls).Returns(false);
        backend.Setup(b => b.ValidateCertificate).Returns(true);

        
        var result = await service.CheckHealthAsync(backend.Object, timeoutSeconds: 5);

        
        // Should succeed with just TCP check, no TLS handshake
        Assert.IsTrue(result);
        
        listener.Stop();
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithTlsEnabled_AndUnreachableBackend_ReturnsFalse()
    {
        
        var service = new BackendHealthCheckService(_loggerMock.Object);
        var backend = new Mock<IBackendService>();
        
        backend.Setup(b => b.Address).Returns("127.0.0.1");
        backend.Setup(b => b.Port).Returns(65535); // Unreachable port
        backend.Setup(b => b.EnableTls).Returns(true);
        backend.Setup(b => b.ValidateCertificate).Returns(true);

        
        var result = await service.CheckHealthAsync(backend.Object, timeoutSeconds: 1);

        
        // Should fail at TCP connection, before TLS handshake
        Assert.IsFalse(result);
    }
}
