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
