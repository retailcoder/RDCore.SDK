using RDCore.SDK.Formatting;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Workspace;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.Tests.Formatting;

/// <summary>
/// A file of blocks is indented module by module: the lines of a block as its own module has them, from the margin, and the header and the end of the block
/// as the file's own module has them. A line break typed in a block closes the block's procedure.
/// </summary>
[TestClass]
public sealed class ModuleIndenterBlocksTests
{
    private static readonly Uri File = new("file:///c:/ws/Backup.vbs");
    private static readonly Uri Logger = RDCoreDocument.BlockUri(File, "Logger");
    private static int _next;

    private static SyntaxNodeId Id(Uri module) => new(module.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation At(Uri module, int startLine, int startCharacter, int endLine, int endCharacter)
        => new() { Uri = module, Range = new SourceRange(startLine, startCharacter, endLine, endCharacter) };

    // a Sub of a module, from its header line to its last line (its End Sub, or its header when it has none yet), with a Beep on each line of its body.
    private static MemberDeclarationNode Sub(Uri module, string name, int header, int last, params int[] beeps)
        => new(Id(module), At(module, header, 0, last, last == header ? "Public Sub Log()".Length : "End Sub".Length),
            [.. beeps.Select(line => new KeywordStatementNode(Id(module), At(module, line, 0, line, 4), "Beep", []))], name, MemberKind.Procedure);

    // the file's parse: its own module's tree, which has Main, and one block, Logger, whose tree has its procedures.
    private static ModuleParseResult Parse(int lines, MemberDeclarationNode main, int header, int end, params MemberDeclarationNode[] procedures)
    {
        var block = new BlockModule("Logger", ModuleType.StdModule, Logger, new SourceRange(header, 0, end, 10),
            new ModuleNode(Id(Logger), At(Logger, header + 1, 0, end, 0), [.. procedures]));
        return ModuleParseResult.Success(new ModuleNode(Id(File), At(File, 0, 0, lines, 0), [main])) with { BlockModules = [block] };
    }

    private static string Text(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    // the procedures of a block are its module's: they are spaced as the module spaces them, and their lines indented as its tree has them.
    [TestMethod]
    public void TheLinesOfABlock_AreIndentedAsItsModuleHasThem_AndItsHeaderAndEndAsTheFilesOwnModuleHasThem()
    {
        var text = Text(
            "Public Sub Main()",
            "Beep",
            "End Sub",
            "",
            "  Module Logger",
            "Public Sub Log()",
            "Beep",
            "End Sub",
            "Public Sub Flush()",
            "End Sub",
            "  End Module");
        var parse = Parse(11, Sub(File, "Main", 0, 2, 1), 4, 10, Sub(Logger, "Log", 5, 7, 6), Sub(Logger, "Flush", 8, 9));

        var indented = ModuleIndenter.Apply(text, ModuleIndenter.Indent(parse, text, CodeFormattingOptions.Default));

        Assert.AreEqual(Text(
            "Public Sub Main()",
            "    Beep",
            "End Sub",
            "",
            "Module Logger",
            "Public Sub Log()",
            "    Beep",
            "End Sub",
            "",
            "Public Sub Flush()",
            "End Sub",
            "End Module"), indented);
    }

    [TestMethod]
    public void TheOpenBlocksAtALineOfABlock_AreThoseOfItsModule()
    {
        var text = Text(
            "Public Sub Main()",
            "End Sub",
            "Module Logger",
            "Public Sub Log()",
            "    Beep",
            "End Module");
        var parse = Parse(6, Sub(File, "Main", 0, 1), 2, 5, Sub(Logger, "Log", 3, 4, 4));

        var open = ModuleBlocks.OpenAt(parse, text, 4);

        CollectionAssert.AreEqual(new[] { new ModuleBlock(3, 3, "End Sub", HasBody: true) }, open.ToArray());
    }

    [TestMethod]
    public async Task ALineBreakAfterTheHeaderOfAProcedureOfABlock_ClosesIt()
    {
        var text = Text(
            "Public Sub Main()",
            "End Sub",
            "Module Logger",
            "Public Sub Log()",
            "",
            "End Module");
        var parse = Parse(6, Sub(File, "Main", 0, 1), 2, 5, Sub(Logger, "Log", 3, 3));
        var closed = Text(
            "Public Sub Main()",
            "End Sub",
            "Module Logger",
            "Public Sub Log()",
            "",
            "End Sub",
            "End Module");
        var reparsed = Parse(7, Sub(File, "Main", 0, 1), 2, 6, Sub(Logger, "Log", 3, 5));

        var edits = await ModuleBlocks.OnLineBreakAsync(parse, text, 4, CodeFormattingOptions.Default,
            (written, _) => Task.FromResult(written == closed ? reparsed : throw new AssertFailedException($"parsed again: {written}")));

        Assert.AreEqual("End Sub\r\n", edits[^1].NewText);
        Assert.AreEqual(new Range(5, 0, 5, 0), edits[^1].Range);
        Assert.IsTrue(edits.Any(edit => edit.Range.Start.Line == 4 && edit.NewText == "    "), "the new line is in the procedure's body");
    }
}
