using System.Text.Json;
using RDCore.SDK.Client;

namespace RDCore.SDK.Platform;

/// <summary>
/// A platform deployed in a shared platform root: its version, and its folder.
/// </summary>
/// <param name="Version">The platform's version.</param>
/// <param name="Root">The folder it is deployed in: <c>versions/&lt;version&gt;</c> under the shared root.</param>
public sealed record DeployedPlatform(PlatformVersion Version, string Root)
{
    /// <summary>
    /// The path of the platform's manifest, <c>rdcore.json</c>.
    /// </summary>
    public string Manifest => Path.Combine(Root, PlatformManifest.FileName);
}

/// <summary>
/// The folder every program of a user deploys the platform into, whichever comes first: <c>rdc install</c>, or an IDE add-in's
/// platform installer. Each deploys it by these rules, so that each finds what the other deployed.
/// </summary>
/// <remarks>
/// <para>
/// <b>A deployed version is never changed.</b> Each one is deployed beside the others, under <c>versions/&lt;version&gt;</c>, named by
/// <see cref="PlatformVersion.Text"/>. It is first written whole into a <c>.partial</c> folder beside it (<see cref="PartialRoot"/>), and
/// moved into place once it is a whole platform (<see cref="MoveIntoPlace"/>).
/// </para>
/// <para>
/// <b>One version is current</b>, as <c>installer.json</c> names it: <c>{"Current":"0.2.0","Previous":"0.1.0"}</c>. A version older than the
/// current one is deployed beside it, and not made current (<see cref="MakeCurrentUnlessOlder"/>).
/// </para>
/// <para>
/// <b>One deployment at a time</b>, across processes: <c>install.lock</c> is held open, unshared, for the whole of one (<see cref="LockAsync"/>).
/// </para>
/// <para>
/// <b>Old versions are removed whole, or not at all</b> (<see cref="RemoveOldVersions"/>): the current one and the one before it are kept. A
/// version is renamed out of the way first. On Windows the rename fails while a process runs from the version, which is then left where it
/// is until a later deployment finds it unused.
/// </para>
/// <para>
/// <b>A held version is not removed</b> (<see cref="Hold"/>): a program holds the version a server runs from for as long as the server runs,
/// whichever program deployed it, and a removal leaves that version where it is, on every system.
/// </para>
/// </remarks>
/// <param name="root">The root's folder, such as <see cref="ForThisUser"/>.</param>
public sealed class SharedPlatformRoot(string root)
{
    /// <summary>
    /// The name of the file that names the current version and the one before it: <c>installer.json</c>.
    /// </summary>
    public const string StateFileName = "installer.json";

    /// <summary>
    /// The name of the folder deployed versions are in: <c>versions</c>.
    /// </summary>
    public const string VersionsFolderName = "versions";

    /// <summary>
    /// The name of the file held for one deployment: <c>install.lock</c>.
    /// </summary>
    public const string LockFileName = "install.lock";

    /// <summary>
    /// The suffix of a version's folder while it is written: <c>.partial</c>.
    /// </summary>
    public const string PartialSuffix = ".partial";

    /// <summary>
    /// The suffix of a version's folder while it is removed: <c>.removing</c>.
    /// </summary>
    public const string RemovingSuffix = ".removing";

    private static readonly TimeSpan LockRetry = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The shared root of this user: <c>RDCore/Platform</c> in the local application data folder, which is RDCore's own, not any one
    /// program's.
    /// </summary>
    public static string ForThisUser =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDCore", "Platform");

    /// <summary>
    /// The root's folder.
    /// </summary>
    public string Root { get; } = root;

    /// <summary>
    /// The folder deployed versions are in.
    /// </summary>
    public string Versions => Path.Combine(Root, VersionsFolderName);

    /// <summary>
    /// The platform in use, or null when none is named, or the one named is gone, or is not a whole platform.
    /// </summary>
    public DeployedPlatform? Current
    {
        get
        {
            if (ReadState().Current is not { } current || !PlatformVersion.TryParse(current, out var version))
            {
                return null;
            }

            var folder = VersionRoot(version);
            return IsWhole(folder) ? new DeployedPlatform(version, folder) : null;
        }
    }

    /// <summary>
    /// The version <c>installer.json</c> names as the one before the current one, or null when it names none.
    /// </summary>
    public PlatformVersion? Previous => PlatformVersion.TryParse(ReadState().Previous, out var previous) ? previous : null;

    /// <summary>
    /// Where <paramref name="version"/> is, or would be, deployed.
    /// </summary>
    /// <param name="version">The version.</param>
    public string VersionRoot(PlatformVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return Path.Combine(Versions, version.Text);
    }

    /// <summary>
    /// Where <paramref name="version"/> is written before it is moved into place: its folder, with <see cref="PartialSuffix"/>.
    /// </summary>
    /// <param name="version">The version.</param>
    public string PartialRoot(PlatformVersion version) => VersionRoot(version) + PartialSuffix;

    /// <summary>
    /// Whether <paramref name="folder"/> is a whole platform: one whose <c>rdcore.json</c> names a language server it has, as
    /// <see cref="RDCoreLanguageServerLaunch.FindLanguageServer"/> finds it.
    /// </summary>
    /// <param name="folder">The folder of a platform's <c>rdcore.json</c>.</param>
    public static bool IsWhole(string folder)
    {
        try
        {
            _ = RDCoreLanguageServerLaunch.FindLanguageServer(folder);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Holds the root for one deployment, across processes: an exclusive handle on <c>install.lock</c>, released with the handle, and by
    /// the system if the process dies holding it. It creates the root when it is not there.
    /// </summary>
    /// <param name="waiting">Told once, when another deployment holds the root; null tells no one.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The hold: dispose it when the deployment is done.</returns>
    public async Task<IDisposable> LockAsync(Action? waiting, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, LockFileName);
        var told = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                if (!told)
                {
                    waiting?.Invoke();
                    told = true;
                }

                await Task.Delay(LockRetry, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Moves <paramref name="version"/>'s <see cref="PartialRoot"/>, written whole, into place, as the version's folder. A folder of that
    /// version that is not a whole platform is what an interrupted deployment left, and is replaced. Call it with the root held.
    /// </summary>
    /// <param name="version">The version written.</param>
    /// <exception cref="DirectoryNotFoundException">The partial folder is not there.</exception>
    /// <exception cref="FileNotFoundException">The partial folder has no manifest, or lacks the language server its manifest names.</exception>
    /// <exception cref="InvalidDataException">The partial folder's manifest is not one, or names a language server outside it.</exception>
    public void MoveIntoPlace(PlatformVersion version)
    {
        var partial = PartialRoot(version);
        _ = RDCoreLanguageServerLaunch.FindLanguageServer(partial);

        var folder = VersionRoot(version);
        DeleteFolder(folder);
        Directory.CreateDirectory(Versions);
        Directory.Move(partial, folder);
    }

    /// <summary>
    /// Makes <paramref name="version"/>, deployed, the platform in use, unless the current one is newer. The current one becomes the
    /// previous one. Call it with the root held.
    /// </summary>
    /// <param name="version">A deployed version.</param>
    /// <returns>Whether <paramref name="version"/> is the current version afterwards.</returns>
    public bool MakeCurrentUnlessOlder(PlatformVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (Current is { } current && current.Version.CompareTo(version) > 0)
        {
            return false;
        }

        var state = ReadState();
        if (state.Current != version.Text)
        {
            state = state with { Current = version.Text, Previous = state.Current };
        }

        // written whole, then moved over the old file, so a reader never sees half of it.
        var path = Path.Combine(Root, StateFileName);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state));
        File.Move(temporary, path, overwrite: true);
        return true;
    }

    /// <summary>
    /// Removes every deployed version but the current one, the one before it and <paramref name="keep"/>, each one whole: renamed out of
    /// the way with <see cref="RemovingSuffix"/> first, then deleted. On Windows the rename fails while a process runs from the version. A
    /// version that cannot be renamed or deleted is left for a later deployment. A folder whose name is not a version is left as it is. Call
    /// it with the root held.
    /// </summary>
    /// <remarks>A version that is held (<see cref="Hold"/>) is kept too, and so is a held one an earlier removal left renamed, which is put back.</remarks>
    /// <param name="keep">A version kept as well, such as the one just deployed.</param>
    public void RemoveOldVersions(PlatformVersion keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        if (!Directory.Exists(Versions))
        {
            return;
        }

        var state = ReadState();
        foreach (var folder in Directory.EnumerateDirectories(Versions))
        {
            var name = Path.GetFileName(folder);
            if (name.EndsWith(RemovingSuffix, StringComparison.Ordinal))
            {
                // a removal an earlier deployment could not finish: deleted, unless a lease taken before its rename still holds it.
                if (IsHeld(folder))
                {
                    TryMove(folder, folder[..^RemovingSuffix.Length]);
                }
                else
                {
                    TryDelete(folder);
                }

                continue;
            }

            if (name == state.Current || name == state.Previous || name == keep.Text || !PlatformVersion.TryParse(name, out _))
            {
                continue;
            }

            // renamed first, so that no lease is taken on it after it is checked: one taken before holds the renamed folder still.
            var removing = folder + RemovingSuffix;
            if (!TryMove(folder, removing))
            {
                // still in use.
                continue;
            }

            if (IsHeld(removing))
            {
                TryMove(removing, folder);
                continue;
            }

            TryDelete(removing);
        }
    }

    /// <summary>
    /// Keeps <paramref name="platform"/> deployed for as long as the lease is held: <see cref="RemoveOldVersions"/> leaves it where it is. Hold
    /// it while a server runs from it, and dispose of it once the server has stopped. Any number of programs can hold the same version, and
    /// the system releases a lease with the process that holds it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lease is the version's manifest, held open for reading and shared for reading only. A removal checks it by opening the manifest
    /// unshared, which fails while any lease holds it.
    /// </para>
    /// <para>
    /// On Windows the open manifest also stops the version's folder from being renamed or deleted by anything else. On Linux and macOS, .NET
    /// takes an advisory lock (<c>flock</c>) for a shared file: <see cref="RemoveOldVersions"/> honours it, and a program that deletes the
    /// folder without asking does not. There, a process run with <c>DOTNET_SYSTEM_IO_DISABLEFILELOCKING</c> set takes no lock, and its
    /// lease keeps nothing deployed.
    /// </para>
    /// </remarks>
    /// <param name="platform">The deployed version, such as <see cref="Current"/>.</param>
    /// <returns>The lease: dispose of it to release the version.</returns>
    /// <exception cref="FileNotFoundException">The version has no manifest.</exception>
    /// <exception cref="DirectoryNotFoundException">The version is not deployed.</exception>
    public static IDisposable Hold(DeployedPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);
        return new FileStream(platform.Manifest, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    // whether a lease holds the version at the folder: its manifest cannot be opened unshared. A folder with no manifest holds nothing.
    private static bool IsHeld(string folder)
    {
        try
        {
            using var unshared = new FileStream(Path.Combine(folder, PlatformManifest.FileName), FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool TryMove(string from, string to)
    {
        try
        {
            Directory.Move(from, to);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private InstallerState ReadState()
    {
        var path = Path.Combine(Root, StateFileName);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<InstallerState>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (JsonException)
        {
            // a state file nobody can read names no platform; the next deployment writes a good one.
            return new();
        }
    }

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // tried again after the next deployment.
        }
    }

    private static void DeleteFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    // read and written with System.Text.Json's defaults, as every program that deploys here writes it: {"Current":"0.2.0","Previous":"0.1.0"}.
    private sealed record InstallerState
    {
        public string? Current { get; init; }

        public string? Previous { get; init; }
    }
}
