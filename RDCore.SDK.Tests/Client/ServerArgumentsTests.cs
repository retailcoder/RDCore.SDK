using Microsoft.Extensions.Logging;
using RDCore.SDK.Client;
using RDCore.SDK.Server.Configuration;
using System.Text.RegularExpressions;

namespace RDCore.Tests.Client;

/// <summary>
/// The command line a client starts a server process with. It is read back by the server's own argument parser, which
/// is the only thing that can say whether it is a command line a server starts on: one that does not parse is a server
/// that exits before it ever connects, and the client only finds that out as "the child process exited".
/// </summary>
[TestClass]
public sealed partial class ServerArgumentsTests
{
    // a command line as the process would be handed it: split on spaces, a quoted run being one argument.
    [GeneratedRegex("\"[^\"]*\"|\\S+")]
    private static partial Regex Token();

    private static string[] Split(string commandLine)
        => [.. Token().Matches(commandLine).Select(match => match.Value.Trim('"'))];

    private static SdkAppCommandLineArgs ParsedBack(string? language, bool verbose = true)
    {
        var arguments = Split(RDCoreServerProcess.ServerArguments(1234, "pipe", "C:/ws", LogLevel.Trace, verbose, language));

        Assert.IsFalse(SdkCommandLine.TryAnswer(arguments, out var exitCode), $"the server's parser rejected it ({exitCode}): {string.Join(" ", arguments)}");
        return SdkCommandLine.Parse(arguments);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("vba")]
    [DataRow("vb6")]
    [DataRow("basic")]
    public void TheCommandLine_IsOneTheServerCanStartOn(string? language)
    {
        var parsed = ParsedBack(language);

        Assert.AreEqual(1234, parsed.ClientProcessId);
        Assert.AreEqual("C:/ws", parsed.WorkspaceUri);
    }

    [TestMethod]
    [DataRow("basic")]
    [DataRow("vb6")]
    public void AClientsLanguage_ReachesTheServer(string language)
        => Assert.AreEqual(language, ParsedBack(language).Language);

    [TestMethod]
    [DataRow(null)]
    [DataRow("vba")]
    [DataRow("VBA")]
    public void TheDefaultLanguage_IsLeftOutSoTheServersOwnSettingsWin(string? language)
        => Assert.IsNull(ParsedBack(language).Language);

    [TestMethod]
    public void TheLanguage_ReachesTheServersConfiguration()
        => CollectionAssert.Contains(
            ParsedBack("basic").ToConfigurationOverrides().ToList(),
            new KeyValuePair<string, string?>("Configuration:Workspace:Language", "basic"));

    [TestMethod]
    public void TheVerboseSwitch_IsTheLastArgument()
        // a switch the parser only reads as one at the end of a command line.
        => StringAssert.EndsWith(
            RDCoreServerProcess.ServerArguments(1, "pipe", "C:/ws", LogLevel.Trace, verbose: true, "basic").TrimEnd(), "-v");

    // the constants the client's command line defines are those of every component: a string's quotes, a backslash before one, a space, all as written.
    [TestMethod]
    public void TheConstantsTheClientDefines_ReachTheServersConfiguration()
    {
        var defines = new Dictionary<string, string> { ["DEV"] = "1", ["MODE"] = "\"debug build\"", ["LOGS"] = "\"C:\\logs\\\"" };
        var commandLine = RDCoreServerProcess.ServerArguments(1234, "pipe", "C:/ws", LogLevel.Trace, verbose: true, "basic", defines);
        var arguments = AsTheProcessReadsThem(commandLine);

        Assert.IsFalse(SdkCommandLine.TryAnswer(arguments, out var exitCode), $"the server's parser rejected it ({exitCode}): {commandLine}");
        var overrides = SdkCommandLine.Parse(arguments).ToConfigurationOverrides().ToList();
        foreach (var (name, value) in defines)
        {
            CollectionAssert.Contains(overrides, new KeyValuePair<string, string?>($"Configuration:Workspace:Defines:{name}", value), commandLine);
        }

        StringAssert.EndsWith(commandLine.TrimEnd(), "-v");
    }

    // the workspace as the client says it: a folder's path ends with its separator when it was read from a folder's URI, which a debug adapter is started
    // with; a space, and a path that is a URI, as written.
    [TestMethod]
    [DataRow(@"C:\ws\", DisplayName = "a path that ends with its separator")]
    [DataRow(@"C:\my ws\", DisplayName = "a path with a space that ends with its separator")]
    [DataRow(@"C:\ws", DisplayName = "a path")]
    [DataRow("file:///c:/ws/", DisplayName = "a folder's URI")]
    public void TheWorkspace_ReachesTheServerAsTheClientSaysIt(string workspace)
    {
        var commandLine = RDCoreServerProcess.ServerArguments(1234, "pipe", workspace, LogLevel.Trace, verbose: true, "basic");
        var arguments = AsTheProcessReadsThem(commandLine);

        Assert.IsFalse(SdkCommandLine.TryAnswer(arguments, out var exitCode), $"the server's parser rejected it ({exitCode}): {commandLine}");
        var parsed = SdkCommandLine.Parse(arguments);
        Assert.AreEqual(workspace, parsed.WorkspaceUri, commandLine);
        Assert.AreEqual("basic", parsed.Language, $"what follows the workspace is read as written: {commandLine}");
    }

    [TestMethod]
    public void NoConstant_IsNoArgument()
        => Assert.DoesNotContain(
            "--define", RDCoreServerProcess.ServerArguments(1, "pipe", "C:/ws", LogLevel.Trace, verbose: true, defines: new Dictionary<string, string>()));

    // the rules a process reads its command line by: a quote begins or ends a quoted run, and 2n backslashes before one are n, 2n+1 are n and the quote.
    private static string[] AsTheProcessReadsThem(string commandLine)
    {
        var arguments = new List<string>();
        var current = new System.Text.StringBuilder();
        var (quoted, started) = (false, false);
        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (character == '\\')
            {
                var backslashes = 0;
                for (; index < commandLine.Length && commandLine[index] == '\\'; index++)
                {
                    backslashes++;
                }

                var beforeQuote = index < commandLine.Length && commandLine[index] == '"';
                current.Append('\\', beforeQuote ? backslashes / 2 : backslashes);
                if (beforeQuote && backslashes % 2 == 1)
                {
                    current.Append('"');
                }
                else if (beforeQuote)
                {
                    quoted = !quoted;
                }
                else
                {
                    index--;
                }

                started = true;
            }
            else if (character == '"')
            {
                (quoted, started) = (!quoted, true);
            }
            else if (char.IsWhiteSpace(character) && !quoted)
            {
                if (started)
                {
                    arguments.Add(current.ToString());
                    (current, started) = (new System.Text.StringBuilder(), false);
                }
            }
            else
            {
                current.Append(character);
                started = true;
            }
        }

        if (started)
        {
            arguments.Add(current.ToString());
        }

        return [.. arguments];
    }
}
