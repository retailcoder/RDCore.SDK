using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.Tests.Platform;

/// <summary>
/// A statement against the <c>Debug</c> object travels with the module's parse, with where its <c>Debug</c> and its member are written.
/// </summary>
[TestClass]
public sealed class DebugStatementWireTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");

    // `Debug.Print count` on line 2 and `Debug  .  Assert count` on line 3, with blanks around the dot: each name's range is its own.
    [TestMethod]
    public void AStatementAgainstDebug_IsReadBackWithWhereItsNamesAreWritten()
    {
        var print = new DebugPrintStatementNode(new SyntaxNodeId(Module.AbsolutePath, [0, 0]), new SourceLocation(Module, new SourceRange(2, 4, 2, 21)),
            [new PrintOutputItemNode(new SyntaxNodeId(Module.AbsolutePath, [0, 0, 0]), new SourceLocation(Module, new SourceRange(2, 16, 2, 21)),
                new SimpleNameExpressionNode(new SyntaxNodeId(Module.AbsolutePath, [0, 0, 0, 0]), new SourceLocation(Module, new SourceRange(2, 16, 2, 21)),
                    "count"), null)])
        {
            ObjectRange = new SourceRange(2, 4, 2, 9),
            MemberRange = new SourceRange(2, 10, 2, 15),
        };
        var assert = new DebugAssertStatementNode(new SyntaxNodeId(Module.AbsolutePath, [0, 1]), new SourceLocation(Module, new SourceRange(3, 4, 3, 26)),
            new SimpleNameExpressionNode(new SyntaxNodeId(Module.AbsolutePath, [0, 1, 0]), new SourceLocation(Module, new SourceRange(3, 21, 3, 26)), "count"))
        {
            ObjectRange = new SourceRange(3, 4, 3, 9),
            MemberRange = new SourceRange(3, 14, 3, 20),
        };
        var module = new ModuleNode(new SyntaxNodeId(Module.AbsolutePath, [0]), new SourceLocation(Module, new SourceRange(0, 0, 4, 0)), [print, assert]);

        var read = PlatformJson.Deserialize<ModuleParseResult>(PlatformJson.Serialize(ModuleParseResult.Success(module))).SyntaxTree!.Children;

        var readPrint = (DebugPrintStatementNode)read[0];
        Assert.AreEqual((print.ObjectRange, print.MemberRange), (readPrint.ObjectRange, readPrint.MemberRange), "where Debug and Print are written");
        var readAssert = (DebugAssertStatementNode)read[1];
        Assert.AreEqual((assert.ObjectRange, assert.MemberRange), (readAssert.ObjectRange, readAssert.MemberRange), "where Debug and Assert are written");
    }

    // a parser that does not give the names' places: the statement reads back without them, not at a range of its own.
    [TestMethod]
    public void AStatementAgainstDebug_WithoutWhereItsNamesAreWritten_IsReadBackWithout()
    {
        var print = new DebugPrintStatementNode(new SyntaxNodeId(Module.AbsolutePath, [0, 0]), new SourceLocation(Module, new SourceRange(2, 4, 2, 15)), []);
        var module = new ModuleNode(new SyntaxNodeId(Module.AbsolutePath, [0]), new SourceLocation(Module, new SourceRange(0, 0, 3, 0)), [print]);

        var read = (DebugPrintStatementNode)PlatformJson.Deserialize<ModuleParseResult>(PlatformJson.Serialize(ModuleParseResult.Success(module))).SyntaxTree!
            .Children.Single();

        Assert.AreEqual(((SourceRange?)null, (SourceRange?)null), (read.ObjectRange, read.MemberRange));
    }
}
