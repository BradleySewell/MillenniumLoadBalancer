using MillenniumLoadBalancer.App.Core.Interfaces;

namespace MillenniumLoadBalancer.App.Core;

internal class BackendService : IBackendService
{
    private volatile bool _isHealthy = false;
    private DateTime? _lastFailure;
    private readonly object _lock = new();

    public string Address { get; }
    public int Port { get; }
    public bool EnableTls { get; }
    public bool ValidateCertificate { get; }
    
    public bool IsHealthy => _isHealthy;
    
    public DateTime? LastFailure
    {
        get
        {
            lock (_lock)
            {
                return _lastFailure;
            }
        }
    }

    public BackendService(string address, int port, bool enableTls = false, bool validateCertificate = true)
    {
        Address = address;
        Port = port;
        EnableTls = enableTls;
        ValidateCertificate = validateCertificate;
    }

    public void MarkUnhealthy()
    {
        lock (_lock)
        {
            _isHealthy = false;
            _lastFailure = DateTime.UtcNow;
        }
    }

    public void MarkHealthy()
    {
        lock (_lock)
        {
            _isHealthy = true;
            _lastFailure = null;
        }
    }
}

