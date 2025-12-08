using Microsoft.Extensions.Logging;

namespace MillenniumLoadBalancer.App.Infrastructure.Logging;

/// <summary>
/// Logger implementation that writes log entries to a file.
/// </summary>
internal class FileLogger : ILogger
{
    private readonly string _logFilePath;
    private readonly LogLevel _minLevel;
    private readonly object _lockObject;

    public FileLogger(string logFilePath, LogLevel minLevel, object lockObject)
    {
        _logFilePath = logFilePath;
        _minLevel = minLevel;
        _lockObject = lockObject;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logLevelString = logLevel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            LogLevel.None => "NONE ",
            _ => logLevel.ToString().ToUpperInvariant().PadRight(5)
        };

        var logEntry = $"{timestamp} [{logLevelString}] {message}";

        if (exception != null)
        {
            logEntry += Environment.NewLine + exception.ToString();
        }

        lock (_lockObject)
        {
            try
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Silently fail if we can't write to the log file
            }
        }
    }
}
