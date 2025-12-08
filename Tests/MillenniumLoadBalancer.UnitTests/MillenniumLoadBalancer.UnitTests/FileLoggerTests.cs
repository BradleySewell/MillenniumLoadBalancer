using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Infrastructure.Logging;
using System.Text;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class FileLoggerTests
{
    private string _testLogFilePath = null!;
    private object _lockObject = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _testLogFilePath = Path.Combine(Path.GetTempPath(), $"test-log-{Guid.NewGuid()}.log");
        _lockObject = new object();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (File.Exists(_testLogFilePath))
        {
            File.Delete(_testLogFilePath);
        }
    }

    [TestMethod]
    public void IsEnabled_WithLogLevelAboveMinimum_ReturnsTrue()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Information, _lockObject);

        
        Assert.IsTrue(logger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Warning));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Error));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Critical));
    }

    [TestMethod]
    public void IsEnabled_WithLogLevelBelowMinimum_ReturnsFalse()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Warning, _lockObject);

        
        Assert.IsFalse(logger.IsEnabled(LogLevel.Trace));
        Assert.IsFalse(logger.IsEnabled(LogLevel.Debug));
        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Warning));
    }

    [TestMethod]
    public void Log_WritesMessageToFile()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Information, _lockObject);
        var message = "Test log message";

        
        logger.Log(LogLevel.Information, 0, message, null, (state, ex) => state?.ToString() ?? string.Empty);

        
        Assert.IsTrue(File.Exists(_testLogFilePath));
        var content = File.ReadAllText(_testLogFilePath);
        Assert.Contains(message, content);
    }

    [TestMethod]
    public void Log_WithException_WritesExceptionToFile()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Error, _lockObject);
        var exception = new InvalidOperationException("Test exception");

        
        logger.Log(LogLevel.Error, 0, "Error occurred", exception, (state, ex) => state?.ToString() ?? string.Empty);

        
        Assert.IsTrue(File.Exists(_testLogFilePath));
        var content = File.ReadAllText(_testLogFilePath);
        Assert.Contains("Error occurred", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("Test exception", content);
    }

    [TestMethod]
    public void Log_WithLogLevelBelowMinimum_DoesNotWriteToFile()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Warning, _lockObject);

        
        logger.Log(LogLevel.Information, 0, "Should not log", null, (state, ex) => state?.ToString() ?? string.Empty);

        
        if (File.Exists(_testLogFilePath))
        {
            var content = File.ReadAllText(_testLogFilePath);
            Assert.DoesNotContain("Should not log", content);
        }
    }

    [TestMethod]
    public void Log_IncludesTimestamp()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Information, _lockObject);
        var beforeLog = DateTime.Now;

        
        logger.Log(LogLevel.Information, 0, "Test", null, (state, ex) => state?.ToString() ?? string.Empty);
        var afterLog = DateTime.Now;

        
        var content = File.ReadAllText(_testLogFilePath);
        // Check for timestamp format: yyyy-MM-dd HH:mm:ss
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(content, @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}"));
    }

    [TestMethod]
    public void Log_IncludesLogLevel()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Information, _lockObject);

        
        logger.Log(LogLevel.Warning, 0, "Test", null, (state, ex) => state?.ToString() ?? string.Empty);

        
        var content = File.ReadAllText(_testLogFilePath);
        Assert.Contains("[WARN ]", content);
    }

    [TestMethod]
    public void BeginScope_ReturnsNull()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Information, _lockObject);

        
        var scope = logger.BeginScope("Test scope");

        
        Assert.IsNull(scope);
    }

    [TestMethod]
    public void Log_IsThreadSafe()
    {
        
        var logger = new FileLogger(_testLogFilePath, LogLevel.Information, _lockObject);

        // Log from multiple threads
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                logger.Log(LogLevel.Information, 0, $"Message {i}", null, (state, ex) => state?.ToString() ?? string.Empty);
            }))
            .ToArray();

        Task.WaitAll(tasks);

        // File should exist and contain all messages
        Assert.IsTrue(File.Exists(_testLogFilePath));
        var content = File.ReadAllText(_testLogFilePath);
        for (int i = 0; i < 100; i++)
        {
            Assert.Contains($"Message {i}", content, $"Message {i} not found in log");
        }
    }
}
