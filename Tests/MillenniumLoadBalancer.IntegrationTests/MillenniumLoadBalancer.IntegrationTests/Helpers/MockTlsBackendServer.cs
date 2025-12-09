using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MillenniumLoadBalancer.IntegrationTests.Helpers;

/// <summary>
/// A mock TLS/HTTPS server that can be used as a backend for load balancer testing.
/// </summary>
public class MockTlsBackendServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Task? _acceptTask;
    private readonly List<TcpClient> _connectedClients = new();
    private readonly object _lock = new();
    private int _connectionCount = 0;
    private readonly List<string> _receivedMessages = new();
    private readonly object _messagesLock = new();
    private readonly X509Certificate2 _serverCertificate;

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

    public MockTlsBackendServer(string address = "127.0.0.1", int port = 0)
    {
        Address = address;
        _listener = new TcpListener(IPAddress.Parse(address), port);
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Create a self-signed certificate for testing
        _serverCertificate = CreateSelfSignedCertificate(address);
    }

    private static X509Certificate2 CreateSelfSignedCertificate(string hostname)
    {
        // For testing purposes, we'll create a simple certificate
        // In a real scenario, you'd use a proper certificate
        // This is a simplified approach for integration tests
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            $"CN={hostname}",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        
        request.CertificateExtensions.Add(
            new System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
                System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature |
                System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment,
                false));
        
        request.CertificateExtensions.Add(
            new System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension(
                new System.Security.Cryptography.OidCollection
                {
                    new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1") // Server Authentication
                },
                false));
        
        var sanBuilder = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(hostname);
        sanBuilder.AddIpAddress(IPAddress.Parse(hostname == "127.0.0.1" ? "127.0.0.1" : hostname));
        request.CertificateExtensions.Add(sanBuilder.Build());
        
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));
        
        var pfxBytes = certificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx);
        return System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(pfxBytes, null);
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
                
                // Wrap in SSL stream
                using var sslStream = new SslStream(stream, false);
                await sslStream.AuthenticateAsServerAsync(
                    _serverCertificate,
                    false,
                    System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                    false);
                
                var buffer = new byte[4096];

                while (!cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await sslStream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                        break;

                    var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    lock (_messagesLock)
                    {
                        _receivedMessages.Add(message);
                    }

                    // Echo the message back
                    await sslStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
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
        _serverCertificate.Dispose();
    }
}

