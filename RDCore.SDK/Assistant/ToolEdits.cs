using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Assistant;

/// <summary>
/// A <see cref="WorkspaceEdit"/> (<strong>LSP 3.17</strong> <c>WorkspaceEdit</c>) as the tools that fix code say it: the change of each document it
/// makes, and the questions it asks before it is applied.
/// </summary>
public static class ToolEdits
{
    // what the edit does to a document: its text before, its text after, the document it is renamed to, and whether it creates or deletes it.
    private sealed record class Change(string Before, string After, DocumentUri? RenamedTo, bool CreatesOrDeletes = false);

    /// <summary>
    /// The change of each document an edit makes, by path.
    /// </summary>
    /// <param name="edit">The edit: its <c>documentChanges</c>, or else its <c>changes</c>.</param>
    /// <param name="textOf">The text of a document before the edit; <see langword="null"/> for one that does not exist.</param>
    /// <param name="pathOf">The path of a document, as <see cref="ToolFileChange.Path"/> says it.</param>
    /// <returns>
    /// Each document the edit changes, once: the text edits of its <c>TextDocumentEdit</c>s made in order, each to the text the one before it left. A
    /// document it renames has the path it is renamed to (<see cref="ToolFileChange.NewPath"/>), and the text edits of its new name, after the rename, are
    /// counted as its own. A document it creates is a diff from an empty text; one it deletes, a diff to one. A document whose edits leave its text as it was
    /// is left out.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Two text edits of a document overlap, or one is of a line its text does not have (<see cref="ArgumentOutOfRangeException"/>).
    /// </exception>
    public static IReadOnlyList<ToolFileChange> FilesOf(WorkspaceEdit edit, Func<DocumentUri, string?> textOf, Func<DocumentUri, string> pathOf)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(textOf);
        ArgumentNullException.ThrowIfNull(pathOf);

        var changes = new Dictionary<DocumentUri, Change>();
        var renamed = new Dictionary<DocumentUri, DocumentUri>();
        DocumentUri Key(DocumentUri document) => renamed.TryGetValue(document, out var original) ? original : document;
        Change Held(DocumentUri document)
        {
            if (!changes.TryGetValue(document, out var change))
            {
                var text = textOf(document) ?? string.Empty;
                changes[document] = change = new Change(text, text, null);
            }

            return change;
        }

        void Edit(DocumentUri document, IEnumerable<TextEdit> edits)
        {
            var key = Key(document);
            var change = Held(key);
            changes[key] = change with { After = DeclarationEdits.ApplyTo(change.After, [.. edits.Select(RefactorEditOf)]) };
        }

        foreach (var change in edit.DocumentChanges ?? [])
        {
            if (change.IsTextDocumentEdit)
            {
                Edit(change.TextDocumentEdit!.TextDocument.Uri, change.TextDocumentEdit.Edits);
            }
            else if (change.IsRenameFile)
            {
                var key = Key(change.RenameFile!.OldUri);
                changes[key] = Held(key) with { RenamedTo = change.RenameFile.NewUri };
                renamed[change.RenameFile.NewUri] = key;
            }
            else if (change.IsCreateFile)
            {
                changes[change.CreateFile!.Uri] = new Change(string.Empty, string.Empty, null, CreatesOrDeletes: true);
            }
            else if (change.IsDeleteFile)
            {
                var key = Key(change.DeleteFile!.Uri);
                changes[key] = Held(key) with { After = string.Empty, CreatesOrDeletes = true };
            }
        }

        if (edit.DocumentChanges is null)
        {
            foreach (var (document, edits) in edit.Changes ?? new Dictionary<DocumentUri, IEnumerable<TextEdit>>())
            {
                Edit(document, edits);
            }
        }

        return [.. changes
            .Select(change => (Path: pathOf(change.Key), change.Value))
            .Select(change => (File: new ToolFileChange
            {
                Path = change.Path,
                NewPath = change.Value.RenamedTo is { } renamedTo ? pathOf(renamedTo) : null,
                Diff = UnifiedDiff.Of(change.Path, change.Value.Before, change.Value.After),
            }, change.Value.CreatesOrDeletes))
            .Where(change => change.File.Diff.Length > 0 || change.File.NewPath is not null || change.CreatesOrDeletes)
            .Select(change => change.File)
            .OrderBy(change => change.Path, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The questions an edit asks before it is applied: each of its change annotations that needs a confirmation (<c>needsConfirmation</c>).
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <returns>The questions, each with the id of its annotation, its label and its description; none for an edit that asks nothing.</returns>
    public static IReadOnlyList<ToolEditQuestion> QuestionsOf(WorkspaceEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return [.. (edit.ChangeAnnotations ?? new Dictionary<ChangeAnnotationIdentifier, ChangeAnnotation>())
            .Where(annotation => annotation.Value.NeedsConfirmation == true)
            .Select(annotation => new ToolEditQuestion { Id = annotation.Key.Identifier, Label = annotation.Value.Label, Description = annotation.Value.Description })];
    }

    private static RefactorTextEdit RefactorEditOf(TextEdit edit) => new()
    {
        Range = new SourceRange(edit.Range.Start.Line, edit.Range.Start.Character, edit.Range.End.Line, edit.Range.End.Character),
        NewText = edit.NewText,
    };
}
