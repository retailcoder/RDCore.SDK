using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Formatting;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Completion;
using RDCore.SDK.Model.Symbols.VBProject;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// For a client that takes snippets, the keyword that starts a block or a procedure is offered as the whole block, its body indented relative to its first
/// line as the formatting options indent it: the client adds the caret line's indentation when it adjusts what it inserts, and the snippet has it written in
/// otherwise. A keyword that ends a block, and a client that takes no snippets, are offered the keyword alone.
/// </summary>
[TestClass]
public sealed class CompletionSnippetsTests
{
    private static readonly Uri Root = new("file://rdcore-test");
    private static readonly VBClassModuleSymbol Class1 = new(Root, Root, "Class1");
    private static readonly ScopeTree Tree = ScopeTreeBuilder.Build([Class1]);

    private static readonly string[] Procedure = ["Sub DoSomething()", "        ", "End Sub"];

    private static CompletionItemCapabilityOptions Client(bool snippets = true, bool adjustsIndentation = true) => new()
    {
        SnippetSupport = snippets,
        InsertTextModeSupport = adjustsIndentation
            ? new CompletionItemInsertTextModeSupportCapabilityOptions { ValueSet = new Container<InsertTextMode>(InsertTextMode.AdjustIndentation, InsertTextMode.AsIs) }
            : null,
    };

    // what is offered at the end of a line of the lines given, the line typed so far being what it has after its indentation.
    private static IReadOnlyList<CompletionCandidate> At(string[] lines, int line, string typed, CompletionSnippets? snippets)
    {
        lines = [.. lines];
        lines[line] += typed;
        return CompletionRules.At(Tree, Class1.Uri, CompletionSite.Read(lines[line]), CompletionPlacement.Read(lines, line), new CompletionLanguage(), snippets);
    }

    private static CompletionCandidate Offered(IReadOnlyList<CompletionCandidate> candidates, string label)
        => candidates.Single(candidate => string.Equals(candidate.Label, label, StringComparison.Ordinal));

    [TestMethod]
    public void TheKeywordThatStartsABlock_IsOfferedAsTheBlock_ItsBodyIndentedRelativeToItsFirstLine()
    {
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default, Procedure[1], "\r\n");

        var offered = Offered(At(Procedure, 1, string.Empty, snippets), "If");

        Assert.IsTrue(offered.IsSnippet);
        Assert.AreEqual("If ${1:condition} Then\r\n    $0\r\nEnd If", offered.InsertText);
        Assert.AreEqual(InsertTextMode.AdjustIndentation, offered.InsertTextMode);
        Assert.AreEqual("If condition Then ... End If", offered.Detail);
    }

    [TestMethod]
    public void ASnippetsItem_SaysItIsASnippet_AndThatTheClientAdjustsItsIndentation()
    {
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default, Procedure[1], "\r\n");

        var item = Offered(At(Procedure, 1, "Wi", snippets), "With").ToCompletionItem();

        Assert.AreEqual(InsertTextFormat.Snippet, item.InsertTextFormat);
        Assert.AreEqual(InsertTextMode.AdjustIndentation, item.InsertTextMode);
        Assert.AreEqual("With ${1:object}\r\n    $0\r\nEnd With", item.InsertText);
    }

    [TestMethod]
    public void ForAClientThatDoesNotAdjustTheIndentation_TheCaretLinesIndentation_IsWrittenIntoTheSnippet()
    {
        var snippets = CompletionSnippets.For(Client(adjustsIndentation: false), CodeFormattingOptions.Default, Procedure[1], "\r\n");

        var offered = Offered(At(Procedure, 1, string.Empty, snippets), "For");

        Assert.AreEqual("For ${1:i} = ${2:1} To ${3:10}\r\n            $0\r\n        Next", offered.InsertText);
        Assert.IsNull(offered.InsertTextMode);
        Assert.AreEqual(default, offered.ToCompletionItem().InsertTextMode, "the item says no mode");
    }

    [TestMethod]
    public void TheSnippet_IsWrittenWithTheLineEndOfTheCaretsLine()
    {
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default, Procedure[1], "\n");

        Assert.AreEqual("With ${1:object}\n    $0\nEnd With", snippets!.TextOf("With"));
    }

    [TestMethod]
    public void TheEachOfAForEach_IsOfferedAsTheLoop()
    {
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default, Procedure[1], "\r\n");

        var offered = Offered(At(Procedure, 1, "For ", snippets), "Each");

        Assert.AreEqual("Each ${1:item} In ${2:collection}\r\n    $0\r\nNext", offered.InsertText);
    }

    [TestMethod]
    public void TheCaseOfASelectCase_IsIndentedAsTheOptionsIndentIt()
    {
        var indented = CompletionSnippets.For(Client(), CodeFormattingOptions.Default with { IndentCase = true }, Procedure[1], "\r\n");
        var notIndented = CompletionSnippets.For(Client(), CodeFormattingOptions.Default with { IndentCase = false }, Procedure[1], "\r\n");

        Assert.AreEqual("Select Case ${1:expression}\r\n    Case ${2:value}\r\n        $0\r\nEnd Select", indented!.TextOf("Select"));
        Assert.AreEqual("Select Case ${1:expression}\r\nCase ${2:value}\r\n    $0\r\nEnd Select", notIndented!.TextOf("Select"));
    }

    [TestMethod]
    public void TheIndentationOfABody_IsTheOptionsIndentSpaces()
    {
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default with { IndentSpaces = 2 }, Procedure[1], "\r\n");

        Assert.AreEqual("Do ${1|While,Until|} ${2:condition}\r\n  $0\r\nLoop", snippets!.TextOf("Do"));
    }

    [DataTestMethod]
    [DataRow("", DisplayName = "at the start of a declaration")]
    [DataRow("Private ", DisplayName = "after a modifier")]
    public void AProcedure_IsOfferedAsItsHeaderAndItsEnd_WhereADeclarationStarts(string typed)
    {
        string[] module = ["Option Explicit", string.Empty];
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default, string.Empty, "\r\n");

        var offered = At(module, 1, typed, snippets);

        Assert.AreEqual("Sub ${1:Name}($2)\r\n    $0\r\nEnd Sub", Offered(offered, "Sub").InsertText);
        Assert.AreEqual("Function ${1:Name}($2) As ${3:Variant}\r\n    $0\r\nEnd Function", Offered(offered, "Function").InsertText);
        Assert.AreEqual("Property Get ${1:Name}() As ${2:Variant}\r\n    $0\r\nEnd Property", Offered(offered, "Property").InsertText);
        Assert.IsFalse(Offered(offered, "Const").IsSnippet);
    }

    [TestMethod]
    public void TheBodyOfAProcedure_IsNotIndented_WhereTheOptionsDoNotIndentIt()
    {
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default with { IndentEntireProcedureBody = false }, string.Empty, "\r\n");

        Assert.AreEqual("Sub ${1:Name}($2)\r\n$0\r\nEnd Sub", snippets!.TextOf("Sub"));
        Assert.AreEqual("If ${1:condition} Then\r\n    $0\r\nEnd If", snippets.TextOf("If"), "a block's body is indented whatever a procedure's is");
    }

    [TestMethod]
    public void AKeywordThatEndsABlock_IsTheKeywordAlone()
    {
        string[] lines = ["Sub DoSomething()", "    If True Then", "        ", "    End If", "End Sub"];
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default, lines[2], "\r\n");

        var offered = At(lines, 2, "End ", snippets);

        Assert.IsFalse(Offered(offered, "If").IsSnippet);
        Assert.IsNull(Offered(offered, "If").InsertText);
    }

    [TestMethod]
    public void EachSnippet_IsOneToInsert_WithTheCursorInItsBodyLast()
    {
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default, Procedure[1], "\r\n")!;

        foreach (var keyword in new[] { "If", "For", "Each", "Do", "Select", "With", "Sub", "Function", "Property" })
        {
            Assert.IsTrue(Snippet.TryParse(snippets.TextOf(keyword)!, out var snippet), keyword);
            Assert.IsTrue(snippet.IsMultiline, keyword);
            Assert.AreEqual(1, snippet.TabStops.Count(stop => stop.Index == 0), keyword);
        }
    }

    [TestMethod]
    public void AKeywordThatStartsNoBlock_HasNoSnippet()
    {
        var snippets = CompletionSnippets.For(Client(), CodeFormattingOptions.Default, Procedure[1], "\r\n")!;

        Assert.IsNull(snippets.TextOf("Dim"));
        Assert.IsFalse(Offered(At(Procedure, 1, string.Empty, snippets), "Dim").IsSnippet);
    }

    [TestMethod]
    public void AClientThatTakesNoSnippets_IsOfferedNone()
    {
        Assert.IsNull(CompletionSnippets.For(Client(snippets: false), CodeFormattingOptions.Default, Procedure[1], "\r\n"));
        Assert.IsNull(CompletionSnippets.For(null, CodeFormattingOptions.Default, Procedure[1], "\r\n"));

        var offered = Offered(At(Procedure, 1, string.Empty, snippets: null), "If");
        Assert.IsFalse(offered.IsSnippet);
        Assert.IsNull(offered.ToCompletionItem().InsertText);
    }

    [TestMethod]
    public void AUserWhoSetNoSnippets_IsOfferedNone()
    {
        Assert.IsNull(CompletionSnippets.For(Client(), CodeFormattingOptions.Default with { Snippets = false }, Procedure[1], "\r\n"));
    }
}
