using System.IO.Abstractions;

namespace RDCore.SDK.Platform;

/// <summary>
/// Resolves platform paths from an explicit <em>platform root</em>.
/// </summary>
/// <remarks>
/// The platform root is the directory that holds <c>rdcore.json</c>, <c>platform-settings.json</c>, <c>rdc</c> and its files, and one
/// subfolder per other component (<c>RDCore.LanguageServer/</c>, <c>RDCore.Parsing/</c>, <c>Extensions/&lt;Title&gt;/</c>, …). The
/// <c>RDCORE_PLATFORM_ROOT</c> environment variable (<see cref="PlatformEnvironment.RootEnvironmentVariable"/>) names it:
/// <list type="bullet">
/// <item><description><c>rdc</c>, at the root, sets it to its own folder or to the root <c>--platform-root</c> names, and keeps the one it was
/// given when it runs as a server of another process (<see cref="PlatformEnvironment.ApplyOwnFolderAsRoot(string[])"/>); each process it starts
/// inherits it;</description></item>
/// <item><description>a component in a subfolder inherits it from the process that started it, or is given it with <c>--platform-root</c>
/// (<see cref="Server.Configuration.SdkCommandLine.ApplyPlatformRoot(string[])"/>);</description></item>
/// <item><description>a program that <c>rdc build</c> wrote sets it to its own output folder
/// (<see cref="PlatformEnvironment.ApplyRoot(string)"/>).</description></item>
/// </list>
/// It is never searched for, nor derived from the working directory.
/// <para>
/// The root is read when a member that needs it is first read (<see cref="Root"/>, <see cref="ManifestPath"/>, <see cref="SettingsPath"/>,
/// <see cref="AuditsDirectory"/>, <see cref="Resolve(string)"/>). A process that is given none can use the members that do not need it
/// (<see cref="LogsDirectory"/>, <see cref="UserDataDirectory"/>), and fails with <see cref="PlatformRootNotGivenException"/> at the
/// first read of one that does.
/// </para>
/// </remarks>
public interface IPlatformEnvironment
{
    /// <summary>
    /// The platform root directory, as a full path.
    /// </summary>
    /// <exception cref="PlatformRootNotGivenException">The process was given no platform root.</exception>
    string Root { get; }

    /// <summary>
    /// Absolute path of the platform manifest, <c>&lt;Root&gt;/rdcore.json</c>.
    /// </summary>
    /// <exception cref="PlatformRootNotGivenException">The process was given no platform root.</exception>
    string ManifestPath { get; }

    /// <summary>
    /// Absolute path of the platform's settings file, <c>&lt;Root&gt;/platform-settings.json</c> (<see cref="PlatformEnvironment.SettingsFileName"/>): what
    /// every component of the platform is deployed with (<see cref="Server.Configuration.PlatformSettingsConfiguration"/>).
    /// </summary>
    /// <exception cref="PlatformRootNotGivenException">The process was given no platform root.</exception>
    string SettingsPath => Resolve(PlatformEnvironment.SettingsFileName);

    /// <summary>
    /// Absolute path of the folder the platform's components write their logs to, created if missing: the user's, not the platform root's, so that a
    /// platform can be installed where its user can't write (<c>Program Files</c>, <c>/opt</c>).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>Windows: <c>%LOCALAPPDATA%\RDCore\Logs</c>. Local, not roaming: a log is of the machine it was written on.</description></item>
    /// <item><description>macOS: <c>~/Library/Logs/RDCore</c>.</description></item>
    /// <item><description>Linux and others: <c>$XDG_STATE_HOME/rdcore/logs</c>, or <c>~/.local/state/rdcore/logs</c> where <c>XDG_STATE_HOME</c> isn't set:
    /// the XDG Base Directory Specification places logs in the user's state.</description></item>
    /// </list>
    /// <c>RDCORE_LOGS_DIRECTORY</c> (<see cref="PlatformEnvironment.LogsEnvironmentVariable"/>) overrides it everywhere.
    /// </remarks>
    string LogsDirectory { get; }

    /// <summary>
    /// Absolute path of the folder the audit log writes its files to, <c>&lt;Root&gt;/Logs/Audits</c> (<see cref="PlatformEnvironment.AuditsFolder"/>). Not
    /// created: nothing is kept there until an entry is written.
    /// </summary>
    /// <remarks>
    /// Under the platform root, and not under <see cref="LogsDirectory"/>: the audit is the administrator's, so <c>RDCORE_LOGS_DIRECTORY</c>, which a user
    /// can set, does not move it. The platform's configuration can name another folder (<see cref="Server.Configuration.SdkAuditFileOptions.Directory"/>).
    /// </remarks>
    /// <exception cref="PlatformRootNotGivenException">The process was given no platform root.</exception>
    string AuditsDirectory => Resolve(PlatformEnvironment.AuditsFolder);

    /// <summary>
    /// Resolves a root-relative path (either slash style) to an absolute path under <see cref="Root"/>.
    /// </summary>
    /// <param name="rootRelativePath">A path relative to <see cref="Root"/>, using either <c>/</c> or <c>\</c>.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="PlatformRootNotGivenException">The process was given no platform root.</exception>
    string Resolve(string rootRelativePath);

    /// <summary>
    /// Absolute path of the folder of the platform's user: <c>%USERAPPDATA%/RDCore</c>, where what the user sets for themselves is kept - their settings
    /// (<see cref="Server.Configuration.UserSettingsStore"/>). Not created: nothing is kept there until something is.
    /// </summary>
    /// <remarks>
    /// It is not under <see cref="Root"/>: a platform that is deployed again replaces what is there, and what the user set is theirs to keep.
    /// </remarks>
    string UserDataDirectory { get; }
}

/// <inheritdoc/>
public sealed class PlatformEnvironment : IPlatformEnvironment
{
    /// <summary>
    /// Name of the environment variable that names the platform root (<see cref="Root"/>). Set on the entry process, or by its
    /// <c>--platform-root</c> argument; a child that <see cref="Client.RDCoreServerProcess"/> starts inherits it.
    /// </summary>
    public const string RootEnvironmentVariable = Client.RDCoreLanguageServerLaunch.PlatformRootEnvironmentVariable;

    /// <summary>
    /// Name of the folder of the platform root that holds the descriptions of the libraries a project can reference
    /// (<see cref="Runtime.Libraries.DirectoryLibrarySource"/>).
    /// </summary>
    public const string SymbolsDirectory = "Symbols";

    /// <summary>
    /// The folder of the audit log's files, relative to the platform root (<see cref="IPlatformEnvironment.AuditsDirectory"/>).
    /// </summary>
    public const string AuditsFolder = "Logs/Audits";

    /// <summary>
    /// Name of the platform's settings file at the platform root (<see cref="IPlatformEnvironment.SettingsPath"/>). It does not begin with
    /// <c>appsettings</c>, the name a component's own settings file is found by.
    /// </summary>
    public const string SettingsFileName = "platform-settings.json";

    /// <summary>
    /// Name of the environment variable that overrides the folder of the platform's user (<see cref="UserDataDirectory"/>): what a test sets, so that it never reads
    /// nor writes the settings of whoever runs it. Inherited by spawned children.
    /// </summary>
    public const string UserDataEnvironmentVariable = "RDCORE_USER_DATA";

    /// <summary>
    /// Name of the environment variable that overrides the folder the platform logs to (<see cref="LogsDirectory"/>): what a test or a release check sets, to
    /// read the logs of the platform it starts and leave the user's alone. Inherited by spawned children.
    /// </summary>
    public const string LogsEnvironmentVariable = "RDCORE_LOGS_DIRECTORY";

    private static readonly Lazy<IPlatformEnvironment> _default = new(() => new PlatformEnvironment(new FileSystem()));

    /// <summary>
    /// A DI-free instance for bootstrap contexts (e.g. logging configuration).
    /// </summary>
    public static IPlatformEnvironment Default => _default.Value;

    /// <summary>
    /// Makes <paramref name="root"/> this process's platform root: sets <c>RDCORE_PLATFORM_ROOT</c> (<see cref="RootEnvironmentVariable"/>) to its
    /// full path, over any value the process inherited. Every <see cref="PlatformEnvironment"/> that has not read its root yet reads it, and every
    /// process this one starts inherits it.
    /// </summary>
    /// <remarks>
    /// For a process whose root is a fact of where it is: a program that <c>rdc build</c> wrote, whose root is its own output folder, which holds
    /// what it runs with (<see cref="SymbolsDirectory"/>, its external host) and no manifest. Call it before anything reads the root of
    /// <see cref="Default"/>, which keeps the root it first reads.
    /// </remarks>
    /// <param name="root">The platform root, absolute or relative to the working directory.</param>
    /// <returns>The full path of the root.</returns>
    public static string ApplyRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        // a folder named with its separator (AppContext.BaseDirectory's) is the same root as the one named without it.
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        Environment.SetEnvironmentVariable(RootEnvironmentVariable, full);
        return full;
    }

    /// <summary>
    /// Makes the platform root of a process that sits at the root, <c>rdc</c>, this process's: the root that <paramref name="args"/> names with
    /// <c>--platform-root</c>; when another process started it as one of its servers, the root that process gave it; otherwise the process's own
    /// folder (<see cref="AppContext.BaseDirectory"/>), which must hold the platform's manifest (<c>rdcore.json</c>). It is applied as
    /// <see cref="ApplyRoot(string)"/> applies a root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The platform's layout puts <c>rdc</c> and its files at the root, beside <c>rdcore.json</c>, <c>platform-settings.json</c> and
    /// <c>Extensions/</c>; the language server, the parse server and each extension are in a folder of their own under it. The root of a process
    /// at the root is therefore its own folder. It is never searched for: a folder that holds no manifest is not a platform root, and nothing
    /// above it is tried.
    /// </para>
    /// <para>
    /// The <c>RDCORE_PLATFORM_ROOT</c> variable that a process a user started inherited is not read, so a value set for another tree cannot point
    /// this tree's <c>rdc</c> at it; it is set over, and each process <c>rdc</c> starts has its root. <c>--platform-root</c> names another root: a
    /// test's, or an IDE launcher's. Call it first, before anything reads the root of <see cref="Default"/>.
    /// </para>
    /// <para>
    /// A process started as a server of another (<see cref="Client.RDCoreServerProcess.ModeEnvironmentVariable"/> names its mode: an environment
    /// host, an external host) is given its root by the process that started it, which sets the variable to its own root
    /// (<see cref="Client.RDCoreServerProcess"/>). That root is kept, wherever the executable is; a server given none is rooted in its own folder.
    /// </para>
    /// </remarks>
    /// <param name="args">The command-line arguments as received.</param>
    /// <returns>The full path of the root.</returns>
    /// <exception cref="NotAPlatformRootException">
    /// <paramref name="args"/> names no root, no process that started this one gave it one, and its own folder holds no <c>rdcore.json</c>.
    /// </exception>
    public static string ApplyOwnFolderAsRoot(string[] args)
    {
        if (Server.Configuration.SdkCommandLine.PlatformRootOf(args) is { } named)
        {
            return ApplyRoot(named);
        }

        if (Environment.GetEnvironmentVariable(Client.RDCoreServerProcess.ModeEnvironmentVariable) is { Length: > 0 }
            && Environment.GetEnvironmentVariable(RootEnvironmentVariable) is { } given && !string.IsNullOrWhiteSpace(given))
        {
            return ApplyRoot(given);
        }

        var own = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        return File.Exists(Path.Combine(own, PlatformManifest.FileName)) ? ApplyRoot(own) : throw new NotAPlatformRootException(own);
    }

    private readonly IFileSystem _fileSystem;
    private readonly Lazy<string> _root;

    /// <summary>
    /// Creates a platform environment whose <see cref="Root"/> is the one <see cref="RootEnvironmentVariable"/> names.
    /// </summary>
    /// <remarks>
    /// The variable is read when the root is first needed, not here, and the root is kept once it is read. A read that finds no root
    /// throws <see cref="PlatformRootNotGivenException"/> and keeps nothing, so a later read finds the variable if it has been set since.
    /// </remarks>
    /// <param name="fileSystem">The file system abstraction used to resolve and create paths.</param>
    public PlatformEnvironment(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;

        // publication only: a failed read is not kept, as an execution-and-publication Lazy keeps its exception.
        _root = new(GivenRoot, LazyThreadSafetyMode.PublicationOnly);
    }

    public string Root => _root.Value;

    // the root is given, never derived here from where the process's files are: a process at the root says its own folder is
    // (ApplyOwnFolderAsRoot), and a platform that is not where it is said to be is a fault to report, not to search for.
    private string GivenRoot()
        => Environment.GetEnvironmentVariable(RootEnvironmentVariable) is { } given && !string.IsNullOrWhiteSpace(given)
            ? _fileSystem.Path.GetFullPath(given)
            : throw new PlatformRootNotGivenException();

    // the roaming application data of the user: %APPDATA% on Windows, ~/.config elsewhere.
    public string UserDataDirectory
        => Environment.GetEnvironmentVariable(UserDataEnvironmentVariable) is { Length: > 0 } overridden
            ? _fileSystem.Path.GetFullPath(overridden)
            : _fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RDCore");

    public string ManifestPath => _fileSystem.Path.Combine(Root, PlatformManifest.FileName);

    public string AuditsDirectory => Resolve(AuditsFolder);

    public string LogsDirectory
    {
        get
        {
            var directory = Environment.GetEnvironmentVariable(LogsEnvironmentVariable) is { Length: > 0 } overridden
                ? _fileSystem.Path.GetFullPath(overridden)
                : UsersLogsDirectory();
            _fileSystem.Directory.CreateDirectory(directory);
            return directory;
        }
    }

    private string UsersLogsDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return _fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDCore", "Logs");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return _fileSystem.Path.Combine(home, "Library", "Logs", "RDCore");
        }

        // the specification ignores a relative XDG_STATE_HOME, as if it weren't set.
        var state = Environment.GetEnvironmentVariable("XDG_STATE_HOME") is { Length: > 0 } xdg && _fileSystem.Path.IsPathRooted(xdg)
            ? xdg
            : _fileSystem.Path.Combine(home, ".local", "state");
        return _fileSystem.Path.Combine(state, "rdcore", "logs");
    }

    public string Resolve(string rootRelativePath)
        => _fileSystem.Path.Combine(Root, rootRelativePath.Replace('/', _fileSystem.Path.DirectorySeparatorChar));
}
