using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RDCore.SDK.Server.Configuration;
using RDCore.SDK.Server.Handlers;
using RDCore.SDK.Server.Services;
using RDCore.SDK.Server.Services.States;
using System.Diagnostics;

namespace RDCore.SDK.Server;

/// <summary>
/// Simplifies implementing a <c>RDCore</c> <em>LSP server</em> application.
/// </summary>
/// <remarks>
/// 🧩 Override templated methods to customize your application.<br/>
/// <list type="bullet">
/// <item>Implement (<c>override</c>) <see cref="AppHost{TApp}.ConfigureExternalLogging(IServiceCollection, ILoggingBuilder, IConfiguration)"/> to override the default <see cref="ILoggingBuilder"/> providers.</item>
/// </list>
/// </remarks>
public class RDCorePlatformServerHost<TApp>() : AppHost<TApp>() 
    where TApp : class, IRDCoreServerApp
{
    /// <summary>
    /// Gets a service that manages the operational state of the language server.
    /// </summary>
    protected IServerStateProvider ServerStateProvider { get; private set; } = default!;
    /// <summary>
    /// Gets the application exit code corresponding to the current <see cref="ServerState"/>.
    /// </summary>
    public override int ExitCode => ServerStateProvider.State.ExitCode;

    protected override void Configure(IConfigurationBuilder configuration, IServiceCollection services, string[] args)
    {
        var commandLineArgs = CommandLine.Parser.Default.ParseArguments<SdkAppCommandLineArgs>(args);
        var overrides = new Dictionary<string, string?>
        {
            ["Configuration:Platform:Transport:PipeConfig:PipeName"] = commandLineArgs.Value.PipeName ?? throw new ArgumentNullException("args[PipeName]"),
            ["Configuration:Workspace:WorkspaceUri"] = commandLineArgs.Value.WorkspaceUri ?? throw new ArgumentNullException("args[WorkspaceUri]"),
            ["Configuration:Server:TraceLevel"] = commandLineArgs.Value.TraceLevel?.ToString() ?? LogLevel.Trace.ToString(),
            ["Configuration:Server:Verbose"] = commandLineArgs.Value.Verbose?.ToString() ?? false.ToString(),
        };
        configuration.AddInMemoryCollection(overrides);
    }
    protected override void ConfigureAdditionalExternalServices(IServiceCollection services, IConfiguration configuration)
    {
        ServerStateProvider = new ServerStateProvider(configuration);
        services.AddSingleton<ExecuteCommandHandler>();
    }
}
