using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace RDCore.SDK.Server.ProtocolExtensions;

/// <summary>
/// A <see cref="WorkspaceEdit"/> as a client can apply it (<strong>LSP 3.17</strong> §WorkspaceEdit): its <c>documentChanges</c> to a client that said it
/// takes them (<c>workspace.workspaceEdit.documentChanges</c>), and its <c>changes</c> to any other; its change annotations to a client that said it takes
/// those (<c>changeAnnotationSupport</c>).
/// </summary>
/// <remarks>
/// <para>
/// A client that did not say it takes <c>documentChanges</c> reads <c>changes</c> alone, and applies none of an edit that has none. <c>changes</c> are the
/// edits of each document by its URI: they cannot say which version of a document they are edits of, which the spec allows - such a client applies them to
/// the text it has - nor that a file is created, renamed or deleted.
/// </para>
/// <para>
/// An edit that has both is giving its <c>changes</c> as the alternative of its <c>documentChanges</c>, for a client that does not take them: they are the
/// same edits, and one client is sent one of them.
/// </para>
/// </remarks>
public static class WorkspaceEditExtensions
{
    /// <summary>
    /// Whether the client said it takes the <c>documentChanges</c> of a <see cref="WorkspaceEdit"/>.
    /// </summary>
    /// <param name="client">What the client said it can do, as it initialized; none when it is not known.</param>
    /// <returns><see langword="true"/> when it said <c>workspace.workspaceEdit.documentChanges</c>; <see langword="false"/> when it did not, or is not known.</returns>
    public static bool TakesDocumentChanges(this ClientCapabilities? client)
        => client?.Workspace?.WorkspaceEdit is { IsSupported: true, Value.DocumentChanges: true };

    /// <summary>
    /// Whether the client said it takes the change annotations of a <see cref="WorkspaceEdit"/>: an edit it asks its user to confirm, a label it shows.
    /// </summary>
    /// <param name="client">What the client said it can do, as it initialized; none when it is not known.</param>
    /// <returns><see langword="true"/> when it said <c>workspace.workspaceEdit.changeAnnotationSupport</c>; <see langword="false"/> when it did not, or is not known.</returns>
    public static bool TakesChangeAnnotations(this ClientCapabilities? client)
        => client?.Workspace?.WorkspaceEdit is { IsSupported: true, Value.ChangeAnnotationSupport: not null };

    /// <summary>
    /// Whether the client takes an edit that replaces a file (<see cref="StagedFile"/>) and puts it back when the edit fails: <c>documentChanges</c> that
    /// rename and delete files, and a failure that undoes the whole edit.
    /// </summary>
    /// <param name="client">What the client said it can do, as it initialized; none when it is not known.</param>
    /// <returns>
    /// <see langword="true"/> when it said <c>documentChanges</c>, <c>resourceOperations</c> with <c>rename</c> and <c>delete</c>, and <c>failureHandling</c>
    /// <c>transactional</c> or <c>undo</c>; <see langword="false"/> otherwise. <c>textOnlyTransactional</c> and <c>abort</c> would leave the file changed
    /// and the text not, or the other way round.
    /// </returns>
    public static bool TakesFileReplacement(this ClientCapabilities? client)
        => client?.Workspace?.WorkspaceEdit is
            { IsSupported: true, Value: { DocumentChanges: true, ResourceOperations: { } kinds, FailureHandling: { } failure } }
            && kinds.Contains(ResourceOperationKind.Rename) && kinds.Contains(ResourceOperationKind.Delete)
            && (failure == FailureHandlingKind.Transactional || failure == FailureHandlingKind.Undo);

    /// <summary>
    /// The edit as the client can apply it.
    /// </summary>
    /// <param name="edit">The edit, as the server made it.</param>
    /// <param name="client">What the client said it can do, as it initialized; none when it is not known, which is a client that takes neither
    /// <c>documentChanges</c> nor change annotations.</param>
    /// <returns>
    /// <para>
    /// To a client that takes <c>documentChanges</c>: the edit, when it says nothing the client did not say it takes. To any other: its <c>changes</c>
    /// alone, when it has them; otherwise the text edits of its <c>documentChanges</c> as <c>changes</c>, by document, in the order they were written.
    /// </para>
    /// <para>
    /// To a client that does not take change annotations - and in <c>changes</c>, which cannot carry them - an edit whose annotation asks the user to
    /// confirm it is left out, being one the user would only have applied if they chose to; the others are sent without their annotation.
    /// </para>
    /// </returns>
    /// <exception cref="WorkspaceEditNotSupportedException">The edit creates, renames or deletes a file, and the client did not say it does that
    /// (<c>documentChanges</c> and <c>resourceOperations</c>); or, to a client that does not take <c>documentChanges</c>, it edits one document in more than
    /// one <c>TextDocumentEdit</c> - the second's ranges are of the text the first made, which <c>changes</c> cannot say.</exception>
    public static WorkspaceEdit For(this WorkspaceEdit edit, ClientCapabilities? client)
    {
        var annotations = client.TakesChangeAnnotations() ? null : NeedingConfirmation(edit);
        if (client.TakesDocumentChanges())
        {
            if (edit.DocumentChanges is { } changes && changes.Any(change => !change.IsTextDocumentEdit && !TakesResourceOperation(client, change)))
            {
                throw new WorkspaceEditNotSupportedException(Exceptions.WorkspaceEdit_FileOperationNotSupported);
            }

            return annotations is null || !IsAnnotated(edit) ? edit : edit with
            {
                DocumentChanges = edit.DocumentChanges is { } documentChanges
                    ? new Container<WorkspaceEditDocumentChange>(documentChanges.Select(change => change.IsTextDocumentEdit
                        ? new WorkspaceEditDocumentChange(change.TextDocumentEdit! with { Edits = new TextEditContainer(Plain(change.TextDocumentEdit.Edits, annotations)) })
                        : change))
                    : null,
                Changes = Plain(edit.Changes, annotations),
                ChangeAnnotations = null,
            };
        }

        // changes cannot carry an annotation: what needs confirming is left out, whatever the client takes.
        annotations ??= NeedingConfirmation(edit);
        if (edit.Changes is { Count: > 0 })
        {
            return edit with { DocumentChanges = null, Changes = Plain(edit.Changes, annotations), ChangeAnnotations = null };
        }

        if (edit.DocumentChanges is not { } textChanges)
        {
            return edit;
        }

        if (textChanges.Any(change => !change.IsTextDocumentEdit))
        {
            throw new WorkspaceEditNotSupportedException(Exceptions.WorkspaceEdit_FileOperationNotSupported);
        }

        var byDocument = new Dictionary<DocumentUri, IEnumerable<TextEdit>>();
        foreach (var document in textChanges.Select(change => change.TextDocumentEdit!))
        {
            if (!byDocument.TryAdd(document.TextDocument.Uri, Plain(document.Edits, annotations)))
            {
                throw new WorkspaceEditNotSupportedException(string.Format(Exceptions.WorkspaceEdit_DocumentEditedTwice, document.TextDocument.Uri));
            }
        }

        return edit with { DocumentChanges = null, Changes = byDocument, ChangeAnnotations = null };
    }

    /// <summary>
    /// The edit with the changes of an annotation confirmed: they are applied like its other changes, with nothing asked.
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <param name="annotation">The annotation of the changes confirmed.</param>
    /// <returns>The edit, with the annotation's <c>needsConfirmation</c> false.</returns>
    public static WorkspaceEdit Confirmed(this WorkspaceEdit edit, ChangeAnnotationIdentifier annotation)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return edit with
        {
            ChangeAnnotations = edit.ChangeAnnotations?.ToDictionary(each => each.Key,
                each => each.Key == annotation ? each.Value with { NeedsConfirmation = false } : each.Value),
        };
    }

    /// <summary>
    /// The edit without the changes of an annotation: those its user chose not to make.
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <param name="annotation">The annotation of the changes left out.</param>
    /// <returns>
    /// The edit without the annotation and without what it annotates: its text edits, and the files it creates, renames or deletes. A
    /// <c>TextDocumentEdit</c> left with no edit stays, empty.
    /// </returns>
    public static WorkspaceEdit Without(this WorkspaceEdit edit, ChangeAnnotationIdentifier annotation)
    {
        ArgumentNullException.ThrowIfNull(edit);
        bool Kept(TextEdit each) => each is not AnnotatedTextEdit { AnnotationId: var id } || id != annotation;
        return edit with
        {
            DocumentChanges = edit.DocumentChanges is { } changes
                ? new Container<WorkspaceEditDocumentChange>(changes
                    .Where(change => change.IsTextDocumentEdit || AnnotationOf(change) != annotation)
                    .Select(change => change.IsTextDocumentEdit
                        ? new WorkspaceEditDocumentChange(change.TextDocumentEdit! with { Edits = new TextEditContainer(change.TextDocumentEdit.Edits.Where(Kept)) })
                        : change))
                : null,
            Changes = edit.Changes?.ToDictionary(change => change.Key, change => (IEnumerable<TextEdit>)[.. change.Value.Where(Kept)]),
            ChangeAnnotations = edit.ChangeAnnotations?.Where(each => each.Key != annotation).ToDictionary() is { Count: > 0 } kept ? kept : null,
        };
    }

    /// <summary>
    /// The edit as the questions it asks are answered: the changes of each annotation that needs confirmation (<c>needsConfirmation</c>) confirmed when it is
    /// answered yes, and left out when it is not.
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <param name="accepted">The identifier of each annotation answered yes; an identifier the edit has no annotation of is ignored.</param>
    /// <returns>The edit, with no annotation that needs confirmation (<see cref="Confirmed"/>, <see cref="Without"/>).</returns>
    public static WorkspaceEdit Answered(this WorkspaceEdit edit, IReadOnlyCollection<string> accepted)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(accepted);
        foreach (var annotation in NeedingConfirmation(edit))
        {
            edit = accepted.Contains(annotation.Identifier) ? edit.Confirmed(annotation) : edit.Without(annotation);
        }

        return edit;
    }

    // the annotation of a file the edit creates, renames or deletes.
    private static ChangeAnnotationIdentifier? AnnotationOf(WorkspaceEditDocumentChange change)
        => change.IsRenameFile ? change.RenameFile!.AnnotationId : change.IsCreateFile ? change.CreateFile!.AnnotationId : change.DeleteFile?.AnnotationId;

    // whether anything of the edit is annotated: its annotations, or a text edit that names one.
    private static bool IsAnnotated(WorkspaceEdit edit)
        => edit.ChangeAnnotations is { Count: > 0 }
            || edit.Changes?.Values.Any(edits => edits.Any(each => each is AnnotatedTextEdit)) == true
            || edit.DocumentChanges?.Any(change => change.IsTextDocumentEdit && change.TextDocumentEdit!.Edits.Any(each => each is AnnotatedTextEdit)) == true;

    // the annotations whose edits the user is asked to confirm.
    private static HashSet<ChangeAnnotationIdentifier> NeedingConfirmation(WorkspaceEdit edit)
        => [.. (edit.ChangeAnnotations ?? new Dictionary<ChangeAnnotationIdentifier, ChangeAnnotation>())
            .Where(annotation => annotation.Value.NeedsConfirmation == true)
            .Select(annotation => annotation.Key)];

    // the edits with no annotation: one that needs confirming is left out, and the others are what they say.
    private static List<TextEdit> Plain(IEnumerable<TextEdit> edits, HashSet<ChangeAnnotationIdentifier> needingConfirmation) => [.. edits
        .Where(edit => edit is not AnnotatedTextEdit { AnnotationId: var id } || !needingConfirmation.Contains(id))
        .Select(edit => edit is AnnotatedTextEdit ? new TextEdit { Range = edit.Range, NewText = edit.NewText } : edit)];

    private static IDictionary<DocumentUri, IEnumerable<TextEdit>>? Plain(IDictionary<DocumentUri, IEnumerable<TextEdit>>? changes,
        HashSet<ChangeAnnotationIdentifier> needingConfirmation)
        => changes?.ToDictionary(change => change.Key, change => (IEnumerable<TextEdit>)Plain(change.Value, needingConfirmation));

    // a file created, renamed or deleted, of a kind the client said it does.
    private static bool TakesResourceOperation(ClientCapabilities? client, WorkspaceEditDocumentChange change)
        => client.TakesResourceOperation(change.IsCreateFile ? ResourceOperationKind.Create : change.IsRenameFile ? ResourceOperationKind.Rename : ResourceOperationKind.Delete);

    /// <summary>
    /// Whether the client said it creates, renames or deletes files, as the <c>documentChanges</c> of a <see cref="WorkspaceEdit"/> say.
    /// </summary>
    /// <param name="client">What the client said it can do, as it initialized; none when it is not known.</param>
    /// <param name="kind">What is done to a file.</param>
    /// <returns>
    /// <see langword="true"/> when it said <c>documentChanges</c> and <c>resourceOperations</c> with <paramref name="kind"/>; <see langword="false"/> when it
    /// did not, or is not known.
    /// </returns>
    public static bool TakesResourceOperation(this ClientCapabilities? client, ResourceOperationKind kind)
        => client?.Workspace?.WorkspaceEdit is { IsSupported: true, Value: { DocumentChanges: true, ResourceOperations: { } kinds } } && kinds.Contains(kind);

    /// <summary>
    /// Whether the client said it applies the edits a server asks it to (<c>workspace/applyEdit</c>).
    /// </summary>
    /// <param name="client">What the client said it can do, as it initialized; none when it is not known.</param>
    /// <returns><see langword="true"/> when it said <c>workspace.applyEdit</c>; <see langword="false"/> when it did not, or is not known.</returns>
    public static bool AppliesEdits(this ClientCapabilities? client) => client?.Workspace?.ApplyEdit is { IsSupported: true, Value: true };

    /// <summary>
    /// The edit without the files it renames, for a client that does not rename files: the documents keep their names, and their texts are edited where they
    /// are.
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <returns>
    /// The edit without its <c>RenameFile</c> changes and without the <c>TextDocumentEdit</c>s of the documents they rename to: those edit a document once it
    /// is renamed, and a document that keeps its name has none. The edit itself when it renames no file.
    /// </returns>
    public static WorkspaceEdit WithoutFileRenames(this WorkspaceEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (edit.DocumentChanges is not { } changes || !changes.Any(change => change.IsRenameFile))
        {
            return edit;
        }

        var renamedTo = changes.Where(change => change.IsRenameFile).Select(change => change.RenameFile!.NewUri).ToHashSet();
        return edit with
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(changes.Where(change
                => !change.IsRenameFile && !(change.IsTextDocumentEdit && renamedTo.Contains(change.TextDocumentEdit!.TextDocument.Uri)))),
        };
    }
}

/// <summary>
/// A <see cref="WorkspaceEdit"/> that says what the client it is for did not say it can apply (<see cref="WorkspaceEditExtensions.For"/>): what asked for it
/// is answered that it can't be done, and why, rather than an edit the client applies none of.
/// </summary>
/// <param name="message">Why, as the user is told.</param>
public sealed class WorkspaceEditNotSupportedException(string message) : Exception(message);
