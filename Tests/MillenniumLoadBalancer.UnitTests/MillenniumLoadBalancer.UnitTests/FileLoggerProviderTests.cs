using Microsoft.Extensions.Logging;
using MillenniumLoadBalancer.App.Infrastructure.Logging;

namespace MillenniumLoadBalancer.UnitTests;

[TestClass]
public sealed class FileLoggerProviderTests
{
    private string _testLogFilePath = null!;
    private string _testLogDirectory = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), $"test-logs-{Guid.NewGuid()}");
        _testLogFilePath = Path.Combine(_testLogDirectory, "test.log");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_testLogDirectory))
        {
            Directory.Delete(_testLogDirectory, true);
        }
    }

    [TestMethod]
    public void Constructor_CreatesDirectoryIfNotExists()
    {

        var provider = new FileLoggerProvider(_testLogFilePath, LogLevel.Information);

        
        Assert.IsTrue(Directory.Exists(_testLogDirectory));
    }

    [TestMethod]
    public void CreateLogger_ReturnsFileLogger()
    {
        
        var provider = new FileLoggerProvider(_testLogFilePath, LogLevel.Information);

        
        var logger = provider.CreateLogger("TestCategory");

        
        Assert.IsNotNull(logger);
        Assert.IsInstanceOfType(logger, typeof(ILogger));
    }

    [TestMethod]
    public void CreateLogger_WithSameCategory_ReturnsSameInstance()
    {
        
        var provider = new FileLoggerProvider(_testLogFilePath, LogLevel.Information);

        
        var logger1 = provider.CreateLogger("TestCategory");
        var logger2 = provider.CreateLogger("TestCategory");

        
        Assert.AreSame(logger1, logger2);
    }

    [TestMethod]
    public void CreateLogger_WithDifferentCategories_ReturnsDifferentInstances()
    {
        
        var provider = new FileLoggerProvider(_testLogFilePath, LogLevel.Information);

        
        var logger1 = provider.CreateLogger("Category1");
        var logger2 = provider.CreateLogger("Category2");

        
        Assert.AreNotSame(logger1, logger2);
    }

    [TestMethod]
    public void CreateLogger_RespectsMinimumLogLevel()
    {
        
        var provider = new FileLoggerProvider(_testLogFilePath, LogLevel.Warning);

        
        var logger = provider.CreateLogger("TestCategory");

        
        Assert.IsFalse(logger.IsEnabled(LogLevel.Information));
        Assert.IsTrue(logger.IsEnabled(LogLevel.Warning));
    }

    [TestMethod]
    public void Dispose_ClearsLoggers()
    {
        
        var provider = new FileLoggerProvider(_testLogFilePath, LogLevel.Information);
        var logger1 = provider.CreateLogger("Category1");
        var logger2 = provider.CreateLogger("Category2");

        
        provider.Dispose();

        // After dispose, we can't verify internal state, but it should not throw
        // and we can create new loggers
        var newProvider = new FileLoggerProvider(_testLogFilePath, LogLevel.Information);
        var newLogger = newProvider.CreateLogger("Category1");
        Assert.IsNotNull(newLogger);
    }

    [TestMethod]
    public void CreateLogger_WithDefaultLogLevel_UsesInformation()
    {
        
        var provider = new FileLoggerProvider(_testLogFilePath);

        
        var logger = provider.CreateLogger("TestCategory");

        
        Assert.IsTrue(logger.IsEnabled(LogLevel.Information));
        Assert.IsFalse(logger.IsEnabled(LogLevel.Debug));
    }
}
