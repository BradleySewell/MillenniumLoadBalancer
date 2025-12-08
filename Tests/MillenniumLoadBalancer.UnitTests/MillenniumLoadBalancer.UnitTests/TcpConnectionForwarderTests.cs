using MillenniumLoadBalancer.App.Core.Interfaces;
using MillenniumLoadBalancer.App.Infrastructure;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class TcpConnectionForwarderTests
{
    [TestMethod]
    public async Task ForwardAsync_WithReachableBackend_ReturnsTrue()
    {
        
        var forwarder = new TcpConnectionForwarder(
            connectionTimeoutSeconds: 5,
            sendTimeoutSeconds: 1,
            receiveTimeoutSeconds: 1);

        var backend = new Mock<IBackendService>();
        
        // Start a simple TCP server for the backend
        using var backendListener = new TcpListener(IPAddress.Loopback, 0);
        backendListener.Start();
        var backendEndpoint = (IPEndPoint)backendListener.LocalEndpoint;
        
        backend.Setup(b => b.Address).Returns(backendEndpoint.Address.ToString());
        backend.Setup(b => b.Port).Returns(backendEndpoint.Port);

        // Accept connection from forwarder in background and close it after a short delay
        var acceptTask = Task.Run(async () =>
        {
            using var backendClient = await backendListener.AcceptTcpClientAsync();
            // Close after a short delay to allow the forward to complete
            await Task.Delay(50);
            backendClient.Close();
        });

        // Create a client stream - use a simple TcpClient connected to a server
        using var clientListener = new TcpListener(IPAddress.Loopback, 0);
        clientListener.Start();
        var clientEndpoint = (IPEndPoint)clientListener.LocalEndpoint;
        
        using var client = new TcpClient();
        await client.ConnectAsync(clientEndpoint.Address, clientEndpoint.Port);
        using var clientStream = client.GetStream();
        
        // Accept client connection and close it after a short delay
        var clientAcceptTask = Task.Run(async () =>
        {
            using var serverClient = await clientListener.AcceptTcpClientAsync();
            await Task.Delay(50);
            serverClient.Close();
        });

        
        var result = await forwarder.ForwardAsync(clientStream, backend.Object);

        
        // The forward should have successfully connected
        Assert.IsTrue(result);
        
        // Wait for cleanup tasks
        await Task.WhenAny(acceptTask, Task.Delay(200));
        await Task.WhenAny(clientAcceptTask, Task.Delay(200));
    }

    [TestMethod]
    public async Task ForwardAsync_WithUnreachableBackend_ReturnsFalse()
    {
        
        var forwarder = new TcpConnectionForwarder(
            connectionTimeoutSeconds: 1,
            sendTimeoutSeconds: 5,
            receiveTimeoutSeconds: 5);

        var backend = new Mock<IBackendService>();
        backend.Setup(b => b.Address).Returns("127.0.0.1");
        backend.Setup(b => b.Port).Returns(99999); // Port that's not listening

        // Create a valid client stream by connecting to a simple server
        using var clientListener = new TcpListener(IPAddress.Loopback, 0);
        clientListener.Start();
        var clientEndpoint = (IPEndPoint)clientListener.LocalEndpoint;
        
        using var client = new TcpClient();
        await client.ConnectAsync(clientEndpoint.Address, clientEndpoint.Port);
        using var clientStream = client.GetStream();
        
        // Accept the connection (but we don't need to do anything with it)
        _ = Task.Run(async () =>
        {
            using var serverClient = await clientListener.AcceptTcpClientAsync();
            // Just accept and hold the connection
        });

        
        var result = await forwarder.ForwardAsync(clientStream, backend.Object);

        
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ForwardAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        
        var forwarder = new TcpConnectionForwarder(
            connectionTimeoutSeconds: 5,
            sendTimeoutSeconds: 5,
            receiveTimeoutSeconds: 5);

        var backend = new Mock<IBackendService>();
        backend.Setup(b => b.Address).Returns("127.0.0.1");
        backend.Setup(b => b.Port).Returns(8080);

        // Create a valid client stream by connecting to a simple server
        using var clientListener = new TcpListener(IPAddress.Loopback, 0);
        clientListener.Start();
        var clientEndpoint = (IPEndPoint)clientListener.LocalEndpoint;
        
        using var client = new TcpClient();
        await client.ConnectAsync(clientEndpoint.Address, clientEndpoint.Port);
        using var clientStream = client.GetStream();
        
        // Accept the connection (but we don't need to do anything with it)
        _ = Task.Run(async () =>
        {
            using var serverClient = await clientListener.AcceptTcpClientAsync();
            // Just accept and hold the connection
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => forwarder.ForwardAsync(clientStream, backend.Object, cts.Token));
    }

    [TestMethod]
    public async Task ForwardAsync_WithConnectionTimeout_ReturnsFalse()
    {
        
        var forwarder = new TcpConnectionForwarder(
            connectionTimeoutSeconds: 1,
            sendTimeoutSeconds: 5,
            receiveTimeoutSeconds: 5);

        var backend = new Mock<IBackendService>();
        backend.Setup(b => b.Address).Returns("192.168.255.255");
        backend.Setup(b => b.Port).Returns(8080);

        // Create a valid client stream by connecting to a simple server
        using var clientListener = new TcpListener(IPAddress.Loopback, 0);
        clientListener.Start();
        var clientEndpoint = (IPEndPoint)clientListener.LocalEndpoint;
        
        using var client = new TcpClient();
        await client.ConnectAsync(clientEndpoint.Address, clientEndpoint.Port);
        using var clientStream = client.GetStream();
        
        // Accept the connection (but we don't need to do anything with it)
        _ = Task.Run(async () =>
        {
            using var serverClient = await clientListener.AcceptTcpClientAsync();
            // Just accept and hold the connection
        });

        
        var result = await forwarder.ForwardAsync(clientStream, backend.Object);

        
        Assert.IsFalse(result);
    }
}
