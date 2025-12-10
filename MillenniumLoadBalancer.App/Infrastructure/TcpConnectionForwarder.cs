using System.Net.Sockets;
using MillenniumLoadBalancer.App.Core.Interfaces;

namespace MillenniumLoadBalancer.App.Infrastructure;

internal class TcpConnectionForwarder : IConnectionForwarder
{
    private readonly int _connectionTimeoutSeconds;
    private readonly int _sendTimeoutSeconds;
    private readonly int _receiveTimeoutSeconds;

    public TcpConnectionForwarder(
        int connectionTimeoutSeconds,
        int sendTimeoutSeconds,
        int receiveTimeoutSeconds)
    {
        _connectionTimeoutSeconds = connectionTimeoutSeconds;
        _sendTimeoutSeconds = sendTimeoutSeconds;
        _receiveTimeoutSeconds = receiveTimeoutSeconds;
    }

    public async Task<bool> ForwardAsync(Stream clientStream, IBackendService backend, CancellationToken cancellationToken = default)
    {
        // Note: This forwarder uses TCP passthrough, so it forwards all bytes transparently.
        // If EnableTls is true, the TLS handshake happens directly between client and backend.
        // The backend.EnableTls and backend.ValidateCertificate settings are only used by
        // the health check service, not by this forwarder.
        
        cancellationToken.ThrowIfCancellationRequested();
        
        using var backendClient = new System.Net.Sockets.TcpClient();
        
        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectionCts.CancelAfter(TimeSpan.FromSeconds(_connectionTimeoutSeconds));
        
        try
        {
            await backendClient.ConnectAsync(backend.Address, backend.Port, connectionCts.Token);
        }

        catch
        {
            return false;
        }
        
        NetworkStream backendStream;
        try
        {
            backendStream = backendClient.GetStream();
        }
        catch
        {
            return false;
        }
        
        using (backendStream)
        {
            backendStream.WriteTimeout = _sendTimeoutSeconds * 1000;
            backendStream.ReadTimeout = _receiveTimeoutSeconds * 1000;
            
            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(_sendTimeoutSeconds, _receiveTimeoutSeconds)));
            
            try
            {
                var forwardTask = clientStream.CopyToAsync(backendStream, operationCts.Token);
                var reverseTask = backendStream.CopyToAsync(clientStream, operationCts.Token);

                var completedTask = await Task.WhenAny(forwardTask, reverseTask);
                
                try
                {
                    await completedTask;
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }
                
                cancellationToken.ThrowIfCancellationRequested();
                
                // When one direction completes (one side closed), wait for the other with a short timeout
                // instead of the full operation timeout. If one side closed, the other should close soon after.
                var otherTask = completedTask == forwardTask ? reverseTask : forwardTask;
                
                try
                {
                    // Wait up to 2 seconds for the other direction to complete
                    // This is much shorter than the 30-second timeout and prevents hanging
                    await otherTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (TimeoutException)
                {
                    // One side closed, other side didn't close within 2 seconds - consider connection done
                    // This is normal when one side closes and the other doesn't immediately respond
                }
                catch
                {
                    // Ignore other exceptions (stream closed, etc.)
                }
                
                return true;
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
            catch (IOException ex)
            {
                throw new IOException($"Network error during data transfer to/from {backend.Address}:{backend.Port}", ex);
            }
            catch (Exception ex) when (!(ex is IOException))
            {
                throw new IOException($"Unexpected error during data transfer to/from {backend.Address}:{backend.Port}", ex);
            }
        }
    }
}

