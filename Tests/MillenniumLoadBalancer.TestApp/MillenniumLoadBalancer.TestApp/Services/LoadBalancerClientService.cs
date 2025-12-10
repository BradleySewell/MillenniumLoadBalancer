using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace MillenniumLoadBalancer.TestApp.Services;

public class LoadBalancerClientService
{
    private readonly TraceService? _traceService;

    public LoadBalancerClientService(TraceService? traceService = null)
    {
        _traceService = traceService;
    }

    public async Task<string> SendRequestAsync(string address, int port, string message, bool enableTls = false)
    {
        _traceService?.AddTrace($"Connecting to load balancer: {address}:{port} (TLS: {enableTls})");
        using var client = new TcpClient();
        await client.ConnectAsync(address, port);
        _traceService?.AddTrace($"Connected to load balancer: {address}:{port}");
        
        var baseStream = client.GetStream();
        Stream stream;
        
        // If TLS is enabled, perform TLS handshake
        // Note: With TLS passthrough, the TLS handshake happens between client and backend.
        // The client connects to the load balancer, but the TLS handshake uses the backend's hostname.
        // For testing with self-signed certs, we accept any certificate.
        if (enableTls)
        {
            var sslStream = new SslStream(baseStream, false, (sender, certificate, chain, sslPolicyErrors) => true, null);
            try
            {
                // Use the address for SNI, but accept any certificate (for self-signed test certs)
                await sslStream.AuthenticateAsClientAsync(address);
                stream = sslStream;
                _traceService?.AddTrace($"TLS handshake completed with load balancer (passthrough to backend)");
            }
            catch (Exception ex)
            {
                _traceService?.AddTrace($"TLS handshake failed: {ex.Message}");
                sslStream.Dispose();
                throw;
            }
        }
        else
        {
            stream = baseStream;
        }
        
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _traceService?.AddTrace($"Sending request to load balancer: {message}");
        await stream.WriteAsync(messageBytes);
        
        // Shutdown the send side to signal we're done sending
        // This allows the backend to detect we're done and close its side
        if (!enableTls)
        {
            client.Client.Shutdown(SocketShutdown.Send);
        }

        // Read the response (read once - backend typically sends response in one go)
        var buffer = new byte[4096];
        var bytesRead = await stream.ReadAsync(buffer);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        _traceService?.AddTrace($"Received response from load balancer: {response}");

        // Dispose SSL stream if it was created
        if (enableTls && stream is SslStream ssl)
        {
            ssl.Dispose();
        }

        return response;
    }
}
