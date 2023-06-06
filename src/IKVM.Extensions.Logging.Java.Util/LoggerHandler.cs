using java.util.logging;

using Microsoft.Extensions.Logging;

namespace IKVM.Extensions.Logging.Java.Util
{

    public class LoggerHandler : Handler
    {

        readonly ILoggerFactory factory;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="factory"></param>
        public LoggerHandler(ILoggerFactory factory)
        {
            this.factory = factory;
        }

        public override void publish(LogRecord record)
        {

        }

        public override void close()
        {

        }

        public override void flush()
        {

        }

    }

}
