using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.Completion;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types.Complex;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// Where a name written in a module of several documents is looked up from (<see cref="CompletionCandidates.OriginAt(ScopeTree, Uri, SourcePosition, Uri)"/>):
/// the procedure of the position's own document, whatever another document of the module writes on the same lines.
/// </summary>
[TestClass]
public sealed class OriginInADocumentTests
{
    private static readonly Uri Root = new("file://rdcore-test");
    private static readonly Uri MainFile = new("file://rdcore-test/Main.bas");
    private static readonly Uri ExtraFile = new("file://rdcore-test/Extra.bas");
    private static readonly VBStandardModuleSymbol Module = new(Root, Root, "Main");

    // a procedure and its local, written in a document on the lines given.
    private static (VBProcedureMemberSymbol Procedure, VBLocalVariableSymbol Local) Written(string name, Uri? document, int first, int last)
    {
        var range = new SourceRange(first, 0, last, 7);
        var procedure = new VBProcedureMemberSymbol(
            Root, Module.Uri, name, ScopeKind.Module, SymbolKindExt.Procedure, VBVoidType.TypeInfo, range, range, AccessModifier.Public)
        {
            DocumentUri = document,
        };
        var line = first + 1;
        var local = new VBLocalVariableSymbol(Root, procedure.Uri, $"in{name}", ScopeKind.Local, new(line, 4, line, 20), new(line, 8, line, 13))
        {
            DocumentUri = document,
        };
        return (procedure, local);
    }

    private static ScopeTree Tree(params (VBProcedureMemberSymbol Procedure, VBLocalVariableSymbol Local)[] written)
        => ScopeTreeBuilder.Build(
            [Module, .. written.SelectMany(each => new WorkspaceSymbol[] { each.Procedure, each.Local })], AccessModifierVisibility.Instance);

    // Foo in Main.bas and Bar in Extra.bas, each on the lines 2 to 5 of its own document.
    private static ScopeTree OnTheSameLines(out Uri foo, out Uri bar)
    {
        var (fooProcedure, fooLocal) = Written("Foo", MainFile, 2, 5);
        var (barProcedure, barLocal) = Written("Bar", ExtraFile, 2, 5);
        (foo, bar) = (fooProcedure.Uri, barProcedure.Uri);
        return Tree((fooProcedure, fooLocal), (barProcedure, barLocal));
    }

    [TestMethod]
    [DataRow("Main.bas", "Foo", DisplayName = "the first document, its procedure")]
    [DataRow("Extra.bas", "Bar", DisplayName = "the second document, its procedure")]
    public void APosition_IsInTheProcedureOfItsOwnDocument_NotInTheOneAnotherDocumentWritesOnTheSameLines(string file, string procedure)
    {
        var tree = OnTheSameLines(out var foo, out var bar);

        var origin = CompletionCandidates.OriginAt(tree, Module.Uri, new SourcePosition(4, 4), new Uri(Root, file));

        Assert.AreEqual((procedure == "Foo" ? foo : bar).AbsoluteUri, origin.AbsoluteUri);
    }

    [TestMethod]
    public void APositionInNoProcedureOfItsDocument_IsInTheModule_ThoughAnotherDocumentWritesAProcedureOnItsLine()
    {
        var (foo, fooLocal) = Written("Foo", MainFile, 2, 5);
        var (bar, barLocal) = Written("Bar", ExtraFile, 10, 12);
        var tree = Tree((foo, fooLocal), (bar, barLocal));

        var origin = CompletionCandidates.OriginAt(tree, Module.Uri, new SourcePosition(4, 4), ExtraFile);

        Assert.AreEqual(Module.Uri.AbsoluteUri, origin.AbsoluteUri);
    }

    [TestMethod]
    public void AProcedureReadFromNoDocument_IsLookedAtFromAnyDocument()
    {
        var (foo, fooLocal) = Written("Foo", document: null, 2, 5);
        var tree = Tree((foo, fooLocal));

        var origin = CompletionCandidates.OriginAt(tree, Module.Uri, new SourcePosition(4, 4), ExtraFile);

        Assert.AreEqual(foo.Uri.AbsoluteUri, origin.AbsoluteUri);
    }

    [TestMethod]
    public void WithNoDocument_ThePositionIsLookedUpAsTheOverloadWithoutOneDoes()
    {
        var (foo, fooLocal) = Written("Foo", MainFile, 2, 5);
        var (bar, barLocal) = Written("Bar", ExtraFile, 10, 12);
        var tree = Tree((foo, fooLocal), (bar, barLocal));

        foreach (var position in new SourcePosition[] { new(0, 0), new(4, 4), new(11, 4) })
        {
            Assert.AreEqual(
                CompletionCandidates.OriginAt(tree, Module.Uri, position).AbsoluteUri,
                CompletionCandidates.OriginAt(tree, Module.Uri, position, document: null).AbsoluteUri,
                $"at {position}");
        }

        Assert.AreEqual(bar.Uri.AbsoluteUri, CompletionCandidates.OriginAt(tree, Module.Uri, new SourcePosition(11, 4), document: null).AbsoluteUri);
    }
}
