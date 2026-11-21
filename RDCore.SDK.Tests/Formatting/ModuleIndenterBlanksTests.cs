using RDCore.SDK.Formatting;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;

namespace RDCore.Tests.Formatting;

/// <summary>
/// The indenter reads a space or a tab as a blank, as the parser's lexer does: a no-break space is written code, which the indenter keeps where it is.
/// </summary>
[TestClass]
public sealed class ModuleIndenterBlanksTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");
    private static int _next;

    private static SyntaxNodeId Id() => new(Module.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation At(int startLine, int startCharacter, int endLine, int endCharacter)
        => new() { Uri = Module, Range = new SourceRange(startLine, startCharacter, endLine, endCharacter) };

    // a procedure with one line in its body, and what the parser has of it: the line's statement, when it has one.
    private static string IndentedBody(string body, bool isStatement, CodeFormattingOptions options)
    {
        var text = $"Public Sub Main()\r\n{body}\r\nEnd Sub\r\n";
        SyntaxNode[] statements = isStatement ? [new KeywordStatementNode(Id(), At(1, 0, 1, body.Length), body, [])] : [];
        var main = new MemberDeclarationNode(Id(), At(0, 0, 2, 7), [.. statements], "Main", MemberKind.Procedure);
        var parse = new ModuleParseResult { SyntaxTree = new ModuleNode(Id(), At(0, 0, 3, 0), [main]) };

        return ModuleIndenter.Apply(text, ModuleIndenter.Indent(parse, text, options)).Split("\r\n")[1];
    }

    [TestMethod]
    public void AStatementThatStartsWithANoBreakSpace_KeepsIt_AfterItsIndentation()
        => Assert.AreEqual("\u00A0Beep", IndentedBody("\u00A0Beep", isStatement: true, CodeFormattingOptions.Default).TrimStart(' '));

    [TestMethod]
    public void ALineOfANoBreakSpace_IsNotAnEmptyLine_ThatIsRemoved()
    {
        var options = CodeFormattingOptions.Default with { EmptyLineHandlingMethod = EmptyLineHandlingMethod.Remove };

        Assert.AreEqual("\u00A0", IndentedBody("\u00A0", isStatement: false, options).TrimStart(' '));
    }
}
