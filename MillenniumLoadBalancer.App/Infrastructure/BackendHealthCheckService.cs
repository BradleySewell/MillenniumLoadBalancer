using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Core.Interfaces;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace MillenniumLoadBalancer.App.Infrastructure;

internal class BackendHealthCheckService : IBackendHealthCheckService
{
    private readonly ILogger<BackendHealthCheckService> _logger;

    public BackendHealthCheckService(ILogger<BackendHealthCheckService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CheckHealthAsync(IBackendService backend, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            using var testClient = new TcpClient();
            await testClient.ConnectAsync(backend.Address, backend.Port, connectCts.Token);

            if (!testClient.Connected)
            {
                _logger.LogDebug($"Backend {backend.Address}:{backend.Port} connection not established");
                return false;
            }

            // If TLS is enabled, perform a TLS handshake to verify the backend can handle HTTPS
            if (backend.EnableTls)
            {
                try
                {
                    var stream = testClient.GetStream();
                    using var sslStream = new SslStream(stream, false, 
                        backend.ValidateCertificate ? ValidateServerCertificate : AcceptAnyCertificate, 
                        null);
                    
                    // Perform TLS handshake with timeout
                    // Note: AuthenticateAsClientAsync doesn't support CancellationToken directly,
                    // so we use Task.WaitAsync to add timeout support
                    using var sslCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    sslCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                    
                    await sslStream.AuthenticateAsClientAsync(
                        backend.Address, 
                        null, 
                        System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                        checkCertificateRevocation: backend.ValidateCertificate)
                        .WaitAsync(sslCts.Token);
                    
                    _logger.LogDebug($"Backend {backend.Address}:{backend.Port} TLS handshake successful");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Backend {backend.Address}:{backend.Port} TLS handshake failed");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            _logger.LogDebug(ex, $"Backend {backend.Address}:{backend.Port} health check failed");

            return false;
        }
    }

    private static bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
    {
        // Return true if there are no policy errors
        return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
    }

    private static bool AcceptAnyCertificate(object sender, X509Certificate? certificate, X509Chain? chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
    {
        // Accept any certificate (useful for self-signed certificates in development)
        return true;
    }
}

