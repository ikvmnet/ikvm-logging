# IKVM.Extensions.Logging.Java.Util

[![NuGet](https://img.shields.io/nuget/v/IKVM.Extensions.Logging.Java.Util)](https://www.nuget.org/packages/IKVM.Extensions.Logging.Java.Util)

A `java.util.logging.Handler` that forwards the records raised by Java code running under
[IKVM](https://github.com/ikvmnet/ikvm) to `Microsoft.Extensions.Logging`, so a Java library logs through the
same pipeline as the rest of the application instead of writing to its own console handler.

## Supported platforms

Targets **.NET 8**, and is verified on **.NET 8** and **.NET 10**.

## Install

```sh
dotnet add package IKVM.Extensions.Logging.Java.Util
```

## Use

Attach a handler to the logger whose subtree should be forwarded, which is usually the root:

```csharp
using IKVM.Extensions.Logging.Java.Util;

using java.util.logging;

var root = LogManager.getLogManager().getLogger("");

// otherwise every record is also written to the console by java.util.logging's own default handler
foreach (var handler in root.getHandlers())
    root.removeHandler(handler);

root.addHandler(new LoggerHandler(loggerFactory));
root.setLevel(Level.ALL);
```

A record reaches the handler under the name of the Java logger that raised it, so the category an
`ILogger` is created with is the Java logger name — `org.apache.calcite.plan.hep.HepPlanner`, for example —
and the usual `Logging:LogLevel` configuration filters on it by prefix.

## Levels

Levels are mapped by value, so a custom level sits wherever its severity puts it:

| java.util.logging | LogLevel |
|---|---|
| `SEVERE` and above | `Error` |
| `WARNING` | `Warning` |
| `INFO`, `CONFIG` | `Information` |
| `FINE` | `Debug` |
| `FINER`, `FINEST` | `Trace` |
| `OFF` | dropped |

**A Java logger filters on its own level before any handler is reached.** A subtree left at its default
`INFO` forwards nothing finer no matter how permissive the `ILoggerFactory` is, which is why the example
above sets `Level.ALL` on the root: set the Java side to the finest level any `ILogger` rule asks for, and
let `Microsoft.Extensions.Logging` do the filtering from there.

## Notes

- `flush()` does nothing. Records are handed over as they arrive, and any buffering past that point belongs
  to the logging provider.
- `close()` stops forwarding but leaves the `ILoggerFactory` open — it belongs to the caller.
- A failure inside the logging pipeline goes to the handler's `ErrorManager` rather than propagating into
  the Java code that raised the record.
