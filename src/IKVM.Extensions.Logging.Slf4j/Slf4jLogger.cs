using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using org.slf4j.helpers;

namespace IKVM.Extensions.Logging.Slf4j
{

    /// <summary>
    /// An slf4j logger that forwards to the <see cref="ILogger"/> of the same name, taken from whatever
    /// <see cref="ILoggerFactory"/> is installed on <see cref="Slf4jBridge"/> at the moment of the call.
    /// </summary>
    /// <remarks>
    /// Derives from <see cref="LegacyAbstractLogger"/>, which implements the forty-odd overloads slf4j's
    /// <c>Logger</c> declares and funnels all of them into a single call with the arguments normalized and any
    /// trailing throwable already separated out.
    /// </remarks>
    public sealed class Slf4jLogger : LegacyAbstractLogger
    {

        // Resolving through the factory on every call would be correct but takes a lock inside
        // LoggerFactory, and isDebugEnabled() sits in loops that Java libraries run per row. So the ILogger is
        // kept next to the factory it came from, and a different factory -- an Install, or an Uninstall --
        // is what invalidates it. The two fields are not read as a pair atomically, which is why the factory
        // is written second: a torn read costs one redundant CreateLogger and never a stale logger.
        ILoggerFactory? resolvedFrom;
        ILogger? resolved;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="name">slf4j logger name, which is also the <see cref="ILogger"/> category</param>
        internal Slf4jLogger(string name)
        {
            // AbstractLogger keeps the name in a protected field and returns it from getName()
            this.name = name;
        }

        ILogger? Logger
        {
            get
            {
                var current = Slf4jBridge.Factory;
                if (current is null)
                    return null;

                if (ReferenceEquals(current, resolvedFrom))
                    return resolved;

                var logger = current.CreateLogger(name);
                resolved = logger;
                resolvedFrom = current;
                return logger;
            }
        }

        bool IsEnabled(LogLevel level) => Logger is { } logger && logger.IsEnabled(level);

        /// <inheritdoc />
        public override bool isTraceEnabled() => IsEnabled(LogLevel.Trace);

        /// <inheritdoc />
        public override bool isDebugEnabled() => IsEnabled(LogLevel.Debug);

        /// <inheritdoc />
        public override bool isInfoEnabled() => IsEnabled(LogLevel.Information);

        /// <inheritdoc />
        public override bool isWarnEnabled() => IsEnabled(LogLevel.Warning);

        /// <inheritdoc />
        public override bool isErrorEnabled() => IsEnabled(LogLevel.Error);

        /// <summary>
        /// Name of the class whose frame slf4j would treat as the caller. Nothing here walks the stack, so
        /// there is none to report.
        /// </summary>
        /// <returns></returns>
        protected override string? getFullyQualifiedCallerName() => null;

        /// <summary>
        /// The single call every <c>Logger</c> overload funnels into.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="marker"></param>
        /// <param name="messagePattern">message, with <c>{}</c> where the arguments go</param>
        /// <param name="arguments">arguments for the anchors, already trimmed of any trailing throwable</param>
        /// <param name="throwable">
        /// the thrown exception, which IKVM presents as a <see cref="Exception"/> because that is what
        /// <c>java.lang.Throwable</c> derives from
        /// </param>
        protected override void handleNormalizedLoggingCall(org.slf4j.@event.Level level, org.slf4j.Marker marker, string messagePattern, object[] arguments, Exception throwable)
        {
            if (Logger is not { } logger)
                return;

            var logLevel = ToLogLevel(level);
            if (logger.IsEnabled(logLevel) == false)
                return;

            // basicArrayFormat is slf4j's own {} substitution, so a message renders exactly as it would under
            // any other provider. With no arguments there is nothing to substitute and the pattern is the
            // message, braces and all.
            var message = arguments is { Length: > 0 } ? MessageFormatter.basicArrayFormat(messagePattern, arguments) : messagePattern;

            // The message goes through as the state and is rendered verbatim rather than as a format string:
            // it has already been substituted, and what is left that looks like a template hole is text.
            using (BeginMdcScope(logger))
                logger.Log(logLevel, default, message, throwable, static (state, exception) => state);
        }

        /// <summary>
        /// Attaches whatever is in the MDC to the entry as a scope, so a provider that renders scopes shows the
        /// same context slf4j would.
        /// </summary>
        /// <param name="logger"></param>
        /// <returns>the scope, or <c>null</c> when the MDC is empty, which is the common case</returns>
        static IDisposable? BeginMdcScope(ILogger logger)
        {
            var context = org.slf4j.MDC.getCopyOfContextMap();
            if (context is null || context.isEmpty())
                return null;

            var state = new List<KeyValuePair<string, object?>>(context.size());
            var entries = context.entrySet().iterator();
            while (entries.hasNext())
            {
                var entry = (java.util.Map.Entry)entries.next();
                state.Add(new KeyValuePair<string, object?>((string)entry.getKey(), entry.getValue()));
            }

            return logger.BeginScope(state);
        }

        /// <summary>
        /// Maps an slf4j level onto the <see cref="LogLevel"/> that means the same thing. The two scales agree
        /// rank for rank, which is the whole of it.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        static LogLevel ToLogLevel(org.slf4j.@event.Level level) => level.toInt() switch
        {
            org.slf4j.@event.EventConstants.ERROR_INT => LogLevel.Error,
            org.slf4j.@event.EventConstants.WARN_INT => LogLevel.Warning,
            org.slf4j.@event.EventConstants.INFO_INT => LogLevel.Information,
            org.slf4j.@event.EventConstants.DEBUG_INT => LogLevel.Debug,
            _ => LogLevel.Trace,
        };

    }

}
