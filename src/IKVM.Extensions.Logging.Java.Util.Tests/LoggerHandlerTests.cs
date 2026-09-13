using System;

using IKVM.Extensions.Logging.TestUtilities;

using java.util.logging;

using Microsoft.Extensions.Logging;

using Xunit;

namespace IKVM.Extensions.Logging.Java.Util.Tests;

/// <summary>
/// Covers what <see cref="LoggerHandler"/> does with a record: which <see cref="ILogger"/> it picks, which
/// <see cref="LogLevel"/> a java.util.logging level becomes, what the message and the thrown exception look
/// like on the other side, and which records never get that far.
/// </summary>
public class LoggerHandlerTests
{

    const string LoggerName = "org.example.Thing";

    readonly CapturingLoggerFactory factory = new();

    static LogRecord Record(Level level, string message, string? loggerName = LoggerName)
    {
        var record = new LogRecord(level, message);
        if (loggerName != null)
            record.setLoggerName(loggerName);

        return record;
    }

    [Fact]
    public void Constructor_NullFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LoggerHandler(null!));
    }

    [Fact]
    public void Publish_NamesTheLoggerAfterTheJavaLogger()
    {
        new LoggerHandler(factory).publish(Record(Level.INFO, "planner ran"));

        var log = Assert.Single(factory.Logs);
        Assert.Equal(LoggerName, log.Category);
        Assert.Equal("planner ran", log.Message);
        Assert.Null(log.Exception);
    }

    [Fact]
    public void Publish_NoLoggerName_UsesTheDefaultName()
    {
        new LoggerHandler(factory).publish(Record(Level.INFO, "from an anonymous logger", loggerName: null));

        Assert.Equal(LoggerHandler.DefaultLoggerName, Assert.Single(factory.Logs).Category);
    }

    [Theory]
    [InlineData("SEVERE", LogLevel.Error)]
    [InlineData("WARNING", LogLevel.Warning)]
    [InlineData("INFO", LogLevel.Information)]
    [InlineData("CONFIG", LogLevel.Information)]
    [InlineData("FINE", LogLevel.Debug)]
    [InlineData("FINER", LogLevel.Trace)]
    [InlineData("FINEST", LogLevel.Trace)]
    public void Publish_MapsTheLevel(string levelName, LogLevel expected)
    {
        new LoggerHandler(factory).publish(Record(Level.parse(levelName), "message"));

        Assert.Equal(expected, Assert.Single(factory.Logs).Level);
    }

    [Fact]
    public void Publish_LevelOff_IsDropped()
    {
        // OFF is not a severity a record can be logged at, whatever a caller may have constructed
        new LoggerHandler(factory).publish(Record(Level.OFF, "message"));

        Assert.Empty(factory.Logs);
    }

    [Fact]
    public void Publish_CustomLevel_IsPlacedByItsValue()
    {
        // between FINE (500) and INFO (800), so it belongs with CONFIG (700)
        new LoggerHandler(factory).publish(Record(Level.parse("750"), "message"));

        Assert.Equal(LogLevel.Information, Assert.Single(factory.Logs).Level);
    }

    [Fact]
    public void Publish_SubstitutesTheParameters()
    {
        var record = Record(Level.INFO, "planner chose {0} over {1}");
        record.setParameters(["EnumerableConvention", "BindableConvention"]);

        new LoggerHandler(factory).publish(record);

        Assert.Equal("planner chose EnumerableConvention over BindableConvention", Assert.Single(factory.Logs).Message);
    }

    [Fact]
    public void Publish_MessageWithNoParameters_IsLeftAlone()
    {
        // a lone brace is text, not a hole to fill, and must not be treated as a format string
        new LoggerHandler(factory).publish(Record(Level.INFO, "rel#12:LogicalFilter{condition=$0 > 5}"));

        Assert.Equal("rel#12:LogicalFilter{condition=$0 > 5}", Assert.Single(factory.Logs).Message);
    }

    [Fact]
    public void Publish_ForwardsTheThrownException()
    {
        var thrown = new java.lang.IllegalStateException("unable to implement");
        var record = Record(Level.SEVERE, "planning failed");
        record.setThrown(thrown);

        new LoggerHandler(factory).publish(record);

        var log = Assert.Single(factory.Logs);
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Same(thrown, log.Exception);
    }

    [Fact]
    public void Publish_BelowTheHandlerLevel_IsDropped()
    {
        var handler = new LoggerHandler(factory);
        handler.setLevel(Level.WARNING);

        handler.publish(Record(Level.INFO, "message"));

        Assert.Empty(factory.Logs);
    }

    [Fact]
    public void Publish_RejectedByTheHandlerFilter_IsDropped()
    {
        var handler = new LoggerHandler(factory);
        handler.setFilter(new DenyAll());

        handler.publish(Record(Level.SEVERE, "message"));

        Assert.Empty(factory.Logs);
    }

    [Fact]
    public void Publish_LevelTheFactoryRefuses_IsDropped()
    {
        factory.MinimumLevel = LogLevel.Warning;

        new LoggerHandler(factory).publish(Record(Level.INFO, "message"));

        Assert.Empty(factory.Logs);
    }

    [Fact]
    public void Publish_AfterClose_IsDropped()
    {
        var handler = new LoggerHandler(factory);
        handler.close();

        handler.publish(Record(Level.SEVERE, "message"));

        Assert.Empty(factory.Logs);
    }

    [Fact]
    public void Publish_FactoryThrows_DoesNotReachTheCaller()
    {
        // the Java code that raised the record must not be taken down by the logging pipeline behind it
        var handler = new LoggerHandler(new ThrowingLoggerFactory());

        handler.publish(Record(Level.SEVERE, "message"));
    }

    [Fact]
    public void Publish_FromAJavaLogger_ReachesTheFactory()
    {
        var handler = new LoggerHandler(factory);
        var logger = Logger.getLogger("org.example.EndToEnd");
        logger.setUseParentHandlers(false);
        logger.setLevel(Level.ALL);
        logger.addHandler(handler);

        try
        {
            logger.fine("the finest detail");
            logger.log(Level.WARNING, "there were {0} rows", "no");
        }
        finally
        {
            logger.removeHandler(handler);
        }

        Assert.Collection(
            factory.Logs,
            log =>
            {
                Assert.Equal("org.example.EndToEnd", log.Category);
                Assert.Equal(LogLevel.Debug, log.Level);
                Assert.Equal("the finest detail", log.Message);
            },
            log =>
            {
                Assert.Equal("org.example.EndToEnd", log.Category);
                Assert.Equal(LogLevel.Warning, log.Level);
                Assert.Equal("there were no rows", log.Message);
            });
    }

    sealed class DenyAll : Filter
    {

        public bool isLoggable(LogRecord record) => false;

    }

    sealed class ThrowingLoggerFactory : ILoggerFactory
    {

        public ILogger CreateLogger(string categoryName) => throw new InvalidOperationException("the provider is broken");

        public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

        public void Dispose()
        {

        }

    }

}
