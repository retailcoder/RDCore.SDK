using OmniSharp.Extensions.LanguageServer.Protocol;
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
/// <param name="Applied"><see langword="true"/> when every change was written.</param>
/// <param name="FailureReason">Why the edit was not applied, in the language of the user interface; <see langword="null"/> when it was.</param>
/// <param name="FailedChange">The index of the change that was not applied, among the edit's document changes (or its changes); <see langword="null"/> when it was.</param>
/// <param name="Written">The paths of the files written, in the order they were written: every file the edit changes, or those written before a write failed.</param>
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
/// encoding, edited, and written back in it (<see cref="ModuleFileText"/>).
/// </summary>
/// <remarks>
/// <para>
/// The edit is applied whole or not at all, as far as a file system allows: every change is made in memory first, and a change that cannot be made (a document
/// whose version is not the client's, a file that does not exist, a text its encoding cannot write, an edit that does not fit the text) refuses the edit before
/// any file is written. Then each file is written atomically; a write that fails stops there, and the result says which files were written before it.
/// </para>
/// <para>
/// Text edits only: creating, renaming and deleting files (resource operations) are refused, and a client that uses this says none in its
/// <c>workspace.workspaceEdit.resourceOperations</c> capability. Several changes of one document are made in the order the edit lists them.
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
    /// Applies a workspace edit to the files it changes.
    /// </summary>
    /// <param name="edit">The edit: its document changes, or else its changes.</param>
    /// <param name="versionOf">
    /// The version the client has of a document, <see langword="null"/> for one it has not opened. A change that names a version applies only to that version.
    /// </param>
    /// <param name="token">A token that cancels the edit; a file being written is written whole.</param>
    /// <returns>Whether the edit was applied, and the files written.</returns>
    public async Task<WorkspaceEditFilesResult> ApplyAsync(WorkspaceEdit edit, Func<DocumentUri, int?> versionOf, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(versionOf);

        // each document's text as the changes before leave it, and the index of the first change of it.
        var edited = new Dictionary<string, (ModuleFileText Text, int FirstChange)>(StringComparer.Ordinal);
        var order = new List<string>();
        var changes = ChangesOf(edit);
        for (var index = 0; index < changes.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            if (changes[index] is not { } change)
            {
                return Refused(Message("ResourceOperation"), index, []);
            }

            var (uri, version, edits) = change;
            var path = uri.GetFileSystemPath();
            if (version is { } expected && versionOf(uri) != expected)
            {
                return Refused(Message("Stale", path), index, []);
            }

            try
            {
                if (!edited.TryGetValue(path, out var held))
                {
                    if (!fileSystem.File.Exists(path))
                    {
                        return Refused(Message("NoFile", path), index, []);
                    }

                    held = (await ModuleFileText.ReadAsync(fileSystem, path, ansiCodePage, token), index);
                    order.Add(path);
                }

                edited[path] = (held.Text.Edit(edits), held.FirstChange);
            }
            catch (ModuleFileTextException exception)
            {
                return Refused(exception.Message, index, []);
            }
            catch (ArgumentException exception)
            {
                return Refused(Message("EditDoesNotFit", path, exception.Message), index, []);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Refused(Message("NotRead", path, exception.Message), index, []);
            }
        }

        var written = new List<string>();
        foreach (var path in order)
        {
            var (text, firstChange) = edited[path];
            try
            {
                await text.WriteAsync(fileSystem, path, CancellationToken.None);
                written.Add(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Refused(Message("NotWritten", path, exception.Message), firstChange, written);
            }
        }

        return new WorkspaceEditFilesResult(true, null, null, written);
    }

    // each change of the edit: a document, the version it is of, and its edits; null for a resource operation.
    private static List<(DocumentUri Uri, int? Version, IReadOnlyList<RefactorTextEdit> Edits)?> ChangesOf(WorkspaceEdit edit)
        => edit.DocumentChanges is { } documentChanges
            ? [.. documentChanges.Select(change => change.IsTextDocumentEdit
                ? ((DocumentUri, int?, IReadOnlyList<RefactorTextEdit>)?)(change.TextDocumentEdit!.TextDocument.Uri, change.TextDocumentEdit.TextDocument.Version, EditsOf(change.TextDocumentEdit.Edits))
                : null)]
            : [.. (edit.Changes ?? new Dictionary<DocumentUri, IEnumerable<TextEdit>>())
                .Select(change => ((DocumentUri, int?, IReadOnlyList<RefactorTextEdit>)?)(change.Key, null, EditsOf(change.Value)))];

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
