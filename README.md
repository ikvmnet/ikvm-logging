# IKVM.Logging

Bridges between the Java logging APIs and .NET logging, for Java code running under
[IKVM](https://github.com/ikvmnet/ikvm).

| package | bridges |
|---|---|
| [`IKVM.Extensions.Logging.Java.Util`](src/IKVM.Extensions.Logging.Java.Util) | `java.util.logging` to `Microsoft.Extensions.Logging` |

See that package's [README](src/IKVM.Extensions.Logging.Java.Util/README.md) for how to attach the handler
and what the level mapping is.

## Not here yet

- **slf4j to `Microsoft.Extensions.Logging`.** A Java library that logs through slf4j with no binding on the
  classpath resolves to `org.slf4j.helpers.NOPLogger` and silently discards everything — including the
  diagnostics a library only computes when it finds the level enabled.
- Serilog sinks for either API, which the `Microsoft.Extensions.Logging` bridges make unnecessary for most
  applications.

## Building

- Build the **solution**: `dotnet build IKVM.Logging.slnx`.
- Test: `dotnet test src/IKVM.Extensions.Logging.Java.Util.Tests`. Plain xunit on VSTest, so `--filter` is
  honored.
- The packages and the test payload CI ships are produced by `IKVM.Logging.dist.msbuildproj`, into `dist/`.

## Releasing

Versions come from GitVersion: `main` builds are labelled `pre`, `develop` builds `dev`. Nothing reaches
nuget.org implicitly — run the workflow with its `publish` input set, or push a tag, and the same run
creates the GitHub release.
