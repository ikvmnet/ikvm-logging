using System;

using java.util.logging;

using Microsoft.Extensions.Logging;

namespace IKVM.Extensions.Logging.Java.Util
{

    /// <summary>
    /// A <see cref="Handler"/> that forwards the records raised by <c>java.util.logging</c> to an
    /// <see cref="ILoggerFactory"/>, so that Java code running under IKVM logs through the same pipeline as
    /// the rest of the application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attach one to the logger whose subtree should be forwarded, which is usually the root:
    /// </para>
    /// <code>
    /// var root = LogManager.getLogManager().getLogger("");
    /// foreach (var handler in root.getHandlers())
    ///     root.removeHandler(handler);
    ///
    /// root.addHandler(new LoggerHandler(loggerFactory));
    /// root.setLevel(Level.ALL);
    /// </code>
    /// <para>
    /// A logger filters on its own level before any handler is reached, so a subtree left at its default
    /// <c>INFO</c> forwards nothing finer no matter what the <see cref="ILoggerFactory"/> would accept. Set
    /// the level on the Java side to whatever the finest <see cref="ILogger"/> rule is, and let the
    /// <see cref="ILoggerFactory"/> do the filtering from there.
    /// </para>
    /// </remarks>
    public class LoggerHandler : Handler
    {

        /// <summary>
        /// Name given to the <see cref="ILogger"/> for a record carrying no logger name of its own, which is
        /// what an anonymous logger produces.
        /// </summary>
        public const string DefaultLoggerName = "java.util.logging";

        // Localizing the message against the record's resource bundle and substituting its parameters is
        // Formatter.formatMessage's job, and SimpleFormatter inherits that method unchanged. None of
        // SimpleFormatter's own format is used: the timestamp, level and source it would prepend are exactly
        // what Microsoft.Extensions.Logging adds for itself.
        static readonly Formatter defaultFormatter = new SimpleFormatter();

        readonly ILoggerFactory factory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="factory">factory the forwarded records are logged through</param>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <c>null</c></exception>
        public LoggerHandler(ILoggerFactory factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Forwards <paramref name="record"/> to the <see cref="ILogger"/> named after the Java logger that
        /// raised it.
        /// </summary>
        /// <param name="record"></param>
        public override void publish(LogRecord record)
        {
            // the handler's own level and filter, and false as well once close has been called
            if (isLoggable(record) == false)
                return;

            try
            {
                var level = ToLogLevel(record.getLevel());
                if (level == LogLevel.None)
                    return;

                var name = record.getLoggerName();
                if (string.IsNullOrEmpty(name))
                    name = DefaultLoggerName;

                var logger = factory.CreateLogger(name);
                if (logger.IsEnabled(level) == false)
                    return;

                var message = (getFormatter() ?? defaultFormatter).formatMessage(record);

                // The message goes through as the state and is rendered verbatim. It has already been through
                // parameter substitution, so anything left in it that looks like a template hole is text that
                // a format string would either drop or throw on.
                logger.Log(level, default, message, record.getThrown(), static (state, exception) => state);
            }
            catch (Exception e)
            {
                // A handler that throws propagates into whatever Java code raised the record. The contract is
                // to hand the failure to the ErrorManager instead, which by default writes it to stderr once.
                // reportError takes a java.lang.Exception and a CLR one is not convertible to it, so the cast
                // carries a Java exception through and the message carries everything else.
                reportError(e.ToString(), e as java.lang.Exception, ErrorManager.WRITE_FAILURE);
            }
        }

        /// <summary>
        /// Does nothing. Records are handed to the <see cref="ILoggerFactory"/> as they arrive, and whether
        /// anything is buffered past that point is the logging provider's business, not this handler's.
        /// </summary>
        public override void flush()
        {

        }

        /// <summary>
        /// Stops forwarding. The <see cref="ILoggerFactory"/> belongs to the caller and is left open; all this
        /// does is drop the records that arrive after it, as the <see cref="Handler"/> contract requires of a
        /// handler that has been closed.
        /// </summary>
        public override void close()
        {
            // Handler.isLoggable returns false for every record once the level is OFF, which is the whole of
            // what closing means here and needs no state of its own.
            setLevel(Level.OFF);
        }

        /// <summary>
        /// Maps a <c>java.util.logging</c> level onto the closest <see cref="LogLevel"/>.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        /// <remarks>
        /// Compared by value rather than by identity so that a custom level sits wherever its severity puts
        /// it, the way java.util.logging itself orders levels.
        /// </remarks>
        static LogLevel ToLogLevel(Level level) => level.intValue() switch
        {
            int.MaxValue => LogLevel.None,  // OFF
            >= 1000 => LogLevel.Error,      // SEVERE
            >= 900 => LogLevel.Warning,     // WARNING
            >= 700 => LogLevel.Information, // INFO, CONFIG
            >= 500 => LogLevel.Debug,       // FINE
            _ => LogLevel.Trace,            // FINER, FINEST, ALL
        };

    }

}
