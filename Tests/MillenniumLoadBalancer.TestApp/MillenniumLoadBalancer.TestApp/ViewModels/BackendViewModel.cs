using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MillenniumLoadBalancer.TestApp.ViewModels;

public class BackendViewModel : INotifyPropertyChanged
{
    private string _address = "127.0.0.1";
    private int _port;
    private bool _isRunning;
    private int _connectionCount;
    private string _status = "Stopped";
    private string _response = "";
    private int _responseDelay = 0;
    private bool _enableTls = false;

    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); }
    }

    public int Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); Status = value ? "Running" : "Stopped"; }
    }

    public int ConnectionCount
    {
        get => _connectionCount;
        set { _connectionCount = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string Response
    {
        get => _response;
        set { _response = value; OnPropertyChanged(); }
    }

    public int ResponseDelay
    {
        get => _responseDelay;
        set { _responseDelay = value; OnPropertyChanged(); }
    }

    public bool EnableTls
    {
        get => _enableTls;
        set { _enableTls = value; OnPropertyChanged(); }
    }

    public string DisplayName => $"{Address}:{Port}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
