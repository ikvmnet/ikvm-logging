using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace IKVM.Extensions.Logging.Java.Util.Tests;

/// <summary>
/// One call to <see cref="ILogger.Log"/> as <see cref="CapturingLoggerFactory"/> saw it.
/// </summary>
/// <param name="Category">name of the logger the call arrived on</param>
/// <param name="Level"></param>
/// <param name="Message">what the state rendered to</param>
/// <param name="Exception"></param>
sealed record CapturedLog(string Category, LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// An <see cref="ILoggerFactory"/> that keeps what was logged instead of writing it anywhere, and which can
/// be held at a minimum level so that the handler's <see cref="ILogger.IsEnabled"/> check has something to
/// refuse.
/// </summary>
sealed class CapturingLoggerFactory : ILoggerFactory
{

    /// <summary>
    /// Every call that got past <see cref="MinimumLevel"/>, in the order it arrived.
    /// </summary>
    public List<CapturedLog> Logs { get; } = [];

    /// <summary>
    /// Level below which <see cref="ILogger.IsEnabled"/> answers <c>false</c>.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose()
    {

    }

    sealed class CapturingLogger(CapturingLoggerFactory factory, string category) : ILogger
    {

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= factory.MinimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                factory.Logs.Add(new CapturedLog(category, logLevel, formatter(state, exception), exception));
        }

    }

}
