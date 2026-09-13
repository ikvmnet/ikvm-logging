using System.Collections.Concurrent;

namespace IKVM.Extensions.Logging.Slf4j
{

    /// <summary>
    /// slf4j's factory side of the bridge. Hands out one <see cref="Slf4jLogger"/> per name and keeps it.
    /// </summary>
    /// <remarks>
    /// The loggers are cached for the life of the process because that is what slf4j callers assume — a Java
    /// library asks for its logger once, into a static field, and never asks again. Since a logger resolves
    /// <see cref="Slf4jBridge.Factory"/> on every call, caching them costs nothing in flexibility.
    /// </remarks>
    public sealed class Slf4jLoggerFactory : org.slf4j.ILoggerFactory
    {

        readonly ConcurrentDictionary<string, Slf4jLogger> loggers = new();

        /// <summary>
        /// Gets the logger for <paramref name="name"/>, which is also the category it logs under.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public org.slf4j.Logger getLogger(string name) => loggers.GetOrAdd(name ?? string.Empty, static n => new Slf4jLogger(n));

    }

}
