# IKVM.Logging

Bridges between the Java logging APIs and .NET logging, for Java code running under
[IKVM](https://github.com/ikvmnet/ikvm). Point them at the `ILoggerFactory` your application already has, and
the Java libraries in your process log through the same pipeline as everything else, filtered by the same
configuration.

| package | bridges |
|---|---|
| [`IKVM.Extensions.Logging.Java.Util`](src/IKVM.Extensions.Logging.Java.Util) | `java.util.logging` to `Microsoft.Extensions.Logging` |
| [`IKVM.Extensions.Logging.Slf4j`](src/IKVM.Extensions.Logging.Slf4j) | [slf4j](https://www.slf4j.org/) to `Microsoft.Extensions.Logging` |

Each package's README has the details. The short version:

```csharp
// slf4j -- register early, from a module initializer, then install the container's factory
Slf4jBridge.Register();
Slf4jBridge.Install(host.Services.GetRequiredService<ILoggerFactory>());

// java.util.logging -- attach a handler to the subtree you want forwarded
var root = LogManager.getLogManager().getLogger("");
foreach (var handler in root.getHandlers())
    root.removeHandler(handler);

root.addHandler(new LoggerHandler(loggerFactory));
root.setLevel(Level.ALL);
```

## Why bother

Both APIs fail quietly when nothing is bound. slf4j with no provider returns `NOPLogger`; `java.util.logging`
with no handler writes to a console nobody reads. Either way the log statements disappear — and so does the
work a library only does when it finds a level enabled. Calcite's `HepPlanner.dumpGraph` returns immediately
unless planner tracing is on, taking the graph-consistency assertions it would have run with it, which is how
a planner bug ends up reproducible on one machine and not another.

## The two shapes

`java.util.logging` lets you construct a handler and attach it, so `LoggerHandler` takes an `ILoggerFactory`
and there is nothing global about it.

slf4j does not. It resolves one provider per process, on first use, and constructs it itself — so
`Slf4jBridge` has a single static anchor, because the API it implements has one. What it avoids is capturing:
the loggers slf4j hands out read the installed factory on every call, so a logger a Java library took into a
static field before your container existed starts working the moment you install one.

## Not here yet

Serilog sinks for either API, which the `Microsoft.Extensions.Logging` bridges make unnecessary for most
applications.

## Building

- Build the **solution**: `dotnet build IKVM.Logging.slnx`.
- Test: `dotnet test src/IKVM.Extensions.Logging.Slf4j.Tests`. Plain xunit on VSTest, so `--filter` is
  honored. The slf4j suite runs without parallelization — slf4j binds once per process and the tests take
  turns installing a factory on the one bridge.
- The packages and the test payload CI ships are produced by `IKVM.Logging.dist.msbuildproj`, into `dist/`.

## Releasing

Versions come from GitVersion: `main` builds are labelled `pre`, `develop` builds `dev`. Nothing reaches
nuget.org implicitly — run the workflow with its `publish` input set, or push a tag, and the same run
creates the GitHub release.
