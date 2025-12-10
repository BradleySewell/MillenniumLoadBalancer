using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using MillenniumLoadBalancer.TestApp.Services;

namespace MillenniumLoadBalancer.TestApp.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly BackendSimulatorService _backendSimulator;
    private readonly LoadBalancerClientService _clientService;
    private readonly TraceService _traceService;
    private string _loadBalancerAddress = "127.0.0.1";
    private int _loadBalancerPort = 8080;
    private string _newBackendAddress = "127.0.0.1";
    private int _newBackendPort = 5001;
    private bool _newBackendEnableTls = false;
    private string _newBackendResponse = "";
    private int _newBackendResponseDelay = 0;
    private string _newRequestMessage = "";
    private bool _requestEnableTls = false;
    private int _bulkRequestCount = 10;

    public MainViewModel()
    {
        _traceService = new TraceService();
        _backendSimulator = new BackendSimulatorService(_traceService);
        _clientService = new LoadBalancerClientService(_traceService);
        
        Backends = new ObservableCollection<BackendViewModel>();
        Requests = new ObservableCollection<RequestViewModel>();

        _backendSimulator.BackendStatusChanged += OnBackendStatusChanged;
        
        _traceService.AddTrace("Application started");
    }

    public TraceService TraceService => _traceService;

    public ObservableCollection<BackendViewModel> Backends { get; }
    public ObservableCollection<RequestViewModel> Requests { get; }

    public string LoadBalancerAddress
    {
        get => _loadBalancerAddress;
        set { _loadBalancerAddress = value; OnPropertyChanged(); }
    }

    public int LoadBalancerPort
    {
        get => _loadBalancerPort;
        set { _loadBalancerPort = value; OnPropertyChanged(); }
    }

    public string NewBackendAddress
    {
        get => _newBackendAddress;
        set { _newBackendAddress = value; OnPropertyChanged(); }
    }

    public int NewBackendPort
    {
        get => _newBackendPort;
        set { _newBackendPort = value; OnPropertyChanged(); }
    }

    public bool NewBackendEnableTls
    {
        get => _newBackendEnableTls;
        set { _newBackendEnableTls = value; OnPropertyChanged(); }
    }

    public string NewBackendResponse
    {
        get => _newBackendResponse;
        set { _newBackendResponse = value; OnPropertyChanged(); }
    }

    public int NewBackendResponseDelay
    {
        get => _newBackendResponseDelay;
        set { _newBackendResponseDelay = value; OnPropertyChanged(); }
    }

    public string NewRequestMessage
    {
        get => _newRequestMessage;
        set { _newRequestMessage = value; OnPropertyChanged(); }
    }

    public bool RequestEnableTls
    {
        get => _requestEnableTls;
        set { _requestEnableTls = value; OnPropertyChanged(); }
    }

    public int BulkRequestCount
    {
        get => _bulkRequestCount;
        set { _bulkRequestCount = value; OnPropertyChanged(); }
    }

    public async Task AddBackendAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBackendAddress) || NewBackendPort <= 0)
            return;

        // Check if a backend with the same address and port already exists
        if (Backends.Any(b => b.Address == NewBackendAddress && b.Port == NewBackendPort))
        {
            // Backend already exists - could show a message here
            return;
        }

        var backend = new BackendViewModel
        {
            Address = NewBackendAddress,
            Port = NewBackendPort,
            EnableTls = NewBackendEnableTls,
            Response = NewBackendResponse,
            ResponseDelay = NewBackendResponseDelay
        };

        Backends.Add(backend);
        _traceService.AddTrace($"Adding backend: {backend.Address}:{backend.Port}");
        try
        {
        await _backendSimulator.StartBackendAsync(backend.Address, backend.Port, backend);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            // Port is already in use - error is already displayed in the backend status
            // Optionally show a message box here if desired
        }
        catch (Exception)
        {
            // Other errors - error is already displayed in the backend status
            // Optionally show a message box here if desired
        }
    }

    public async Task RemoveBackendAsync(BackendViewModel backend)
    {
        if (backend == null) return;

        _traceService.AddTrace($"Removing backend: {backend.Address}:{backend.Port}");
        await _backendSimulator.StopBackendAsync(backend.Address, backend.Port);
        Backends.Remove(backend);
    }

    public void RemoveRequest(RequestViewModel request)
    {
        if (request == null) return;
        Requests.Remove(request);
    }

    public async Task ToggleBackendAsync(BackendViewModel backend)
    {
        if (backend == null) return;

        if (backend.IsRunning)
        {
            _traceService.AddTrace($"Stopping backend: {backend.Address}:{backend.Port}");
            await _backendSimulator.StopBackendAsync(backend.Address, backend.Port);
        }
        else
        {
            _traceService.AddTrace($"Starting backend: {backend.Address}:{backend.Port}");
            try
        {
            await _backendSimulator.StartBackendAsync(backend.Address, backend.Port, backend);
        }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                _traceService.AddTrace($"Failed to start backend {backend.Address}:{backend.Port} - Port already in use");
                // Port is already in use - error is already displayed in the backend status
            }
            catch (Exception ex)
            {
                _traceService.AddTrace($"Failed to start backend {backend.Address}:{backend.Port} - Error: {ex.Message}");
                // Other errors - error is already displayed in the backend status
            }
        }
    }

    public void AddRequest(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _traceService.AddTrace($"Adding request to queue: {message}");
        var request = new RequestViewModel
        {
            Message = message,
            Status = RequestStatus.Pending
        };
        Requests.Add(request);
    }

    public void AddBulkRequests()
    {
        if (_bulkRequestCount <= 0)
            return;

        _traceService.AddTrace($"Adding {_bulkRequestCount} numbered requests to queue");
        
        for (int i = 1; i <= _bulkRequestCount; i++)
        {
            var request = new RequestViewModel
            {
                Message = i.ToString(),
                Status = RequestStatus.Pending
            };
            Requests.Add(request);
        }
        
        _traceService.AddTrace($"Added {_bulkRequestCount} numbered requests (1-{_bulkRequestCount})");
    }

    public async Task SendRequestAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var request = new RequestViewModel
        {
            Message = message,
            Status = RequestStatus.Sending
        };
        Requests.Add(request);

        try
        {
            request.SentTime = DateTime.Now;
            var response = await _clientService.SendRequestAsync(LoadBalancerAddress, LoadBalancerPort, message, RequestEnableTls);
            request.ReceivedTime = DateTime.Now;
            request.Response = response;
            request.Status = RequestStatus.Success;
        }
        catch (Exception ex)
        {
            request.ReceivedTime = DateTime.Now;
            request.Response = $"Error: {ex.Message}";
            request.Status = RequestStatus.Failed;
        }
    }

    public async Task SendAllRequestsAsync()
    {
        var allRequests = Requests
            .Where(r => !string.IsNullOrWhiteSpace(r.Message))
            .ToList();

        if (allRequests.Count == 0)
            return;

        // Reset all non-pending requests to Pending status (for resending)
        var requestsToReset = allRequests.Where(r => r.Status != RequestStatus.Pending).ToList();
        foreach (var request in requestsToReset)
        {
            request.Status = RequestStatus.Pending;
            request.SentTime = null;
            request.ReceivedTime = null;
            request.Response = "";
        }

        var pendingCount = allRequests.Count(r => r.Status == RequestStatus.Pending);
        var resendCount = requestsToReset.Count;
        
        if (resendCount > 0)
        {
            _traceService.AddTrace($"Sending all requests ({pendingCount} pending, {resendCount} being resent)");
        }
        else
        {
            _traceService.AddTrace($"Sending all pending requests ({pendingCount} requests)");
        }
        
        // Send all requests concurrently
        var tasks = allRequests.Select(request => SendExistingRequestAsync(request));
        await Task.WhenAll(tasks);
        
        _traceService.AddTrace($"Finished sending all requests");
    }

    public void ClearAllRequests()
    {
        var count = Requests.Count;
        Requests.Clear();
        _traceService.AddTrace($"Cleared all requests ({count} requests removed)");
    }

    private async Task SendExistingRequestAsync(RequestViewModel request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
            return;

        request.Status = RequestStatus.Sending;
        _traceService.AddTrace($"Sending request: {request.Message}");

        try
        {
            request.SentTime = DateTime.Now;
            var response = await _clientService.SendRequestAsync(LoadBalancerAddress, LoadBalancerPort, request.Message, RequestEnableTls);
            request.ReceivedTime = DateTime.Now;
            request.Response = response;
            request.Status = RequestStatus.Success;
            _traceService.AddTrace($"Request completed successfully: {request.Message} -> {response}");
        }
        catch (Exception ex)
        {
            request.ReceivedTime = DateTime.Now;
            request.Response = $"Error: {ex.Message}";
            request.Status = RequestStatus.Failed;
            _traceService.AddTrace($"Request failed: {request.Message} - Error: {ex.Message}");
        }
    }

    private void OnBackendStatusChanged(string address, int port, bool isRunning, int connectionCount)
    {
        var backend = Backends.FirstOrDefault(b => b.Address == address && b.Port == port);
        if (backend != null)
        {
            backend.IsRunning = isRunning;
            backend.ConnectionCount = connectionCount;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
