using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MillenniumLoadBalancer.TestApp.Services;

public class TraceService : INotifyPropertyChanged
{
    private readonly ObservableCollection<string> _traceLines = new();
    private const int MaxTraceLines = 1000;

    public ObservableCollection<string> TraceLines => _traceLines;

    public void AddTrace(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var traceLine = $"[{timestamp}] {message}";
        
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _traceLines.Insert(0, traceLine);
                if (_traceLines.Count > MaxTraceLines)
                {
                    _traceLines.RemoveAt(_traceLines.Count - 1);
                }
                OnPropertyChanged(nameof(TraceLines));
            });
        }
        else
        {
            _traceLines.Insert(0, traceLine);
            if (_traceLines.Count > MaxTraceLines)
            {
                _traceLines.RemoveAt(_traceLines.Count - 1);
            }
            OnPropertyChanged(nameof(TraceLines));
        }
    }

    public void Clear()
    {
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _traceLines.Clear();
                OnPropertyChanged(nameof(TraceLines));
            });
        }
        else
        {
            _traceLines.Clear();
            OnPropertyChanged(nameof(TraceLines));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
