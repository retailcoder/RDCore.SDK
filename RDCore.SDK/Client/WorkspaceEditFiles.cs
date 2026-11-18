using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Workspace;
using System.Globalization;
using System.IO.Abstractions;
using System.Resources;

namespace RDCore.SDK.Client;

/// <summary>
/// What applying a workspace edit to files did (<see cref="WorkspaceEditFiles.ApplyAsync"/>): the answer to <c>workspace/applyEdit</c>
/// (<strong>LSP 3.17</strong> §ApplyWorkspaceEditResult), and the files that were written.
/// </summary>
/// <param name="Applied"><see langword="true"/> when every change was made.</param>
/// <param name="FailureReason">Why the edit was not applied, in the language of the user interface; <see langword="null"/> when it was.</param>
/// <param name="FailedChange">The index of the change that was not applied, among the edit's document changes (or its changes); <see langword="null"/> when it was.</param>
/// <param name="Written">
/// The paths of the files whose text was written, in the order they were written, each where the edit leaves it: every file whose text
/// the edit changes, when it was applied. When it was not, the paths the edit changed and could not put back as they were; empty when it
/// put everything back.
/// </param>
public sealed record class WorkspaceEditFilesResult(bool Applied, string? FailureReason, int? FailedChange, IReadOnlyList<string> Written)
{
    /// <summary>
    /// The answer to <c>workspace/applyEdit</c>.
    /// </summary>
    /// <returns>The response, with the reason and the change that failed, if any.</returns>
    public ApplyWorkspaceEditResponse ToResponse() => new() { Applied = Applied, FailureReason = FailureReason, FailedChange = FailedChange };
}

/// <summary>
/// Applies a workspace edit (<strong>LSP 3.17</strong> §WorkspaceEdit) to the files on disk, for a client that keeps no document open: each file is read in its
/// encoding, edited, and written back in it (<see cref="ModuleFileText"/>); a file is renamed or deleted where the edit says.
/// </summary>
/// <remarks>
/// <para>
/// The edit is applied whole or not at all. Every change is made in memory first, in the order the edit lists them: a change that cannot be
/// made (a document whose version is not the client's, a file that does not exist, a rename over a file that exists, a text its encoding
/// cannot write, an edit that does not fit the text) refuses the edit before any file is touched. Then the files are renamed and deleted, in
/// the edit's order, and each text is written, atomically, where the edit leaves its file. When one of those fails, what was done before it
/// is undone (<c>failureHandling</c> <c>undo</c>): each text is written back as it was, each rename is reversed, and each deleted file is
/// put back.
/// </para>
/// <para>
/// A file the edit deletes, or renames another over (<c>overwrite</c>), is set beside itself under another name, and deleted for good only
/// once every change was made, so undoing the edit never loses it. Creating a file and deleting a folder are refused. A client that applies
/// edits with this says what it takes with <see cref="Capability"/>.
/// </para>
/// </remarks>
/// <param name="fileSystem">The file system the files are on.</param>
/// <param name="ansiCodePage">
/// The code page a file without a byte order mark that is not valid UTF-8 is read and written in: the environment's (<c>Environment.AnsiCodePage</c>), or
/// <see cref="ModuleFileText.DefaultAnsiCodePage"/>.
/// </param>
public sealed class WorkspaceEditFiles(IFileSystem fileSystem, int ansiCodePage)
{
    private static readonly ResourceManager Messages = new("RDCore.SDK.Workspace.WorkspaceFileMessages", typeof(WorkspaceEditFiles).Assembly);

    /// <summary>
    /// What a client that applies workspace edits with <see cref="WorkspaceEditFiles"/> says it takes (<c>workspace.workspaceEdit</c>): document changes, the
    /// renaming and the deleting of files, and an edit that a failure undoes whole.
    /// </summary>
    public static WorkspaceEditCapability Capability => new()
    {
        DocumentChanges = true,
        ResourceOperations = new Container<ResourceOperationKind>(ResourceOperationKind.Rename, ResourceOperationKind.Delete),
        FailureHandling = FailureHandlingKind.Undo,
    };

    /// <summary>
    /// Applies a workspace edit to the files it changes.
    /// </summary>
    /// <param name="edit">The edit: its document changes, or else its changes.</param>
    /// <param name="versionOf">
    /// The version the client has of a document, <see langword="null"/> for one it has not opened. A change that names a version applies only to that version.
    /// </param>
    /// <param name="token">A token that cancels the edit before any file is touched; once one is, every change is made, or every one undone.</param>
    /// <returns>Whether the edit was applied, and the files written.</returns>
    public async Task<WorkspaceEditFilesResult> ApplyAsync(WorkspaceEdit edit, Func<DocumentUri, int?> versionOf, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(versionOf);

        var plan = new Plan(fileSystem);
        var changes = ChangesOf(edit);
        for (var index = 0; index < changes.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            if (await PlanAsync(plan, changes[index], index, versionOf, token) is { } refusal)
            {
                return Refused(refusal, index, []);
            }
        }

        return await ExecuteAsync(plan);
    }

    // the change made in memory; why it cannot be, when it cannot.
    private async Task<string?> PlanAsync(Plan plan, WorkspaceEditDocumentChange change, int index, Func<DocumentUri, int?> versionOf, CancellationToken token)
    {
        if (change.IsRenameFile)
        {
            var rename = change.RenameFile!;
            var (from, to) = (rename.OldUri.GetFileSystemPath(), rename.NewUri.GetFileSystemPath());
            if (!plan.Exists(from))
            {
                return Message("NoFile", from);
            }

            var overwrite = rename.Options?.Overwrite == true;
            if (plan.Exists(to) && !overwrite)
            {
                return rename.Options?.IgnoreIfExists == true ? null : Message("Exists", to, from);
            }

            plan.Rename(from, to, overwrite, index);
            return null;
        }

        if (change.IsDeleteFile)
        {
            var delete = change.DeleteFile!;
            var path = delete.Uri.GetFileSystemPath();
            if (fileSystem.Directory.Exists(path))
            {
                return Message("Folder", path);
            }

            if (!plan.Exists(path))
            {
                return delete.Options?.IgnoreIfNotExists == true ? null : Message("NoFile", path);
            }

            plan.Delete(path, index);
            return null;
        }

        if (!change.IsTextDocumentEdit)
        {
            return Message("ResourceOperation");
        }

        var document = change.TextDocumentEdit!;
        return await PlanTextAsync(plan, document.TextDocument.Uri, document.TextDocument.Version, EditsOf(document.Edits), index, versionOf, token);
    }

    private async Task<string?> PlanTextAsync(
        Plan plan, DocumentUri uri, int? version, IReadOnlyList<RefactorTextEdit> edits, int index, Func<DocumentUri, int?> versionOf, CancellationToken token)
    {
        var path = uri.GetFileSystemPath();
        if (version is { } expected && versionOf(uri) != expected)
        {
            return Message("Stale", path);
        }

        try
        {
            if (!plan.Texts.TryGetValue(path, out var held))
            {
                if (!plan.Exists(path))
                {
                    return Message("NoFile", path);
                }

                held = (await ModuleFileText.ReadAsync(fileSystem, plan.SourceOf(path), ansiCodePage, token), index);
                plan.Order.Add(path);
            }

            plan.Texts[path] = (held.Text.Edit(edits), held.FirstChange);
            return null;
        }
        catch (ModuleFileTextException exception)
        {
            return exception.Message;
        }
        catch (ArgumentException exception)
        {
            return Message("EditDoesNotFit", path, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Message("NotRead", path, exception.Message);
        }
    }

    // the renames and the deletes, in the edit's order, then the texts where the edit leaves them; what fails undoes what was done before it.
    private async Task<WorkspaceEditFilesResult> ExecuteAsync(Plan plan)
    {
        var undo = new Stack<(string Path, Action Undo)>();
        var aside = new List<string>();
        var written = new List<string>();
        var change = -1;
        Func<string, string> failure = why => why;
        try
        {
            foreach (var (from, to, overwrite, index) in plan.Moves)
            {
                change = index;
                if (to is null)
                {
                    failure = why => Message("NotDeleted", from, why);
                    SetAside(from, undo, aside);
                    continue;
                }

                failure = why => Message("NotRenamed", from, to, why);
                if (overwrite && fileSystem.File.Exists(to))
                {
                    SetAside(to, undo, aside);
                }

                fileSystem.File.Move(from, to);
                undo.Push((from, () => fileSystem.File.Move(to, from)));
            }

            foreach (var path in plan.Order.Distinct(StringComparer.OrdinalIgnoreCase).Where(plan.Texts.ContainsKey))
            {
                var (text, firstChange) = plan.Texts[path];
                change = firstChange;
                failure = why => Message("NotWritten", path, why);
                var before = await fileSystem.File.ReadAllBytesAsync(path, CancellationToken.None);
                await text.WriteAsync(fileSystem, path, CancellationToken.None);
                undo.Push((path, () => fileSystem.File.WriteAllBytes(path, before)));
                written.Add(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Refused(failure(exception.Message), change, Undo(undo));
        }

        // every change was made: what was set aside is deleted for good, and one that cannot be is left where it was set.
        foreach (var path in aside)
        {
            try
            {
                fileSystem.File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        return new WorkspaceEditFilesResult(true, null, null, written);
    }

    // a file deleted, or renamed over, until every change is made: beside itself, under a name no other file has.
    private void SetAside(string path, Stack<(string Path, Action Undo)> undo, List<string> aside)
    {
        var set = $"{path}.{Guid.NewGuid():N}.rdcore-aside";
        fileSystem.File.Move(path, set);
        undo.Push((path, () => fileSystem.File.Move(set, path)));
        aside.Add(set);
    }

    // what was done, undone from the last; the paths that could not be put back as they were.
    private static List<string> Undo(Stack<(string Path, Action Undo)> undo)
    {
        var left = new List<string>();
        while (undo.TryPop(out var done))
        {
            try
            {
                done.Undo();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                left.Add(done.Path);
            }
        }

        return left;
    }

    // the files as the changes before leave them: where each text is read from, which exist, the texts edited, and the renames and deletes to make.
    private sealed class Plan(IFileSystem fileSystem)
    {
        private readonly Dictionary<string, string> _sources = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _added = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _removed = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, (ModuleFileText Text, int FirstChange)> Texts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Order { get; } = [];

        // a rename, or a delete (To is null), and the change it is.
        public List<(string From, string? To, bool Overwrite, int Change)> Moves { get; } = [];

        public bool Exists(string path) => !_removed.Contains(path) && (_added.Contains(path) || fileSystem.File.Exists(path));

        public string SourceOf(string path) => _sources.GetValueOrDefault(path, path);

        public void Rename(string from, string to, bool overwrite, int change)
        {
            _sources[to] = SourceOf(from);
            _sources.Remove(from);
            if (Texts.Remove(from, out var text))
            {
                Texts[to] = text;
                Order[Order.IndexOf(from)] = to;
            }

            Texts.Remove(to, out _);
            _removed.Add(from);
            _added.Remove(from);
            _added.Add(to);
            _removed.Remove(to);
            Moves.Add((from, to, overwrite, change));
        }

        public void Delete(string path, int change)
        {
            Texts.Remove(path);
            _sources.Remove(path);
            _removed.Add(path);
            _added.Remove(path);
            Moves.Add((path, null, false, change));
        }
    }

    // each change of the edit, as document changes: a text edit of an edit's changes names no version.
    private static List<WorkspaceEditDocumentChange> ChangesOf(WorkspaceEdit edit)
        => edit.DocumentChanges is { } documentChanges
            ? [.. documentChanges]
            : [.. (edit.Changes ?? new Dictionary<DocumentUri, IEnumerable<TextEdit>>())
                .Select(change => new WorkspaceEditDocumentChange(new TextDocumentEdit
                {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = change.Key },
                    Edits = new TextEditContainer(change.Value),
                }))];

    private static IReadOnlyList<RefactorTextEdit> EditsOf(IEnumerable<TextEdit> edits)
        => [.. edits.Select(edit => new RefactorTextEdit
        {
            Range = new SourceRange(edit.Range.Start.Line, edit.Range.Start.Character, edit.Range.End.Line, edit.Range.End.Character),
            NewText = edit.NewText,
        })];

    private static WorkspaceEditFilesResult Refused(string reason, int change, IReadOnlyList<string> written) => new(false, reason, change, written);

    private static string Message(string name, params object[] arguments)
        => string.Format(CultureInfo.CurrentUICulture, Messages.GetString(name, CultureInfo.CurrentUICulture)!, arguments);
}
