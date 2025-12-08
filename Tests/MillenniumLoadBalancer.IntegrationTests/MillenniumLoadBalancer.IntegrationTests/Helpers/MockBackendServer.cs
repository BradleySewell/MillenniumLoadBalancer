using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MillenniumLoadBalancer.IntegrationTests.Helpers;

/// <summary>
/// A mock TCP server that can be used as a backend for load balancer testing.
/// </summary>
public class MockBackendServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task? _acceptTask;
    private readonly List<TcpClient> _connectedClients = new();
    private readonly object _lock = new();
    private int _connectionCount = 0;
    private readonly List<string> _receivedMessages = new();
    private readonly object _messagesLock = new();

    public string Address { get; }
    public int Port { get; private set; }
    public int ConnectionCount
    {
        get
        {
            lock (_lock)
            {
                return _connectionCount;
            }
        }
    }

    public IReadOnlyList<string> ReceivedMessages
    {
        get
        {
            lock (_messagesLock)
            {
                return _receivedMessages.ToList();
            }
        }
    }

    public bool IsRunning { get; private set; }

    public MockBackendServer(string address = "127.0.0.1", int port = 0)
    {
        Address = address;
        _listener = new TcpListener(IPAddress.Parse(address), port);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        IsRunning = true;
        _acceptTask = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));
    }

    public async Task StopAsync()
    {
        IsRunning = false;
        _cancellationTokenSource.Cancel();
        _listener.Stop();

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        lock (_lock)
        {
            foreach (var client in _connectedClients)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    // Ignore errors when closing
                }
            }
            _connectedClients.Clear();
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _connectedClients.Add(client);
            _connectionCount++;
        }

        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[4096];

                while (!cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                        break;

                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    lock (_messagesLock)
                    {
                        _receivedMessages.Add(message);
                    }

                    // Echo the message back
                    await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception)
        {
            // Connection closed or error
        }
        finally
        {
            lock (_lock)
            {
                _connectedClients.Remove(client);
            }
        }
    }

    public void Dispose()
    {
        StopAsync().Wait(TimeSpan.FromSeconds(5));
        _cancellationTokenSource.Dispose();
        _listener.Stop();
    }
}
