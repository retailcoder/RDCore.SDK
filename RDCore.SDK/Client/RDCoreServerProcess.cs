using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RDCore.SDK.Platform;
using RDCore.SDK.Server.Configuration;
using System.Diagnostics;
using System.IO.Abstractions;

namespace RDCore.SDK.Client;

/// <summary>
/// Decouples the startup sequence from an actual <c>Process</c> boundary.
/// </summary>
public interface IRDCoreServerProcess : IDisposable
{
    /// <summary>
    /// Finds and runs the language server executable with command-line arguments mapping the specified <c>LanguageClientSettings</c>.
    /// </summary>
    void Start(CancellationTokenSource tokenSource);
    /// <summary>
    /// Stops awaiting LSP server process exit to restart it.
    /// </summary>
    /// <remarks>
    /// This method should be invoked during the <c>Shutdown</c> LSP <em>server lifecycle</em> handler.
    /// </remarks>
    void Shutdown();
}

/// <summary>
/// Represents a <c>RDCore.SDK</c> <em> server application</em>.
/// </summary>
/// <remarks>
/// A <em>language server application</em> is any <c>RDCore.SDK</c> server platform application that can run a sidecar LSP server.
/// </remarks>
/// <param name="FileSystem">Provides an abstraction over the file system.</param>
/// <param name="Configuration">The current <see cref="IConfiguration"/> .</param>
/// <param name="Logger">A standard <see cref="ILogger"/>.</param>
public class RDCoreServerProcess(
    IFileSystem FileSystem,
    IPlatformCompositionService Platform,
    IConfiguration Configuration,
    ILogger<RDCoreServerProcess> Logger) : IRDCoreServerProcess
{
    private readonly CancellationTokenSource _tokenSource = new();
    private Process? _serverProcess = default;
    private Task? _waitForExit = default;

    public void Dispose()
    {
        _tokenSource.Dispose();

        _serverProcess?.Dispose();
        _serverProcess = default;

        _waitForExit?.Dispose();
        _waitForExit = default;

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) 
    {
        _serverProcess?.Dispose();
    }

    public void Shutdown() => _serverProcess?.Kill();

    public void Start(CancellationTokenSource tokenSource)
    {
        if (_serverProcess is Process running)
        {
            // this should not be happening
            throw new ServerAlreadyRunningException(running.Id);
        }

        var manifest = Platform.GetManifest();
        var fullPath = FileSystem.Path.Combine(Platform.RootPath, manifest.LangService);
        
        var args = CommandLine.UnParserExtensions.FormatCommandLine(CommandLine.Parser.Default, 
            new SdkAppCommandLineArgs
            {
                ClientProcessId = Environment.ProcessId,
                PipeName =  Configuration["Configuration:Platform:Transport:PipeConfig:PipeName"],
                TraceLevel = Enum.Parse<LogLevel>(Configuration["Configuration:Server:TraceLevel"] ?? "None"),
                Verbose = Convert.ToBoolean(Configuration["Configuration:Server:Verbose"]),
                WorkspaceUri = Configuration["Configuration:Workspace:WorkspaceUri"],
            });

        var info = CreateProcessStartInfo(fullPath, args);
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[ProcessStartInfo]\n\tPath:'{path}'\n\tWorkingDirectory:'{workdir}'\n\tArguments:'{args}'", fullPath, info.WorkingDirectory, info.Arguments); // TODO REMOVE
        }

        _serverProcess = Process.Start(info) ?? throw new ServerNotFoundException(fullPath);
        _waitForExit = _serverProcess.WaitForExitAsync(_tokenSource.Token);

        if (_serverProcess.HasExited)
        {
            // server process was started but unexpectedly exited.
            throw new ServerProtocolSdkException("Unable to start server process.");
        }
    }

    private ProcessStartInfo CreateProcessStartInfo(string validPath, string args) => new()
    {
        FileName = FileSystem.Path.GetFileName(validPath),
        WorkingDirectory = FileSystem.Path.GetDirectoryName(validPath),
        Arguments = args,
        CreateNoWindow = true,
        UseShellExecute = false,

        RedirectStandardInput = false,
        RedirectStandardOutput = false,
        RedirectStandardError = false
    };
}
