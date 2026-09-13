using org.slf4j;
using org.slf4j.helpers;
using org.slf4j.spi;

namespace IKVM.Extensions.Logging.Slf4j
{

    /// <summary>
    /// The slf4j provider for this bridge. slf4j constructs it, by name, from the <c>slf4j.provider</c> system
    /// property that <see cref="Slf4jBridge.Register"/> sets.
    /// </summary>
    /// <remarks>
    /// Nothing here holds an <c>ILoggerFactory</c>. This object exists from the moment slf4j binds, which is
    /// generally before an application has a container to take one from; where it forwards to is
    /// <see cref="Slf4jBridge.Factory"/>, read afresh on every call. Being constructed by slf4j through
    /// reflection, it needs to be public and to keep a public parameterless constructor.
    /// </remarks>
    public class Slf4jServiceProvider : SLF4JServiceProvider
    {

        /// <summary>
        /// slf4j API version this provider is written against. slf4j compares it against its own and warns on a
        /// mismatch; the trailing <c>.99</c> is the convention providers use to mean "any 2.0.x".
        /// </summary>
        const string RequestedApiVersion = "2.0.99";

        readonly Slf4jLoggerFactory loggerFactory = new();
        readonly IMarkerFactory markerFactory = new BasicMarkerFactory();
        readonly MDCAdapter mdcAdapter = new BasicMDCAdapter();

        /// <summary>
        /// Gets the factory slf4j asks for loggers.
        /// </summary>
        /// <returns></returns>
        public ILoggerFactory getLoggerFactory() => loggerFactory;

        /// <summary>
        /// Gets the marker factory.
        /// </summary>
        /// <returns></returns>
        public IMarkerFactory getMarkerFactory() => markerFactory;

        /// <summary>
        /// Gets the MDC adapter.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// A real one, so that Java code calling <c>MDC.put</c> works as it would under any other provider.
        /// What is in the MDC when a record is raised is attached to the .NET log entry as a scope.
        /// </remarks>
        public MDCAdapter getMDCAdapter() => mdcAdapter;

        /// <summary>
        /// Gets the slf4j API version this provider is built against.
        /// </summary>
        /// <returns></returns>
        public string getRequestedApiVersion() => RequestedApiVersion;

        /// <summary>
        /// Called once by slf4j after it selects this provider. There is nothing to do: the bridge is usable
        /// immediately and stays quiet until an <c>ILoggerFactory</c> is installed.
        /// </summary>
        public void initialize()
        {

        }

    }

}
