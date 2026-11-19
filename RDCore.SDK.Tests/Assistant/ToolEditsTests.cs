using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Assistant;
using RDCore.Tests.Platform;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.Tests.Assistant;

/// <summary>
/// A workspace edit as the tools that fix code say it: each document's change once, by path, as a unified diff of its text before and after, with the path
/// it is renamed to; and the questions of its annotations that need confirmation.
/// </summary>
[TestClass]
public sealed class ToolEditsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-tool-edits");
    private static readonly DocumentUri Module1 = Uri("Module1.bas");
    private static readonly DocumentUri Totals = Uri("Totals.bas");
    private static readonly DocumentUri Helpers = Uri("Helpers.bas");

    private static DocumentUri Uri(string fileName) => DocumentUri.From(Wire.FileUri(Path.Combine(Root, fileName)));

    private static readonly Dictionary<DocumentUri, string> Texts = new()
    {
        [Module1] = "Attribute VB_Name = \"Module1\"\r\nPublic Sub Main()\r\nEnd Sub\r\n",
        [Helpers] = "Attribute VB_Name = \"Helpers\"\r\nPublic Sub Show()\r\n    Main\r\nEnd Sub\r\n",
    };

    private static IReadOnlyList<ToolFileChange> FilesOf(WorkspaceEdit edit)
        => ToolEdits.FilesOf(edit, document => Texts.GetValueOrDefault(document), document => Path.GetFileName(document.GetFileSystemPath()));

    private static WorkspaceEditDocumentChange Edited(DocumentUri document, params TextEdit[] edits)
        => new(new TextDocumentEdit { TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = document }, Edits = new TextEditContainer(edits) });

    private static TextEdit Edit(int line, int start, int end, string text) => new() { Range = new Range(line, start, line, end), NewText = text };

    [TestMethod]
    public void EachDocument_IsItsTextBeforeAndAfter_AsAUnifiedDiff_ByPath()
    {
        var edit = new WorkspaceEdit
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(Edited(Module1, Edit(1, 11, 15, "Run")), Edited(Helpers, Edit(2, 4, 8, "Run"))),
        };

        var files = FilesOf(edit);

        CollectionAssert.AreEqual(new[] { "Helpers.bas", "Module1.bas" }, files.Select(file => file.Path).ToArray());
        Assert.AreEqual("--- Module1.bas\n+++ Module1.bas\n@@ -1,3 +1,3 @@\n Attribute VB_Name = \"Module1\"\n-Public Sub Main()\n+Public Sub Run()\n End Sub\n", files[1].Diff);
        Assert.IsNull(files[1].NewPath);
    }

    [TestMethod]
    public void TheEditsOfADocumentEditedTwice_AreMadeEachToTheTextTheOneBeforeLeft()
    {
        // the second edit's range is of the text the first left: "Public Sub Run()".
        var edit = new WorkspaceEdit
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(Edited(Module1, Edit(1, 11, 15, "Run")), Edited(Module1, Edit(1, 11, 14, "Go"))),
        };

        StringAssert.Contains(FilesOf(edit).Single().Diff, "+Public Sub Go()\n");
    }

    [TestMethod]
    public void ADocumentRenamed_HasThePathItIsRenamedTo_AndTheEditsMadeToItAfter()
    {
        var edit = new WorkspaceEdit
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(
                Edited(Module1, Edit(0, 21, 28, "Totals")),
                new WorkspaceEditDocumentChange(new RenameFile { OldUri = Module1, NewUri = Totals }),
                Edited(Totals, Edit(1, 11, 15, "Run"))),
        };

        var file = FilesOf(edit).Single();

        Assert.AreEqual(("Module1.bas", "Totals.bas"), (file.Path, file.NewPath));
        StringAssert.Contains(file.Diff, "+Attribute VB_Name = \"Totals\"\n");
        StringAssert.Contains(file.Diff, "+Public Sub Run()\n");
    }

    [TestMethod]
    public void ADocumentOnlyRenamed_HasNoDiff()
    {
        var edit = new WorkspaceEdit { DocumentChanges = new Container<WorkspaceEditDocumentChange>(new WorkspaceEditDocumentChange(new RenameFile { OldUri = Module1, NewUri = Totals })) };

        Assert.AreEqual(("Totals.bas", string.Empty), (FilesOf(edit).Single().NewPath, FilesOf(edit).Single().Diff));
    }

    [TestMethod]
    public void ADocumentDeleted_IsADiffToAnEmptyText()
    {
        var edit = new WorkspaceEdit { DocumentChanges = new Container<WorkspaceEditDocumentChange>(new WorkspaceEditDocumentChange(new DeleteFile { Uri = Helpers })) };

        StringAssert.StartsWith(FilesOf(edit).Single().Diff, "--- Helpers.bas\n+++ Helpers.bas\n@@ -1,4 +0,0 @@\n");
    }

    [TestMethod]
    public void TheChangesOfAnEditWithNoDocumentChanges_AreItsChanges()
    {
        var edit = new WorkspaceEdit { Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>> { [Module1] = [Edit(1, 11, 15, "Run")] } };

        StringAssert.Contains(FilesOf(edit).Single().Diff, "+Public Sub Run()\n");
    }

    [TestMethod]
    public void ADocumentItsEditsLeaveAsItWas_IsLeftOut()
    {
        var edit = new WorkspaceEdit
        {
            DocumentChanges = new Container<WorkspaceEditDocumentChange>(Edited(Module1, Edit(1, 11, 15, "Main")), Edited(Helpers)),
        };

        Assert.IsEmpty(FilesOf(edit));
    }

    [TestMethod]
    public void TheQuestionsOfAnEdit_AreItsAnnotationsThatNeedConfirmation()
    {
        var edit = new WorkspaceEdit
        {
            ChangeAnnotations = new Dictionary<ChangeAnnotationIdentifier, ChangeAnnotation>
            {
                ["inString"] = new ChangeAnnotation { Label = "Rename in strings?", Description = "CallByName \"Main\"", NeedsConfirmation = true },
                ["label"] = new ChangeAnnotation { Label = "Rename" },
            },
        };

        var question = ToolEdits.QuestionsOf(edit).Single();

        Assert.AreEqual(("inString", "Rename in strings?", "CallByName \"Main\""), (question.Id, question.Label, question.Description));
        Assert.IsEmpty(ToolEdits.QuestionsOf(new WorkspaceEdit()));
    }
}
