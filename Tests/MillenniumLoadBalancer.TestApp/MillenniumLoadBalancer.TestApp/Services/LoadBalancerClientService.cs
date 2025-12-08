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

    public async Task<string> SendRequestAsync(string address, int port, string message)
    {
        _traceService?.AddTrace($"Connecting to load balancer: {address}:{port}");
        using var client = new TcpClient();
        await client.ConnectAsync(address, port);
        _traceService?.AddTrace($"Connected to load balancer: {address}:{port}");
        
        var stream = client.GetStream();
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _traceService?.AddTrace($"Sending request to load balancer: {message}");
        await stream.WriteAsync(messageBytes);

        var buffer = new byte[4096];
        var bytesRead = await stream.ReadAsync(buffer);
        var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        _traceService?.AddTrace($"Received response from load balancer: {response}");

        return response;
    }
}
