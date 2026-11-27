using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using RDCore.SDK.Platform;
using RDCore.SDK.Server.Configuration;

namespace RDCore.SDK.Server.Auditing;

/// <summary>
/// Registers the audit log of a component (<see cref="IAuditLog"/>).
/// </summary>
public static class AuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers the component's <see cref="IAuditLog"/>, made from the <c>Audit</c> section of its configuration (<see cref="SdkAppOptions.Audit"/>) the first
    /// time it is asked for, and disposed with the service provider, which writes what it still holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The log writes to a logger factory of its own, never the component's: a filter of the component's trace log never drops an entry, and the trace log
    /// never holds one. Its providers are the file (<see cref="SdkAuditOptions.File"/>, <see cref="AuditFileLoggerProvider"/>), the Windows Event Log on Windows
    /// (<see cref="SdkAuditOptions.EventLog"/>), and every <see cref="ILoggerProvider"/> registered as a keyed service of <see cref="AuditLog.ProviderKey"/>.
    /// With none of them, the log is <see cref="AuditLog.None"/>.
    /// </para>
    /// <para>
    /// It needs <see cref="IOptions{TOptions}"/> of <see cref="SdkAppOptions"/>, an <see cref="IFileSystem"/> and an <see cref="IPlatformEnvironment"/>, which a
    /// platform component registers. The component its entries name is <see cref="SdkServerAppOptions.Component"/>. A registration already made is kept.
    /// </para>
    /// </remarks>
    /// <param name="services">The services of the component.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddAuditLog(this IServiceCollection services)
    {
        services.TryAddSingleton(provider => AuditLogHost.Create(provider));
        services.TryAddSingleton(provider => provider.GetRequiredService<AuditLogHost>().Log);
        return services;
    }

    /// <summary>
    /// The folder of the audit log's files that the options say: <see cref="IPlatformEnvironment.AuditsDirectory"/> when they name none, and a relative folder
    /// under the platform root.
    /// </summary>
    /// <param name="options">The options of the file.</param>
    /// <param name="environment">The platform.</param>
    public static string DirectoryOf(SdkAuditFileOptions options, IPlatformEnvironment environment)
        => string.IsNullOrWhiteSpace(options.Directory) ? environment.AuditsDirectory
            : Path.IsPathRooted(options.Directory) ? options.Directory
            : environment.Resolve(options.Directory);

    // the log, the logger factory it writes to, and the providers made for it, disposed in that order: the log writes what it holds, then the providers close
    // their files. A factory does not dispose the providers it is given, and a keyed provider is the service provider's to dispose.
    internal sealed class AuditLogHost(IAuditLog log, ILoggerFactory? factory, IReadOnlyList<ILoggerProvider> owned) : IAsyncDisposable, IDisposable
    {
        public IAuditLog Log { get; } = log;

        public static AuditLogHost Create(IServiceProvider provider)
        {
            var options = provider.GetRequiredService<IOptions<SdkAppOptions>>().Value;
            var audit = options.Audit;
            var component = options.ServerApp.Component.ToString();
            var process = System.Environment.ProcessId;

            var owned = new List<ILoggerProvider>();
            if (audit.File.Enabled)
            {
                var directory = DirectoryOf(audit.File, provider.GetRequiredService<IPlatformEnvironment>());
                owned.Add(new AuditFileLoggerProvider(provider.GetRequiredService<IFileSystem>(), directory, component, process));
            }

            if (audit.EventLog.Enabled && OperatingSystem.IsWindows())
            {
                owned.Add(new EventLogLoggerProvider(new EventLogSettings { LogName = audit.EventLog.LogName, SourceName = audit.EventLog.Source }));
            }

            var providers = owned.Concat(provider.GetKeyedServices<ILoggerProvider>(AuditLog.ProviderKey)).ToArray();
            if (providers.Length == 0)
            {
                return new AuditLogHost(AuditLog.None, null, []);
            }

            var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                foreach (var each in providers)
                {
                    builder.AddProvider(each);
                }
            });

            var trace = provider.GetService<ILoggerFactory>()?.CreateLogger(AuditLog.Category);
            var log = new AuditLog(factory.CreateLogger(AuditLog.Category), component, process, audit.Capacity,
                TimeSpan.FromSeconds(audit.ShutdownTimeoutSeconds), trace);
            return new AuditLogHost(log, factory, owned);
        }

        public async ValueTask DisposeAsync()
        {
            if (Log is IAsyncDisposable log)
            {
                await log.DisposeAsync().ConfigureAwait(false);
            }

            DisposeProviders();
        }

        public void Dispose()
        {
            (Log as IDisposable)?.Dispose();
            DisposeProviders();
        }

        private void DisposeProviders()
        {
            factory?.Dispose();
            foreach (var each in owned)
            {
                each.Dispose();
            }
        }
    }
}
