using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MillenniumLoadBalancer.TestApp.ViewModels;

public class RequestViewModel : INotifyPropertyChanged
{
    private string _message = "";
    private string _response = "";
    private string _status = RequestStatus.Pending;
    private DateTime? _sentTime;
    private DateTime? _receivedTime;

    public string Message
    {
        get => _message;
        set { _message = value; OnPropertyChanged(); }
    }

    public string Response
    {
        get => _response;
        set { _response = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public DateTime? SentTime
    {
        get => _sentTime;
        set 
        { 
            _sentTime = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(ResponseTime));
        }
    }

    public DateTime? ReceivedTime
    {
        get => _receivedTime;
        set 
        { 
            _receivedTime = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(ResponseTime));
        }
    }

    public TimeSpan? ResponseTime => SentTime.HasValue && ReceivedTime.HasValue 
        ? ReceivedTime.Value - SentTime.Value 
        : null;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
