using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace RDCore.SDK.Server;

/// <summary>
/// A file whose new content a server wrote beside it, for a workspace edit to put in its place (<see cref="StagedFiles.Stage"/>).
/// </summary>
/// <remarks>
/// A workspace edit (<strong>LSP 3.17</strong> §WorkspaceEdit) can rename and delete a file, but cannot write a binary one. The edit that replaces the file
/// renames it to <see cref="Backup"/> and <see cref="Staged"/> to it (<see cref="Installing"/>), then makes its other changes, and deletes the backup last
/// (<see cref="Removing"/>). No change renames a file over one that exists, so a client that undoes a failed edit (<c>failureHandling</c> <c>undo</c> or
/// <c>transactional</c>) puts the file back as it was.
/// </remarks>
/// <param name="Target">The file the edit replaces.</param>
/// <param name="Staged">The new content, beside it: <c>&lt;file&gt;.&lt;id&gt;.rdcore</c>.</param>
/// <param name="Backup">Where the edit sets the file aside until it is done: <c>&lt;file&gt;.&lt;id&gt;.bak</c>.</param>
public sealed record class StagedFile(DocumentUri Target, DocumentUri Staged, DocumentUri Backup)
{
    /// <summary>
    /// The changes that put the new content in place: the file renamed to its backup, then the staged file renamed to the file. Neither overwrites a file.
    /// </summary>
    /// <returns>The two renames, in the order the edit makes them.</returns>
    public IEnumerable<WorkspaceEditDocumentChange> Installing() =>
    [
        new(new RenameFile { OldUri = Target, NewUri = Backup, Options = new RenameFileOptions { Overwrite = false, IgnoreIfExists = false } }),
        new(new RenameFile { OldUri = Staged, NewUri = Target, Options = new RenameFileOptions { Overwrite = false, IgnoreIfExists = false } }),
    ];

    /// <summary>
    /// The change that deletes the backup, which the edit makes after every other one.
    /// </summary>
    /// <returns>The deletion of <see cref="Backup"/>.</returns>
    public WorkspaceEditDocumentChange Removing() => new(new DeleteFile { Uri = Backup, Options = new DeleteFileOptions { IgnoreIfNotExists = false } });
}

/// <summary>
/// The files a server staged for workspace edits that replace files (<see cref="StagedFile"/>), and what keeps each of them from replacing a file that changed
/// after it was staged.
/// </summary>
/// <remarks>
/// <para>
/// A staged file holds content made of the file as it was. When the file changes before the client applies the edit, the staged file is deleted
/// (<see cref="Verify"/>): the edit's rename of it then fails, and a client that undoes a failed edit puts every other change back. The server calls
/// <see cref="Verify"/> when it hears of a change to a file (<c>workspace/didChangeWatchedFiles</c>, its own watcher) and when the client says it will rename
/// a staged file (<c>workspace/willRenameFiles</c>, whose filter <see cref="GlobPattern"/> is).
/// </para>
/// <para>
/// A file is staged for one edit at a time: staging another deletes the ones before it, and so does <see cref="Discard"/>. A staged file a server left behind
/// (it ended before the client applied the edit, or the client never did) is deleted by <see cref="Sweep"/>. Only files named as a staged file is named
/// (<see cref="IsStaged"/>) are ever deleted.
/// </para>
/// </remarks>
/// <param name="fileSystem">The file system the files are on.</param>
public sealed partial class StagedFiles(IFileSystem fileSystem) : IDisposable
{
    /// <summary>
    /// The extension of a staged file.
    /// </summary>
    public const string Extension = ".rdcore";

    /// <summary>
    /// The glob of every staged file, for the filter of a file operation the server registers (<c>workspace/willRenameFiles</c>).
    /// </summary>
    public const string GlobPattern = "**/*" + Extension;

    /// <summary>
    /// <c>workspace.fileOperations.willRename</c> as a server that verifies its staged files states it in its <c>initialize</c> result: the client asks before
    /// it renames a staged file (<see cref="Platform.Protocol.WillRenameWorkspaceFilesParams"/>), and the server answers once it has called
    /// <see cref="Verify"/>.
    /// </summary>
    public static WillRenameFileRegistrationOptions.StaticOptions WillRename => new()
    {
        Filters = new Container<FileOperationFilter>(new FileOperationFilter
        {
            Scheme = "file",
            Pattern = new FileOperationPattern
            {
                Glob = GlobPattern, Matches = FileOperationPatternKind.File, Options = new FileOperationPatternOptions { IgnoreCase = true },
            },
        }),
    };

    private readonly Lock _lock = new();
    private readonly List<Entry> _staged = [];

    // a staged file, and what the file it replaces was when it was staged: its length and hash, and where the edit puts it aside.
    private sealed record class Entry(StagedFile File, string Target, string Staged, string Backup, long Length, byte[] Hash);

    /// <summary>
    /// Whether a path is named as a staged file is: <c>&lt;file&gt;.&lt;id&gt;.rdcore</c>, where the id is 32 hexadecimal digits.
    /// </summary>
    /// <remarks>
    /// A workspace's loader and its watchers leave such a file alone.
    /// </remarks>
    /// <param name="path">The path, or a file name.</param>
    /// <returns><see langword="true"/> when its name is a staged file's.</returns>
    public static bool IsStaged(string path) => StagedName().IsMatch(Path.GetFileName(path));

    /// <summary>
    /// Writes the new content of a file beside it, for a workspace edit to put in its place.
    /// </summary>
    /// <remarks>
    /// The files staged before it are deleted, if they are still there. Nothing is staged for a file that is not on disk as <paramref name="original"/> is:
    /// content made of what the server read would replace what the file is now.
    /// </remarks>
    /// <param name="target">The file to replace, as the client spells its URI.</param>
    /// <param name="original">The content the new one was made of: the file as the server read it.</param>
    /// <param name="content">The new content.</param>
    /// <returns>
    /// The staged file, and the URIs the edit names, spelled as <paramref name="target"/> is; <see langword="null"/> when the file is not there, cannot be
    /// read, or is not <paramref name="original"/>.
    /// </returns>
    /// <exception cref="IOException">The staged file could not be written.</exception>
    public StagedFile? Stage(DocumentUri target, ReadOnlySpan<byte> original, ReadOnlySpan<byte> content)
    {
        var path = target.GetFileSystemPath();
        var id = Guid.NewGuid().ToString("N");
        var name = Path.GetFileName(path);
        var file = new StagedFile(target, Sibling(target, $"{name}.{id}{Extension}"), Sibling(target, $"{name}.{id}.bak"));
        var entry = new Entry(file, path, $"{path}.{id}{Extension}", $"{path}.{id}.bak", original.Length, SHA256.HashData(original));
        lock (_lock)
        {
            DiscardAll();
            if (!IsAsStaged(entry, path))
            {
                return null;
            }

            fileSystem.File.WriteAllBytes(entry.Staged, content.ToArray());
            _staged.Add(entry);
        }

        return file;
    }

    /// <summary>
    /// Deletes a staged file when the file it replaces differs from what it was made of.
    /// </summary>
    /// <remarks>
    /// The file is what it was made of while it has the length and hash it had when it was staged, or, once the edit has set it aside, while its backup has
    /// them. A staged file that is gone (the edit put it in place) is forgotten.
    /// </remarks>
    /// <param name="changed">A file that changed or is about to be renamed: a file a staged file replaces, or a staged file.</param>
    /// <returns><see langword="false"/> when a staged file of it was deleted; <see langword="true"/> otherwise.</returns>
    public bool Verify(DocumentUri changed)
    {
        var path = changed.GetFileSystemPath();
        lock (_lock)
        {
            var kept = true;
            foreach (var entry in _staged.Where(each => Same(each.Target, path) || Same(each.Staged, path)).ToList())
            {
                if (!fileSystem.File.Exists(entry.Staged))
                {
                    _staged.Remove(entry);
                }
                else if (!IsAsStaged(entry, entry.Target) && !IsAsStaged(entry, entry.Backup))
                {
                    Delete(entry.Staged);
                    _staged.Remove(entry);
                    kept = false;
                }
            }

            return kept;
        }
    }

    /// <summary>
    /// Deletes the staged files of a file that are on disk and that no edit of this server is waiting to put in place: what a server left behind.
    /// </summary>
    /// <param name="target">A file that staged files replace.</param>
    /// <returns>How many files were deleted.</returns>
    public int Sweep(DocumentUri target)
    {
        var path = target.GetFileSystemPath();
        var folder = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(folder) || !fileSystem.Directory.Exists(folder))
        {
            return 0;
        }

        var name = Path.GetFileName(path);
        lock (_lock)
        {
            var left = fileSystem.Directory.EnumerateFiles(folder, $"{name}.*{Extension}")
                .Where(candidate => IsStagedOf(Path.GetFileName(candidate), name) && !_staged.Any(entry => Same(entry.Staged, candidate)))
                .ToList();
            left.ForEach(Delete);
            return left.Count;
        }
    }

    /// <summary>
    /// Deletes every staged file that is still there, and forgets them: no edit puts one in place after it.
    /// </summary>
    public void Discard()
    {
        lock (_lock)
        {
            DiscardAll();
        }
    }

    /// <summary>
    /// Deletes every staged file that is still there (<see cref="Discard"/>).
    /// </summary>
    public void Dispose() => Discard();

    private void DiscardAll()
    {
        foreach (var entry in _staged.Where(entry => fileSystem.File.Exists(entry.Staged)))
        {
            Delete(entry.Staged);
        }

        _staged.Clear();
    }

    // a file that cannot be read (another program holds it) is not known to be what it was: its staged file does not replace it.
    private bool IsAsStaged(Entry entry, string path)
    {
        try
        {
            return fileSystem.File.Exists(path) && fileSystem.FileInfo.New(path).Length == entry.Length
                && SHA256.HashData(fileSystem.File.ReadAllBytes(path)).AsSpan().SequenceEqual(entry.Hash);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // a staged file is only ever deleted by its name: one that is not a staged file's is left as it is.
    private void Delete(string path)
    {
        if (IsStaged(path))
        {
            fileSystem.File.Delete(path);
        }
    }

    private static bool IsStagedOf(string fileName, string target)
        => fileName.Length == target.Length + 1 + 32 + Extension.Length && fileName.StartsWith(target + ".", StringComparison.OrdinalIgnoreCase)
            && IsStaged(fileName);

    private static bool Same(string path, string other) => string.Equals(Path.GetFullPath(path), Path.GetFullPath(other), StringComparison.OrdinalIgnoreCase);

    // the file of that name in the folder of a file, as the client spells the file's URI (its drive letter, its escapes).
    private static DocumentUri Sibling(DocumentUri file, string fileName) => DocumentUri.From(new Uri(file.ToUri(), Uri.EscapeDataString(fileName)));

    [GeneratedRegex(@"^.+\.[0-9a-f]{32}\.rdcore$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StagedName();
}
