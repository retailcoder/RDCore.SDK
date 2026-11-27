using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Semantics.Refactoring;
using RDCore.SDK.Workspace;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// Where declarations are added to a block's module (<c>Partial Class Circle ... End Class</c>) that has no procedure: on the block's own lines, before
/// its end line, and never after the end of its file.
/// </summary>
[TestClass]
public sealed class DeclarationEditsSectionEndTests
{
    private static readonly Uri File = new("file:///c:/ws/Shapes.bas");
    private const string Stub = "Private Sub IShape_Draw()\r\nEnd Sub";

    [TestMethod]
    [DataRow("Implements IShape\r\n", "Implements IShape\r\n" + Stub + "\r\n", DisplayName = "after the block's last line that is not blank")]
    [DataRow("", Stub + "\r\n", DisplayName = "between the header and the end of an empty block")]
    [DataRow("\r\n", Stub + "\r\n\r\n", DisplayName = "on the line after the header of a block of blank lines")]
    public void DeclarationsAddedToABlockWithNoProcedure_AreWrittenInsideTheBlock(string body, string written)
    {
        // 0 the file's own module; 1 the header; the body; the end; then a procedure of the file's own module, after which a declaration would leave
        // the block.
        var end = 2 + body.Split("\r\n").Length - 1;
        var text = TextOf(body);
        var circle = RDCoreDocument.BlockUri(File, "Circle");
        var block = new BlockModule("Circle", ModuleType.ClassModule, circle, new SourceRange(1, 0, end, 9),
            new ModuleNode(new SyntaxNodeId(circle.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(2, 0, end, 0)), []));
        var main = new MemberDeclarationNode(new SyntaxNodeId(File.AbsolutePath, [0, 0]), new SourceLocation(File, new SourceRange(end + 1, 0, end + 2, 7)),
            [], "Main", MemberKind.Procedure, AccessModifier.Public);
        var own = new ModuleNode(new SyntaxNodeId(File.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(0, 0, end + 3, 0)), [main]);
        var parse = new RDCoreDocument(File, ModuleParseResult.Success(own) with { BlockModules = [block] }).ParseOf(circle)!;

        var edit = DeclarationEdits.Insert(parse, text, Stub);

        Assert.IsNotNull(edit);
        Assert.AreEqual(TextOf(written), DeclarationEdits.ApplyTo(text, [edit]));
    }

    private static string TextOf(string body) => $"Option Explicit\r\nPartial Class Circle\r\n{body}End Class\r\nPublic Sub Main()\r\nEnd Sub\r\n";
}
