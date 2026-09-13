using System;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace IKVM.Extensions.Logging.Slf4j
{

    /// <summary>
    /// The one place slf4j and <see cref="ILoggerFactory"/> are joined: names this assembly as slf4j's
    /// provider, and holds the factory the records it raises are forwarded to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// slf4j resolves a provider once per process, on the first call to <c>LoggerFactory</c>, and there is no
    /// per-container equivalent to bind instead — so there is exactly one anchor here, and it is static. What
    /// it is not is <em>captured</em>: <see cref="Install"/> may be called at any point, and the loggers slf4j
    /// has already handed out resolve the current factory on every call rather than the one that existed when
    /// they were made. That matters because Java libraries hold their loggers in static fields — Calcite's
    /// <c>CalciteTrace.getPlannerTracer()</c> is one — and a logger captured before the host was built would
    /// otherwise stay pointed at nothing for the life of the process.
    /// </para>
    /// <para>
    /// The usual arrangement is two calls. <see cref="Register"/> as early as possible, from a module
    /// initializer, which costs nothing and only has to beat the first Java logger to the draw:
    /// </para>
    /// <code>
    /// [ModuleInitializer]
    /// internal static void InitializeLogging() => Slf4jBridge.Register();
    /// </code>
    /// <para>
    /// Then <see cref="Install"/> once the container exists, with the factory it resolved:
    /// </para>
    /// <code>
    /// var host = builder.Build();
    /// Slf4jBridge.Install(host.Services.GetRequiredService&lt;ILoggerFactory&gt;());
    /// </code>
    /// <para>
    /// <see cref="Install"/> calls <see cref="Register"/> itself, so an application that can do both at once
    /// needs only the second line.
    /// </para>
    /// </remarks>
    public static class Slf4jBridge
    {

        /// <summary>
        /// Name of the system property slf4j reads to load a provider by name, added in slf4j 2.0.9.
        /// </summary>
        /// <remarks>
        /// The alternative, and what a provider shipped as a jar uses, is a
        /// <c>META-INF/services/org.slf4j.spi.SLF4JServiceProvider</c> entry found by <c>ServiceLoader</c>.
        /// That does not work for a resource embedded in a .NET assembly: IKVM presents an assembly's manifest
        /// resources as a flat set of names under one virtual directory, while its virtual file system walks a
        /// lookup path one segment at a time, so a resource whose name contains a separator can be named by
        /// <c>getResource</c> but never opened — <c>getResourceAsStream</c> answers null and
        /// <c>ServiceLoader</c> fails with <c>Error reading configuration file</c>. Every <c>META-INF/services</c>
        /// entry has separators in it by construction. Naming the class outright avoids the mechanism entirely.
        /// </remarks>
        public const string ProviderProperty = "slf4j.provider";

        static ILoggerFactory? factory;

        /// <summary>
        /// Name <see cref="ProviderProperty"/> has to carry for slf4j to load <see cref="Slf4jServiceProvider"/>.
        /// </summary>
        /// <remarks>
        /// Derived rather than written out, so that renaming the type cannot quietly stop the bridge from
        /// binding. <c>cli.</c> is the prefix under which IKVM makes a .NET type visible to Java.
        /// </remarks>
        public static string ProviderClassName { get; } = "cli." + typeof(Slf4jServiceProvider).FullName;

        /// <summary>
        /// Factory records are currently forwarded to, or <c>null</c> while nothing has been installed, in
        /// which case the bridge is bound but quiet.
        /// </summary>
        public static ILoggerFactory? Factory => Volatile.Read(ref factory);

        /// <summary>
        /// Whether slf4j has bound to this bridge.
        /// </summary>
        /// <remarks>
        /// Reading this resolves slf4j's provider if nothing has yet, which is a decision that cannot be taken
        /// back — so it answers the question for a test or a diagnostic, and is not a thing to check before
        /// calling <see cref="Register"/>.
        /// </remarks>
        public static bool IsBound => org.slf4j.LoggerFactory.getILoggerFactory() is Slf4jLoggerFactory;

        /// <summary>
        /// Names this bridge as slf4j's provider, unless a provider has already been chosen.
        /// </summary>
        /// <remarks>
        /// Has to happen before any Java code asks for a logger. slf4j resolves its provider once and caches
        /// what it resolved, so a process where something logged first is bound to <c>NOPLogger</c> for good
        /// and nothing here can change it — see <see cref="IsBound"/>. An explicit <c>-Dslf4j.provider</c>
        /// naming some other provider is left alone.
        /// </remarks>
        public static void Register()
        {
            if (string.IsNullOrEmpty(java.lang.System.getProperty(ProviderProperty)))
                java.lang.System.setProperty(ProviderProperty, ProviderClassName);
        }

        /// <summary>
        /// Registers the bridge and points it at <paramref name="loggerFactory"/>.
        /// </summary>
        /// <param name="loggerFactory">factory the forwarded records are logged through</param>
        /// <returns>
        /// A handle that puts back whatever was installed before, which is what a test wants and an
        /// application can discard.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <c>null</c></exception>
        public static IDisposable Install(ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
                throw new ArgumentNullException(nameof(loggerFactory));

            Register();
            return new Registration(Interlocked.Exchange(ref factory, loggerFactory));
        }

        /// <summary>
        /// Stops forwarding, leaving the bridge bound and quiet. The <see cref="ILoggerFactory"/> belongs to
        /// the caller and is left open.
        /// </summary>
        public static void Uninstall() => Volatile.Write(ref factory, null);

        sealed class Registration(ILoggerFactory? previous) : IDisposable
        {

            int disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                    Volatile.Write(ref factory, previous);
            }

        }

    }

}
