using CommandLine;

namespace RDCore.SDK.Server.Configuration;

/// <summary>
/// Parses a platform application's command line into <see cref="SdkAppCommandLineArgs"/>.
/// </summary>
/// <remarks>
/// Every platform application parses the same arguments the same way, and every one of them has to tell
/// a command line that <em>asked a question</em> apart from one that configures a run: the parser answers
/// <c>--help</c> and <c>--version</c> by writing the text itself, then reports the request as a parse
/// error and hands back no configuration. An application asks <see cref="TryAnswer(string[], out int)"/>
/// first, and goes on to <see cref="Parse(string[])"/> only when it returns <c>false</c>.
/// </remarks>
public static class SdkCommandLine
{
    /// <summary>
    /// Parses <paramref name="args"/> as <see cref="SdkAppCommandLineArgs"/>.
    /// </summary>
    /// <remarks>
    /// A relative <see cref="SdkAppCommandLineArgs.WorkspaceUri"/> is read against the current directory, as the shell that started the
    /// application reads it, and becomes the full path. A full path and a URI are kept as written.
    /// </remarks>
    /// <param name="args">The command-line arguments as received.</param>
    /// <returns>
    /// The parsed arguments; <see langword="null"/> when <paramref name="args"/> does not parse, a help or version request
    /// included (<see cref="TryAnswer(string[], out int)"/>).
    /// </returns>
    public static SdkAppCommandLineArgs Parse(string[] args)
        => Parser.Default.ParseArguments<SdkAppCommandLineArgs>(args).Value is { } parsed
            ? parsed with { WorkspaceUri = FullWorkspace(parsed.WorkspaceUri) }
            : null!;

    /// <summary>
    /// Whether the platform started the process as one of its servers: the command line parses as a platform application's
    /// (<see cref="SdkAppCommandLineArgs"/>) and names the pipe the server is to connect on (<c>--pipe-name</c>), as every child process's does.
    /// </summary>
    /// <remarks>
    /// An executable that is a platform extension and also runs on its own (a web host, a tool) asks this to choose what it runs as. It writes nothing:
    /// a command line of another kind, such as a web host's <c>--urls</c>, is not answered as <see cref="TryAnswer(string[], out int)"/> answers it.
    /// </remarks>
    /// <param name="args">The command-line arguments as received.</param>
    /// <returns>
    /// <see langword="true"/> when the platform started the process; <see langword="false"/> for any other command line, an empty one included.
    /// </returns>
    public static bool IsServerLaunch(string[] args)
    {
        using var quiet = new Parser(settings => settings.HelpWriter = null);
        return quiet.ParseArguments<SdkAppCommandLineArgs>(args).Value is { PipeName.Length: > 0 };
    }

    /// <summary>
    /// Makes the platform root that <paramref name="args"/> names with <c>--platform-root</c> (<see cref="SdkAppCommandLineArgs.PlatformRoot"/>) this
    /// process's: sets <c>RDCORE_PLATFORM_ROOT</c> (<see cref="Platform.PlatformEnvironment.RootEnvironmentVariable"/>) to it, over what the process
    /// was started with. Every <see cref="Platform.PlatformEnvironment"/> made after reads it, and every process this one starts inherits it.
    /// </summary>
    /// <remarks>
    /// <see cref="AppHost{TApp}.RunAsync(string[])"/> calls this first. A host that does not run through it calls this before anything reads
    /// <see cref="Platform.PlatformEnvironment.Default"/>, which reads the root once. The argument is found among any others, a verb's included, as
    /// <c>--platform-root &lt;path&gt;</c> or <c>--platform-root=&lt;path&gt;</c>; a relative path is read against the current directory.
    /// </remarks>
    /// <param name="args">The command-line arguments as received.</param>
    /// <returns>
    /// The full path of the root the argument names; <see langword="null"/> when it names none, and the environment is then as it was.
    /// </returns>
    public static string? ApplyPlatformRoot(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        const string option = "--platform-root";
        for (var index = 0; index < args.Length; index++)
        {
            var root = args[index] == option && index + 1 < args.Length ? args[index + 1]
                : args[index].StartsWith($"{option}=", StringComparison.Ordinal) ? args[index][(option.Length + 1)..]
                : null;
            if (root is { Length: > 0 })
            {
                var full = Path.GetFullPath(root);
                Environment.SetEnvironmentVariable(Platform.PlatformEnvironment.RootEnvironmentVariable, full);
                return full;
            }
        }

        return null;
    }

    // read as a URI, as the platform reads a workspace, a relative path is rooted at "/": "." is the root, and "Ledger" a folder there.
    private static string? FullWorkspace(string? workspace)
        => workspace is not { Length: > 0 } || Path.IsPathFullyQualified(workspace) || Uri.TryCreate(workspace, UriKind.Absolute, out _)
            ? workspace
            : Path.GetFullPath(workspace);

    /// <summary>
    /// Answers <paramref name="args"/> when it is not a command line to run with: one that asked only
    /// for the help or version text, or one the parser could not make sense of at all.
    /// </summary>
    /// <param name="args">The command-line arguments as received.</param>
    /// <param name="exitCode">
    /// The process exit code: <c>0</c> for a question that was answered, <c>-1</c> for a command line
    /// that could not be parsed. Meaningless when this returns <c>false</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the parser has written its answer and there is nothing to run; <c>false</c> if
    /// <paramref name="args"/> configures a run, which the caller goes on to <see cref="Parse(string[])"/>.
    /// </returns>
    /// <remarks>
    /// Writing the text, or the list of what it did not understand, is the parser's own side effect of
    /// being asked. It reports a help or version request as a parse <em>error</em> because there is
    /// nothing left for it to parse, not because anything is wrong with what it was given, so an
    /// application has to ask this before it decides it has failed. In either case the parser hands back
    /// no configuration to run with: <see cref="Parse(string[])"/> returns <see langword="null"/>.
    /// </remarks>
    public static bool TryAnswer(string[] args, out int exitCode)
    {
        var errors = Parser.Default.ParseArguments<SdkAppCommandLineArgs>(args).Errors.ToArray();
        if (errors.Length == 0)
        {
            exitCode = 0;
            return false;
        }

        exitCode = errors.Any(error => error.Tag is ErrorType.HelpRequestedError
            or ErrorType.HelpVerbRequestedError
            or ErrorType.VersionRequestedError) ? 0 : -1;
        return true;
    }
}
