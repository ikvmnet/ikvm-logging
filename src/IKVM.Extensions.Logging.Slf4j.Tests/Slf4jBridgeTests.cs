using System;
using System.Collections.Generic;

using IKVM.Extensions.Logging.TestUtilities;

using Microsoft.Extensions.Logging;

using Xunit;

namespace IKVM.Extensions.Logging.Slf4j.Tests;

/// <summary>
/// Covers the bridge from slf4j's side: that slf4j binds to it at all, that a record comes out on the
/// <see cref="ILogger"/> of the same name at the level that means the same thing, and — the part the design
/// turns on — that a logger slf4j handed out before an <see cref="ILoggerFactory"/> existed starts working
/// the moment one is installed.
/// </summary>
/// <remarks>
/// Every test installs its own factory and takes it back down, because the bridge these run against is the
/// single process-wide one. Parallelization is off for the assembly.
/// </remarks>
public class Slf4jBridgeTests
{

    const string LoggerName = "org.apache.calcite.plan.hep.HepPlanner";

    readonly CapturingLoggerFactory factory = new();

    static org.slf4j.Logger Logger(string name = LoggerName) => org.slf4j.LoggerFactory.getLogger(name);

    [Fact]
    public void Slf4j_BindsToTheBridge()
    {
        // the failure this guards is silent: with no provider slf4j returns NOPLogger and every Java log
        // statement, and every diagnostic a library keeps behind a level check, quietly does nothing
        var bound = org.slf4j.LoggerFactory.getILoggerFactory();

        Assert.IsType<Slf4jLoggerFactory>(bound);
        Assert.IsNotType<org.slf4j.helpers.NOPLoggerFactory>(bound);
        Assert.True(Slf4jBridge.IsBound);
    }

    [Fact]
    public void ProviderClassName_NamesTheProvider()
    {
        // the system property carries this string and nothing checks it until slf4j tries to load it, so a
        // rename that got away from us would show up as a silent fall back to NOPLogger
        Assert.Equal("cli.IKVM.Extensions.Logging.Slf4j.Slf4jServiceProvider", Slf4jBridge.ProviderClassName);
        Assert.NotNull(java.lang.Class.forName(Slf4jBridge.ProviderClassName));
    }

    [Fact]
    public void Logger_IsNotANoOp()
    {
        Assert.IsNotType<org.slf4j.helpers.NOPLogger>(Logger());
    }

    [Fact]
    public void Logger_KeepsItsName()
    {
        Assert.Equal(LoggerName, Logger().getName());
    }

    [Fact]
    public void Log_NamesTheCategoryAfterTheSlf4jLogger()
    {
        using (Slf4jBridge.Install(factory))
            Logger().info("planner ran");

        var log = Assert.Single(factory.Logs);
        Assert.Equal(LoggerName, log.Category);
        Assert.Equal("planner ran", log.Message);
        Assert.Null(log.Exception);
    }

    [Fact]
    public void Log_MapsTheLevels()
    {
        using (Slf4jBridge.Install(factory))
        {
            var log = Logger();
            log.trace("t");
            log.debug("d");
            log.info("i");
            log.warn("w");
            log.error("e");
        }

        Assert.Equal(
            new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error },
            factory.Logs.ConvertAll(static l => l.Level));
    }

    [Fact]
    public void Log_SubstitutesTheAnchors()
    {
        using (Slf4jBridge.Install(factory))
            Logger().info("planner chose {} over {} after {} rules", "EnumerableConvention", "BindableConvention", 12);

        Assert.Equal("planner chose EnumerableConvention over BindableConvention after 12 rules", Assert.Single(factory.Logs).Message);
    }

    [Fact]
    public void Log_MessageWithNoArguments_IsLeftAlone()
    {
        // a brace in a plan dump is text, and must not be read as a hole to fill on either side of the bridge
        using (Slf4jBridge.Install(factory))
            Logger().info("rel#12:LogicalFilter{condition=$0 > 5}");

        Assert.Equal("rel#12:LogicalFilter{condition=$0 > 5}", Assert.Single(factory.Logs).Message);
    }

    [Fact]
    public void Log_ForwardsTheThrowable()
    {
        var thrown = new java.lang.IllegalStateException("unable to implement");

        using (Slf4jBridge.Install(factory))
            Logger().error("planning failed", thrown);

        var log = Assert.Single(factory.Logs);
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Equal("planning failed", log.Message);
        Assert.Same(thrown, log.Exception);
    }

    [Fact]
    public void Log_TrailingThrowableIsNotAnArgument()
    {
        var thrown = new java.lang.IllegalStateException("boom");

        using (Slf4jBridge.Install(factory))
            Logger().error("rule {} failed", "FilterJoinRule", thrown);

        var log = Assert.Single(factory.Logs);
        Assert.Equal("rule FilterJoinRule failed", log.Message);
        Assert.Same(thrown, log.Exception);
    }

    [Fact]
    public void IsEnabled_FollowsTheInstalledFactory()
    {
        var log = Logger();

        Assert.False(log.isErrorEnabled());

        using (Slf4jBridge.Install(factory))
        {
            factory.MinimumLevel = LogLevel.Information;

            Assert.False(log.isDebugEnabled());
            Assert.True(log.isInfoEnabled());
            Assert.True(log.isErrorEnabled());
        }

        Assert.False(log.isErrorEnabled());
    }

    [Fact]
    public void Log_BelowTheFactoryMinimum_IsDropped()
    {
        factory.MinimumLevel = LogLevel.Warning;

        using (Slf4jBridge.Install(factory))
            Logger().info("message");

        Assert.Empty(factory.Logs);
    }

    [Fact]
    public void Log_WithNothingInstalled_IsDropped()
    {
        Logger().error("nobody is listening");

        Assert.Empty(factory.Logs);
    }

    [Fact]
    public void Log_LoggerTakenBeforeInstall_StartsWorkingAfterIt()
    {
        // This is the whole point of resolving the factory per call. A Java library asks for its logger once,
        // into a static field, long before an application has built a container -- Calcite's
        // CalciteTrace.getPlannerTracer() is exactly this -- and a bridge that captured the factory when the
        // logger was made would leave that logger dead for the life of the process.
        var taken = Logger("org.example.TakenEarly");
        taken.info("before");

        Assert.Empty(factory.Logs);

        using (Slf4jBridge.Install(factory))
            taken.info("after");

        Assert.Equal("after", Assert.Single(factory.Logs).Message);
    }

    [Fact]
    public void Install_ReturnsToThePreviousFactoryOnDispose()
    {
        var outer = new CapturingLoggerFactory();

        using (Slf4jBridge.Install(outer))
        {
            using (Slf4jBridge.Install(factory))
                Logger().info("inner");

            Assert.Same(outer, Slf4jBridge.Factory);
            Logger().info("outer");
        }

        Assert.Null(Slf4jBridge.Factory);
        Assert.Equal("inner", Assert.Single(factory.Logs).Message);
        Assert.Equal("outer", Assert.Single(outer.Logs).Message);
    }

    [Fact]
    public void Install_NullFactory_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Slf4jBridge.Install(null!));
    }

    [Fact]
    public void Log_CarriesTheDiagnosticContextAsAScope()
    {
        using (Slf4jBridge.Install(factory))
        {
            org.slf4j.MDC.put("statement", "42");

            try
            {
                Logger().info("preparing");
            }
            finally
            {
                org.slf4j.MDC.clear();
            }
        }

        Assert.Equal("42", Assert.Single(factory.Logs).Scope("statement"));
    }

    [Fact]
    public void Log_WithAnEmptyDiagnosticContext_OpensNoScope()
    {
        using (Slf4jBridge.Install(factory))
            Logger().info("preparing");

        Assert.Empty(Assert.Single(factory.Logs).Scopes);
    }

    [Fact]
    public void LoggerFactory_ReturnsTheSameLoggerForAName()
    {
        // slf4j callers hold on to what they are given, so handing back a new object each time would leak one
        // per call and defeat the per-logger resolution cache
        Assert.Same(Logger("org.example.Stable"), Logger("org.example.Stable"));
    }

    [Fact]
    public void Register_LeavesAProviderChosenElsewhereAlone()
    {
        var chosen = java.lang.System.getProperty(Slf4jBridge.ProviderProperty);

        Assert.Equal(Slf4jBridge.ProviderClassName, chosen);

        java.lang.System.setProperty(Slf4jBridge.ProviderProperty, "com.example.OtherProvider");

        try
        {
            Slf4jBridge.Register();

            Assert.Equal("com.example.OtherProvider", java.lang.System.getProperty(Slf4jBridge.ProviderProperty));
        }
        finally
        {
            java.lang.System.setProperty(Slf4jBridge.ProviderProperty, chosen);
        }
    }

}
