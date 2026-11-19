using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Server.ProtocolExtensions;
using RDCore.Tests.Platform;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.Tests.Server;

/// <summary>
/// A WorkspaceEdit as a client can apply it (LSP 3.17 §WorkspaceEdit): documentChanges to a client that said it takes them, and the same edits as changes to
/// any other - which reads changes alone; change annotations to a client that takes them; and an edit the client can't apply at all refused, with why.
/// </summary>
[TestClass]
public sealed class WorkspaceEditExtensionsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-workspace-edit");
    private static readonly DocumentUri First = DocumentUri.From(Wire.FileUri(Path.Combine(Root, "Module1.bas")));
    private static readonly DocumentUri Second = DocumentUri.From(Wire.FileUri(Path.Combine(Root, "Module2.bas")));
    private static readonly ChangeAnnotationIdentifier InAComment = "in a comment";

    private static TextEdit Edit(int line, string text) => new() { Range = new Range(line, 0, line, 3), NewText = text };

    private static AnnotatedTextEdit Annotated(int line, string text) => new() { Range = new Range(line, 0, line, 3), NewText = text, AnnotationId = InAComment };

    private static WorkspaceEditDocumentChange Change(DocumentUri uri, int version, params TextEdit[] edits)
        => new(new TextDocumentEdit { TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = uri, Version = version }, Edits = new TextEditContainer(edits) });

    private static WorkspaceEdit Versioned() => new()
    {
        DocumentChanges = new Container<WorkspaceEditDocumentChange>(Change(First, 4, Edit(0, "a"), Edit(2, "b")), Change(Second, 7, Edit(1, "c"))),
    };

    // a rename that also renames a name in a comment, which the user is asked to confirm.
    private static WorkspaceEdit WithAConfirmation() => new()
    {
        DocumentChanges = new Container<WorkspaceEditDocumentChange>(Change(First, 4, Edit(0, "a"), Annotated(3, "commented"))),
        ChangeAnnotations = new Dictionary<ChangeAnnotationIdentifier, ChangeAnnotation>
        {
            [InAComment] = new ChangeAnnotation { Label = "a name in a comment", NeedsConfirmation = true },
        },
    };

    private static ClientCapabilities Taking(bool documentChanges, bool annotations = false, params ResourceOperationKind[] resources) => new()
    {
        Workspace = new WorkspaceClientCapabilities
        {
            WorkspaceEdit = new WorkspaceEditCapability
            {
                DocumentChanges = documentChanges,
                ChangeAnnotationSupport = annotations ? new WorkspaceEditSupportCapabilitiesChangeAnnotationSupport() : null,
                ResourceOperations = new Container<ResourceOperationKind>(resources),
            },
        },
    };

    private static string[] Texts(IEnumerable<TextEdit> edits) => [.. edits.Select(edit => edit.NewText)];

    [TestMethod]
    public void AClientThatTakesDocumentChanges_IsSentTheEditAsItIs()
    {
        var edit = Versioned();

        Assert.AreSame(edit, edit.For(Taking(documentChanges: true)));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "a client that says it does not")]
    [DataRow(null, DisplayName = "a client whose capabilities are not known")]
    public void AnyOtherClient_IsSentTheSameEditsAsChanges_ByDocument_InOrder(bool? documentChanges)
    {
        var sent = Versioned().For(documentChanges is { } takes ? Taking(takes) : null);

        Assert.IsNull(sent.DocumentChanges);
        CollectionAssert.AreEqual(new[] { "a", "b" }, Texts(sent.Changes![First]));
        CollectionAssert.AreEqual(new[] { "c" }, Texts(sent.Changes[Second]));
    }

    // an edit that has both is giving its changes as the alternative of its documentChanges: a client is sent one of them, never both of them at once.
    [TestMethod]
    public void AnEditThatHasBoth_IsSentItsChanges_AloneToAClientThatDoesNotTakeDocumentChanges()
    {
        var edit = Versioned() with { Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>> { [First] = [Edit(0, "a"), Edit(2, "b")], [Second] = [Edit(1, "c")] } };

        var sent = edit.For(Taking(documentChanges: false));

        Assert.IsNull(sent.DocumentChanges);
        CollectionAssert.AreEqual(new[] { "a", "b" }, Texts(sent.Changes![First]), "each edit once");
        CollectionAssert.AreEqual(new[] { "c" }, Texts(sent.Changes[Second]));
    }

    // what the user is asked to confirm is optional: a client that can't ask is sent the edits that need no confirmation, with no annotation.
    [TestMethod]
    [DataRow(true, DisplayName = "a client that takes documentChanges, and not annotations")]
    [DataRow(false, DisplayName = "a client that takes neither")]
    public void AnEditThatNeedsConfirmation_IsLeftOut_ForAClientThatDoesNotTakeAnnotations(bool documentChanges)
    {
        var sent = WithAConfirmation().For(Taking(documentChanges));

        var edits = documentChanges ? sent.DocumentChanges!.Single().TextDocumentEdit!.Edits.ToList() : sent.Changes![First].ToList();
        CollectionAssert.AreEqual(new[] { "a" }, Texts(edits));
        Assert.IsFalse(edits.Any(edit => edit is AnnotatedTextEdit), "no annotation is sent");
        Assert.IsNull(sent.ChangeAnnotations);
    }

    [TestMethod]
    public void AClientThatTakesAnnotations_IsSentThemAsTheyAre()
    {
        var edit = WithAConfirmation();

        Assert.AreSame(edit, edit.For(Taking(documentChanges: true, annotations: true)));
    }

    // changes can't say that a file is created, renamed or deleted: an edit that does is refused to a client that can't apply it, rather than sent to do nothing.
    [TestMethod]
    [DataRow(false, DisplayName = "a client that does not take documentChanges")]
    [DataRow(true, DisplayName = "a client that takes documentChanges, and not file operations")]
    public void AnEditThatCreatesAFile_IsRefused_ToAClientThatCantApplyIt(bool documentChanges)
    {
        var edit = new WorkspaceEdit
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(new WorkspaceEditDocumentChange(new CreateFile { Uri = Second }), Change(First, 4, Edit(0, "a"))),
        };

        _ = Assert.ThrowsExactly<WorkspaceEditNotSupportedException>(() => edit.For(Taking(documentChanges)));
    }

    [TestMethod]
    public void AnEditThatCreatesAFile_IsSent_ToAClientThatSaysItCreatesFiles()
    {
        var edit = new WorkspaceEdit
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(new WorkspaceEditDocumentChange(new CreateFile { Uri = Second }), Change(First, 4, Edit(0, "a"))),
        };

        Assert.AreSame(edit, edit.For(Taking(documentChanges: true, annotations: false, ResourceOperationKind.Create)));
    }

    // a second edit of a document is of the text the first one made, which changes cannot say: it is refused to a client that reads changes alone, and sent as
    // it is to one that takes documentChanges.
    [TestMethod]
    public void ADocumentEditedTwice_IsRefused_ToAClientThatDoesNotTakeDocumentChanges()
    {
        var edit = new WorkspaceEdit { DocumentChanges = new Container<WorkspaceEditDocumentChange>(Change(First, 4, Edit(0, "a")), Change(First, 5, Edit(0, "b"))) };

        var refused = Assert.ThrowsExactly<WorkspaceEditNotSupportedException>(() => edit.For(Taking(documentChanges: false)));
        StringAssert.Contains(refused.Message, "Module1.bas");
        Assert.AreSame(edit, edit.For(Taking(documentChanges: true)));
    }

    [TestMethod]
    [DataRow(true, new[] { "a", "commented" }, DisplayName = "answered yes: made, and no longer asked")]
    [DataRow(false, new[] { "a" }, DisplayName = "answered no: left out, with its annotation")]
    public void AQuestionAnswered_ConfirmsItsEdits_OrLeavesThemOut(bool yes, string[] made)
    {
        var answered = WithAConfirmation().Answered(yes ? [InAComment.Identifier] : []);

        CollectionAssert.AreEqual(made, Texts(answered.DocumentChanges!.Single().TextDocumentEdit!.Edits));
        Assert.IsFalse(answered.ChangeAnnotations?.Values.Any(annotation => annotation.NeedsConfirmation == true) ?? false);
        Assert.AreEqual(yes, answered.ChangeAnnotations?.ContainsKey(InAComment) ?? false);
    }

    [TestMethod]
    public void AFileRenameOfAnAnnotation_IsLeftOutWithIt()
    {
        var rename = new RenameFile { OldUri = First, NewUri = Second, AnnotationId = InAComment };
        var edit = WithAConfirmation() with
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(WithAConfirmation().DocumentChanges!.Append(new WorkspaceEditDocumentChange(rename))),
        };

        Assert.IsFalse(edit.Without(InAComment).DocumentChanges!.Any(change => change.IsRenameFile));
        Assert.IsTrue(edit.Confirmed(InAComment).DocumentChanges!.Any(change => change.IsRenameFile));
    }

    [TestMethod]
    public void AnEditWithoutItsFileRenames_KeepsTheTextEditsOfTheDocumentsAsTheyAreNamed()
    {
        var edit = new WorkspaceEdit
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(
                Change(First, 4, Edit(0, "a")),
                new WorkspaceEditDocumentChange(new RenameFile { OldUri = First, NewUri = Second }),
                Change(Second, 1, Edit(1, "b"))),
        };

        var kept = edit.WithoutFileRenames();

        Assert.AreEqual(First, kept.DocumentChanges!.Single().TextDocumentEdit!.TextDocument.Uri);
        var plain = Versioned();
        Assert.AreSame(plain, plain.WithoutFileRenames(), "an edit that renames no file is itself");
    }

    [TestMethod]
    [DataRow(true, true, DisplayName = "documentChanges and rename")]
    [DataRow(false, false, DisplayName = "rename without documentChanges")]
    public void AClientRenamesFiles_WhenItSaysDocumentChangesAndRename(bool documentChanges, bool renames)
    {
        Assert.AreEqual(renames, Taking(documentChanges, resources: ResourceOperationKind.Rename).TakesResourceOperation(ResourceOperationKind.Rename));
        Assert.IsFalse(Taking(documentChanges: true).TakesResourceOperation(ResourceOperationKind.Rename));
        Assert.IsFalse(((ClientCapabilities?)null).TakesResourceOperation(ResourceOperationKind.Rename));
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void AClientAppliesEdits_WhenItSaysApplyEdit(bool applyEdit, bool applies)
    {
        var client = new ClientCapabilities { Workspace = new WorkspaceClientCapabilities { ApplyEdit = applyEdit } };

        Assert.AreEqual(applies, client.AppliesEdits());
        Assert.IsFalse(new ClientCapabilities().AppliesEdits());
    }
}
