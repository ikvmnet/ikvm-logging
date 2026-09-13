using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace IKVM.Extensions.Logging.TestUtilities;

/// <summary>
/// One call to <see cref="ILogger.Log"/> as <see cref="CapturingLoggerFactory"/> saw it.
/// </summary>
/// <param name="Category">name of the logger the call arrived on</param>
/// <param name="Level"></param>
/// <param name="Message">what the state rendered to</param>
/// <param name="Exception"></param>
/// <param name="Scopes">the scopes open at the time, outermost first</param>
public sealed record CapturedLog(string Category, LogLevel Level, string Message, Exception? Exception, IReadOnlyList<object> Scopes)
{

    /// <summary>
    /// Looks up <paramref name="key"/> in the open scopes, which is where a bridge puts the Java-side
    /// diagnostic context.
    /// </summary>
    /// <param name="key"></param>
    /// <returns>the value, or <c>null</c> when no scope carries it</returns>
    public object? Scope(string key) => Scopes
        .OfType<IEnumerable<KeyValuePair<string, object?>>>()
        .SelectMany(static pairs => pairs)
        .Where(pair => pair.Key == key)
        .Select(static pair => pair.Value)
        .FirstOrDefault();

}

/// <summary>
/// An <see cref="ILoggerFactory"/> that keeps what was logged instead of writing it anywhere, and which can
/// be held at a minimum level so that a bridge's <see cref="ILogger.IsEnabled"/> check has something to
/// refuse.
/// </summary>
public sealed class CapturingLoggerFactory : ILoggerFactory
{

    readonly List<object> scopes = [];

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

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            factory.scopes.Add(state);
            return new Scope(factory, state);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= factory.MinimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                factory.Logs.Add(new CapturedLog(category, logLevel, formatter(state, exception), exception, [.. factory.scopes]));
        }

        sealed class Scope(CapturingLoggerFactory factory, object state) : IDisposable
        {

            public void Dispose() => factory.scopes.Remove(state);

        }

    }

}
