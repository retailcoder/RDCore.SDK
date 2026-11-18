using RDCore.SDK.Model.Symbols;
using System.Collections.Immutable;
using System.Globalization;
using System.IO.Abstractions;
using System.Resources;
using System.Text.Json;

namespace RDCore.SDK.Workspace;

/// <summary>
/// A folder of a workspace that its <c>.rdworkspace</c> lists (<strong>RD-VBAL §2.2.2</strong>): the folder of a project, directly under the workspace root.
/// </summary>
/// <param name="Name">The name of the folder, as the file lists it.</param>
/// <param name="Path">The absolute path of the folder.</param>
public sealed record class ListedWorkspaceFolder(string Name, string Path)
{
    /// <summary>
    /// The folder as its <c>file</c> URI (<see cref="SymbolUri.Root"/>): what a client gives a language server for it (<strong>LSP 3.17</strong>
    /// <c>workspaceFolders</c>).
    /// </summary>
    public Uri Uri => SymbolUri.Root(Path);
}

/// <summary>
/// Why an entry of a <c>.rdworkspace</c>'s folders is not loaded.
/// </summary>
public enum WorkspaceFolderRefusal
{
    /// <summary>
    /// The entry is an absolute path: a workspace lists only folders under its own root.
    /// </summary>
    Absolute,

    /// <summary>
    /// The entry has a <c>..</c> in it, which climbs out of the workspace root.
    /// </summary>
    OutsideRoot,

    /// <summary>
    /// The entry is not the name of a folder directly under the workspace root: it has a path separator in it, or it is <c>.</c>.
    /// </summary>
    NotDirectlyUnderRoot,

    /// <summary>
    /// The entry names a folder the file lists before it, in any letter case.
    /// </summary>
    Duplicate,

    /// <summary>
    /// There is no folder of that name under the workspace root.
    /// </summary>
    Missing,
}

/// <summary>
/// An entry of a <c>.rdworkspace</c>'s folders that is not loaded, and why.
/// </summary>
/// <param name="Reason">Why the entry is not loaded.</param>
/// <param name="Entry">The entry, as the file writes it.</param>
public sealed record class RefusedWorkspaceFolder(WorkspaceFolderRefusal Reason, string Entry)
{
    private static readonly ResourceManager Messages = new("RDCore.SDK.Workspace.WorkspaceFolderMessages", typeof(RefusedWorkspaceFolder).Assembly);

    /// <summary>
    /// The refusal as the user is told it, naming the entry and the file.
    /// </summary>
    /// <param name="workspaceFile">The path of the <c>.rdworkspace</c> that lists the entry.</param>
    /// <param name="culture">The language of the reader; the current UI culture by default.</param>
    /// <returns>The message.</returns>
    public string ToMessage(string workspaceFile, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return string.Format(culture, Messages.GetString($"WorkspaceFolder_{Reason}", culture) ?? "{0}: {1}", Entry, workspaceFile);
    }
}

/// <summary>
/// The folders a workspace's <c>.rdworkspace</c> lists, in its order, the entries it lists that are not loaded, and the folder that is the primary project
/// (<strong>RD-VBAL §2.2.2</strong>).
/// </summary>
/// <remarks>
/// <para>
/// A workspace lists only folders directly under its own root, by their names. An entry that is an absolute path, that is <c>.</c>, that has a <c>..</c> or a
/// path separator in it, that names a folder listed before it, or that names a folder that is not there, is not loaded, and is in <see cref="Refused"/>.
/// </para>
/// <para>
/// The <see cref="Folders"/> keep the order of the file, which decides what each folder is to the others. The <see cref="Primary"/> folder is the project a
/// client opened: the folder it was opened on, when the file lists it, or the first folder when it was opened on the workspace root.
/// </para>
/// </remarks>
public sealed record class WorkspaceFolderList
{
    /// <summary>
    /// The absolute path of the workspace root: the folder of the <c>.rdworkspace</c>.
    /// </summary>
    public required string Root { get; init; }

    /// <summary>
    /// The absolute path of the <c>.rdworkspace</c>.
    /// </summary>
    public required string WorkspaceFilePath { get; init; }

    /// <summary>
    /// The folders the file lists that are loaded, in the order it lists them.
    /// </summary>
    public ImmutableArray<ListedWorkspaceFolder> Folders { get; init; } = [];

    /// <summary>
    /// The entries of the file's folders that are not loaded, in the order it lists them.
    /// </summary>
    public ImmutableArray<RefusedWorkspaceFolder> Refused { get; init; } = [];

    /// <summary>
    /// The folder of the project the client opened, one of <see cref="Folders"/>; <see langword="null"/> when no folder is loaded.
    /// </summary>
    public ListedWorkspaceFolder? Primary { get; init; }

    /// <summary>
    /// Reads the workspace a client opens on <paramref name="folder"/>: the workspace whose root it is, or the one whose root is its parent and whose
    /// <c>.rdworkspace</c> lists it.
    /// </summary>
    /// <param name="fileSystem">The file system the workspace is on.</param>
    /// <param name="folder">The folder the client opens, as an absolute path or relative to the current directory.</param>
    /// <returns>
    /// The workspace; <see langword="null"/> when neither <paramref name="folder"/> nor its parent has a <c>.rdworkspace</c> that can be read, and when
    /// its parent's does not list it. The folder is then opened on its own.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> or <paramref name="folder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="folder"/> is empty or only white space.</exception>
    /// <exception cref="IOException">A <c>.rdworkspace</c> is there but could not be read.</exception>
    public static WorkspaceFolderList? Read(IFileSystem fileSystem, string folder)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var full = fileSystem.Path.TrimEndingDirectorySeparator(fileSystem.Path.GetFullPath(folder));
        if (ReadFile(fileSystem, full) is { } own)
        {
            return Of(fileSystem, full, own, opened: null);
        }

        if (fileSystem.Path.GetDirectoryName(full) is { Length: > 0 } parent && ReadFile(fileSystem, parent) is { } workspace)
        {
            var list = Of(fileSystem, parent, workspace, opened: fileSystem.Path.GetFileName(full));
            return list.Primary is null ? null : list;
        }

        return null;
    }

    private sealed record class Content(string Path, WorkspaceFile File);

    // the .rdworkspace of a folder, when it has one that can be read: a file that is not JSON, or not a JSON object, makes no workspace.
    private static Content? ReadFile(IFileSystem fileSystem, string root)
    {
        var path = fileSystem.Path.Combine(root, WorkspaceFile.FileName);
        if (!fileSystem.File.Exists(path))
        {
            return null;
        }

        using var stream = fileSystem.File.OpenRead(path);
        try
        {
            return JsonSerializer.Deserialize<WorkspaceFile>(stream) is { } file ? new(path, file) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WorkspaceFolderList Of(IFileSystem fileSystem, string root, Content content, string? opened)
    {
        var folders = ImmutableArray.CreateBuilder<ListedWorkspaceFolder>();
        var refused = ImmutableArray.CreateBuilder<RefusedWorkspaceFolder>();
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in content.File.Folders)
        {
            var path = fileSystem.Path.Combine(root, entry);
            WorkspaceFolderRefusal? reason = IsAbsolute(fileSystem, entry) ? WorkspaceFolderRefusal.Absolute
                : WorkspacePaths.Climbs(entry) ? WorkspaceFolderRefusal.OutsideRoot
                : entry is "." || entry.IndexOfAny(['/', '\\']) >= 0 ? WorkspaceFolderRefusal.NotDirectlyUnderRoot
                : !listed.Add(entry) ? WorkspaceFolderRefusal.Duplicate
                : !fileSystem.Directory.Exists(path) ? WorkspaceFolderRefusal.Missing
                : null;

            if (reason is { } refusal)
            {
                refused.Add(new RefusedWorkspaceFolder(refusal, entry));
            }
            else
            {
                folders.Add(new ListedWorkspaceFolder(entry, path));
            }
        }

        var loaded = folders.ToImmutable();
        return new WorkspaceFolderList
        {
            Root = root,
            WorkspaceFilePath = content.Path,
            Folders = loaded,
            Refused = refused.ToImmutable(),
            Primary = opened is null
                ? loaded.FirstOrDefault()
                : loaded.FirstOrDefault(folder => string.Equals(folder.Name, opened, WorkspacePaths.Comparison)),
        };
    }

    // an absolute path on any platform the file may have been written on: rooted (/, \, \\server), or with a drive (C:).
    private static bool IsAbsolute(IFileSystem fileSystem, string entry)
        => fileSystem.Path.IsPathRooted(entry) || entry is ['/' or '\\', ..] || entry is [_, ':', ..];
}
