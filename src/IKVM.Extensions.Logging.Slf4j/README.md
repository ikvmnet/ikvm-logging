# IKVM.Extensions.Logging.Slf4j

[![NuGet](https://img.shields.io/nuget/v/IKVM.Extensions.Logging.Slf4j)](https://www.nuget.org/packages/IKVM.Extensions.Logging.Slf4j)

An [slf4j](https://www.slf4j.org/) provider that forwards the records logged by Java code running under
[IKVM](https://github.com/ikvmnet/ikvm) to `Microsoft.Extensions.Logging`.

Without a provider on the class path, slf4j binds to `org.slf4j.helpers.NOPLogger` and **every log statement
in every Java library silently does nothing**. That is worse than it sounds: a library that guards work
behind a level check does not do the work either. Calcite's `HepPlanner.dumpGraph` returns immediately unless
planner tracing is enabled, and the graph-consistency assertions it would otherwise run never execute — so a
process with no provider bound exercises strictly less code than one that has one, and a planner bug can be
reproducible on one machine and not another for no visible reason.

## Supported platforms

Targets **.NET 8**, and is verified on **.NET 8** and **.NET 10**. Needs **slf4j 2.0.9 or later**.

## Install

```sh
dotnet add package IKVM.Extensions.Logging.Slf4j
```

## Use

Two calls. Register the bridge as early as you can, from a module initializer:

```csharp
using System.Runtime.CompilerServices;

using IKVM.Extensions.Logging.Slf4j;

static class LoggingInitializer
{

    [ModuleInitializer]
    internal static void Initialize() => Slf4jBridge.Register();

}
```

Then point it at your container's factory once you have one:

```csharp
var host = builder.Build();

Slf4jBridge.Install(host.Services.GetRequiredService<ILoggerFactory>());
```

`Install` registers too, so an application that can do both in one place needs only the second line. It
returns an `IDisposable` that puts back whatever was installed before, which is what a test wants; an
application can discard it.

A record arrives on the `ILogger` whose category is the slf4j logger name — `org.apache.calcite.plan.hep.HepPlanner`,
for instance — so the usual `Logging:LogLevel` configuration filters Java logging by prefix, with no
Java-side configuration at all.

## Why two calls

slf4j resolves a provider **once per process**, on the first call to `LoggerFactory`, and there is no
per-container equivalent to bind instead. So there is exactly one global anchor here. What it is not is
*captured*: the provider is live from the moment slf4j binds, and the loggers it hands out read the installed
`ILoggerFactory` on **every call** rather than the one that existed when they were created.

That matters because Java libraries take their logger once, into a static field — Calcite's
`CalciteTrace.getPlannerTracer()` is exactly this — long before an application has a container. A bridge that
captured the factory at logger-creation time would leave those loggers pointed at nothing for the life of the
process. Here they start working the moment `Install` is called, and stop when it is disposed.

`Register` still has to beat the first Java logger to the draw, because what slf4j resolves it keeps. If
something logged first, the process is bound to `NOPLogger` for good and nothing here can change it —
`Slf4jBridge.IsBound` says which happened.

## Levels

slf4j's levels and `LogLevel` agree rank for rank:

| slf4j | LogLevel |
|---|---|
| `ERROR` | `Error` |
| `WARN` | `Warning` |
| `INFO` | `Information` |
| `DEBUG` | `Debug` |
| `TRACE` | `Trace` |

## Notes

- `{}` anchors are substituted by slf4j's own `MessageFormatter`, so a message renders exactly as it would
  under any other provider. The result is passed on verbatim rather than as a .NET format string, so a brace
  in a plan dump stays a brace.
- A trailing `Throwable` becomes the entry's exception, following slf4j's argument rules.
- Whatever is in the MDC when a record is raised is attached to the entry as a log scope, so a provider that
  renders scopes shows the same context slf4j would.
- Markers are accepted and ignored; `Microsoft.Extensions.Logging` has nothing that corresponds to them.
- An explicit `-Dslf4j.provider` naming some other provider is left alone by `Register`.

## How the provider is selected

By the `slf4j.provider` system property, which names
`cli.IKVM.Extensions.Logging.Slf4j.Slf4jServiceProvider` — `cli.` being how IKVM makes a .NET type visible to
Java. `Slf4jBridge.Register` sets it.

The usual mechanism, a `META-INF/services/org.slf4j.spi.SLF4JServiceProvider` entry found by `ServiceLoader`,
is not available to a provider written in C#. IKVM presents an assembly's manifest resources as a flat set of
names under one virtual directory, while its virtual file system resolves a lookup path one segment at a
time — so a resource whose name contains a separator can be named by `getResource` but never opened, and
`ServiceLoader` fails on it with `Error reading configuration file`. Every `META-INF/services` entry has
separators in it by construction. Naming the class outright sidesteps the mechanism.
