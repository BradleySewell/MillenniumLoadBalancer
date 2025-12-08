using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using MillenniumLoadBalancer.TestApp.ViewModels;

namespace MillenniumLoadBalancer.TestApp.Services;

public class BackendSimulatorService
{
    private readonly ConcurrentDictionary<string, BackendServer> _servers = new();
    private readonly TraceService? _traceService;

    public event Action<string, int, bool, int>? BackendStatusChanged;

    public BackendSimulatorService(TraceService? traceService = null)
    {
        _traceService = traceService;
    }

    public async Task StartBackendAsync(string address, int port, BackendViewModel viewModel)
    {
        var key = $"{address}:{port}";
        
        if (_servers.ContainsKey(key))
        {
            await StopBackendAsync(address, port);
        }

        var server = new BackendServer(address, port, viewModel, _traceService);
        
        try
        {
            await server.StartAsync();
        _servers[key] = server;
            
            _traceService?.AddTrace($"Backend started: {address}:{server.Port}");
        
            // Update view model with actual port and running status (in case port 0 was used for auto-assignment)
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                viewModel.Port = server.Port;
                    viewModel.IsRunning = true;
                    viewModel.Status = "Running";
                });
            }
            else
            {
                viewModel.Port = server.Port;
                viewModel.IsRunning = true;
                viewModel.Status = "Running";
        }
        
        // Start status update task
        _ = Task.Run(async () => await UpdateStatusAsync(server, viewModel));
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            // Port is already in use
            server.Dispose();
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    viewModel.IsRunning = false;
                    viewModel.Status = $"Error: Port {port} is already in use";
                });
            }
            else
            {
                viewModel.IsRunning = false;
                viewModel.Status = $"Error: Port {port} is already in use";
            }
            throw; // Re-throw so caller can handle it
        }
        catch (Exception ex)
        {
            // Other errors
            server.Dispose();
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    viewModel.IsRunning = false;
                    viewModel.Status = $"Error: {ex.Message}";
                });
            }
            else
            {
                viewModel.IsRunning = false;
                viewModel.Status = $"Error: {ex.Message}";
            }
            throw; // Re-throw so caller can handle it
        }
    }

    public async Task StopBackendAsync(string address, int port)
    {
        var key = $"{address}:{port}";
        if (_servers.TryRemove(key, out var server))
        {
            await server.StopAsync();
            _traceService?.AddTrace($"Backend stopped: {address}:{port}");
            
            // Notify that the backend has stopped
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    BackendStatusChanged?.Invoke(address, port, false, 0);
                });
            }
            else
            {
                BackendStatusChanged?.Invoke(address, port, false, 0);
            }
            
            server.Dispose();
        }
    }

    private async Task UpdateStatusAsync(BackendServer server, BackendViewModel viewModel)
    {
        while (server.IsRunning)
        {
            var connectionCount = server.ConnectionCount;
            var isRunning = server.IsRunning;
            
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    viewModel.IsRunning = isRunning;
                    viewModel.ConnectionCount = connectionCount;
                });
            }
            else
            {
                BackendStatusChanged?.Invoke(server.Address, server.Port, isRunning, connectionCount);
            }
            
            await Task.Delay(500);
        }
    }

    public void Dispose()
    {
        foreach (var server in _servers.Values)
        {
            server.StopAsync().Wait(TimeSpan.FromSeconds(2));
            server.Dispose();
        }
        _servers.Clear();
    }

    private class BackendServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _acceptTask;
        private readonly List<TcpClient> _connectedClients = new();
        private readonly object _lock = new();
        private int _connectionCount = 0;
        private readonly BackendViewModel _viewModel;
            private readonly TraceService? _traceService;

        public string Address { get; }
        public int Port { get; private set; }
        public bool IsRunning { get; private set; }
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

            public BackendServer(string address, int port, BackendViewModel viewModel, TraceService? traceService)
        {
            Address = address;
            _viewModel = viewModel;
                _traceService = traceService;
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
                _connectionCount = 0;
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
                        _traceService?.AddTrace($"Backend {Address}:{Port} - Connection accepted");
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

                        var receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        _traceService?.AddTrace($"Backend {Address}:{Port} - Received: {receivedMessage}");

                        // Get the configured response and delay
                        string responseToSend;
                        int delayMs;
                        if (Application.Current?.Dispatcher != null)
                        {
                            var result = Application.Current.Dispatcher.Invoke(() => 
                                new 
                                { 
                                    Response = string.IsNullOrWhiteSpace(_viewModel.Response) 
                                        ? receivedMessage 
                                        : _viewModel.Response,
                                    Delay = _viewModel.ResponseDelay
                                });
                            responseToSend = result.Response;
                            delayMs = result.Delay;
                        }
                        else
                        {
                            responseToSend = string.IsNullOrWhiteSpace(_viewModel.Response) 
                                ? receivedMessage 
                                : _viewModel.Response;
                            delayMs = _viewModel.ResponseDelay;
                        }
                        
                        // Apply response delay if configured
                        if (delayMs > 0)
                        {
                            _traceService?.AddTrace($"Backend {Address}:{Port} - Delaying response by {delayMs}ms");
                            await Task.Delay(delayMs, cancellationToken);
                        }
                        
                        _traceService?.AddTrace($"Backend {Address}:{Port} - Sending: {responseToSend}");
                        var responseBytes = Encoding.UTF8.GetBytes(responseToSend);
                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
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
                    _connectionCount--;
                    _traceService?.AddTrace($"Backend {Address}:{Port} - Connection closed. Active connections: {_connectionCount}");
                }
            }
        }

        public void Dispose()
        {
            StopAsync().Wait(TimeSpan.FromSeconds(2));
            _cancellationTokenSource.Dispose();
            _listener.Stop();
        }
    }
}
