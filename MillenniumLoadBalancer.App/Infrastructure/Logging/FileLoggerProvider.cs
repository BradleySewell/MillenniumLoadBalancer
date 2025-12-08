using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MillenniumLoadBalancer.App.Infrastructure.Logging;

internal class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;
    private readonly LogLevel _minLevel;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _lockObject = new();

    public FileLoggerProvider(string logFilePath, LogLevel minLevel = LogLevel.Information)
    {
        _logFilePath = logFilePath;
        _minLevel = minLevel;

        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (UnauthorizedAccessException ex)
            {
            }
            catch
            {
            }
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(_logFilePath, _minLevel, _lockObject));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}
