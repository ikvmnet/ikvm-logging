using System.Runtime.CompilerServices;

using Xunit;

// slf4j resolves its provider once per process and every test here shares the result, so the tests cannot run
// against each other: they take turns installing an ILoggerFactory on the one global bridge.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace IKVM.Extensions.Logging.Slf4j.Tests;

/// <summary>
/// Names the bridge as slf4j's provider before anything in this assembly asks for a logger.
/// </summary>
/// <remarks>
/// This is the arrangement the package asks a consumer to make, so the tests make it the same way. It has to
/// be a module initializer rather than a fixture: slf4j binds on the first <c>LoggerFactory</c> call from
/// anywhere in the process, and a test that got there first would bind it to <c>NOPLogger</c> for the whole
/// run. Nothing is installed here — that is each test's business.
/// </remarks>
static class Slf4jBridgeInitializer
{

    [ModuleInitializer]
    internal static void Initialize() => Slf4jBridge.Register();

}
