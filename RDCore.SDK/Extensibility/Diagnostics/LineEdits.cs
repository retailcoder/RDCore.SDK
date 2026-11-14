using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Semantics;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// The edits of a fix (<see cref="EditFix"/>) that remove or write whole lines, placed by the parse of the module an analyzer is given
/// (<see cref="ModuleAnalysisContext.ParseResult"/>): which lines a node is alone on, and lines of their own written before or after one.
/// </summary>
/// <remarks>
/// <para>
/// The parse accounts for everything written in the module: the syntax tree, its <see cref="ModuleParseResult.Trivia"/> (comments, line continuations) and
/// its <see cref="ModuleParseResult.PrecompilerTrivia"/> (the <c>#If</c> directives). What none of them has a range for is blanks. So whether anything else
/// is written on the lines of a node is a question the parse answers, without the text.
/// </para>
/// <para>
/// A line this writes ends with the module's own line ending (<see cref="ModuleParseResult.LineEnding"/>) and is written without indentation: the
/// language server indents the lines a fix writes, the way the user indents the code, before it offers the fix.
/// </para>
/// </remarks>
public static class LineEdits
{
    /// <summary>
    /// The lines a node is written on, from the start of its first line to the start of the line after its last, when nothing else is written on them:
    /// removed, they leave no blank line behind.
    /// </summary>
    /// <param name="parse">The parse of the module the node is in.</param>
    /// <param name="node">The node.</param>
    /// <returns>The range of the lines, or <see langword="null"/> when anything else is written on them (see <see cref="WholeLinesOf(ModuleParseResult, SourceRange)"/>).</returns>
    public static SourceRange? WholeLinesOf(ModuleParseResult parse, SyntaxNode node) => WholeLinesOf(parse, node.SourceLocation.Range);

    /// <summary>
    /// The lines a span of the module is written on, from the start of its first line to the start of the line after its last, when nothing else is written
    /// on them: an empty <c>Else</c> through the line before its <c>End If</c>, or the statements of a body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Something else is written on the lines when a node of the tree, a trivia or a directive has characters on them and is neither inside the span nor
    /// around it: a statement after a <c>:</c>, a comment after the code, a line continuation that carries a statement onto the first line or off the last.
    /// A node around the span, such as the block or the procedure it is in, is something else when it has characters on those lines besides the span's: the
    /// <c>If ... Then</c> of a single-line <c>If</c>, or an <c>End If</c> after a <c>:</c>.
    /// </para>
    /// <para>
    /// The last line of a text that does not end with a line end has none to remove: the range then ends at the end of the text, and the line end before the
    /// span stays, as a blank last line.
    /// </para>
    /// </remarks>
    /// <param name="parse">The parse of the module the span is in.</param>
    /// <param name="span">The span.</param>
    /// <returns>The range of the lines, or <see langword="null"/> when anything else is written on them, or when the parse has no syntax tree.</returns>
    public static SourceRange? WholeLinesOf(ModuleParseResult parse, SourceRange span)
    {
        if (parse.SyntaxTree is null || IsAnythingElseOnTheLinesOf(parse, span))
        {
            return null;
        }

        var end = EndOfText(parse);
        return new SourceRange(new SourcePosition(span.Start.Line, 0), span.End.Line < end.Line ? new SourcePosition(span.End.Line + 1, 0) : end);
    }

    /// <summary>
    /// Lines of their own, written before the line a node starts on.
    /// </summary>
    /// <param name="parse">The parse of the module the node is in.</param>
    /// <param name="node">The node.</param>
    /// <param name="lines">The lines, without their line ends or indentation; an empty one is a blank line.</param>
    public static TextEdit InsertLinesBefore(ModuleParseResult parse, SyntaxNode node, params string[] lines)
        => InsertLinesBefore(parse, node.SourceLocation.Range, lines);

    /// <summary>
    /// Lines of their own, written before the line a range starts on: before the <c>End Sub</c> of a procedure (<see cref="Model.AST.Declarations.MemberDeclarationNode.EndRange"/>).
    /// </summary>
    /// <param name="parse">The parse of the module the range is in.</param>
    /// <param name="range">The range.</param>
    /// <param name="lines">The lines, without their line ends or indentation; an empty one is a blank line.</param>
    public static TextEdit InsertLinesBefore(ModuleParseResult parse, SourceRange range, params string[] lines)
        => SyntaxEdits.Insert(new SourcePosition(range.Start.Line, 0), string.Concat(lines.Select(line => line + parse.LineEnding)));

    /// <summary>
    /// Lines of their own, written after the line a node ends on.
    /// </summary>
    /// <param name="parse">The parse of the module the node is in.</param>
    /// <param name="node">The node.</param>
    /// <param name="lines">The lines, without their line ends or indentation; an empty one is a blank line.</param>
    public static TextEdit InsertLinesAfter(ModuleParseResult parse, SyntaxNode node, params string[] lines)
        => InsertLinesAfter(parse, node.SourceLocation.Range, lines);

    /// <summary>
    /// Lines of their own, written after the line a range ends on.
    /// </summary>
    /// <remarks>
    /// After the last line of a text that does not end with a line end, the lines are written at the end of the text, each after a line end.
    /// </remarks>
    /// <param name="parse">The parse of the module the range is in.</param>
    /// <param name="range">The range.</param>
    /// <param name="lines">The lines, without their line ends or indentation; an empty one is a blank line.</param>
    public static TextEdit InsertLinesAfter(ModuleParseResult parse, SourceRange range, params string[] lines)
    {
        var end = EndOfText(parse);
        return range.End.Line < end.Line
            ? SyntaxEdits.Insert(new SourcePosition(range.End.Line + 1, 0), string.Concat(lines.Select(line => line + parse.LineEnding)))
            : SyntaxEdits.Insert(end, string.Concat(lines.Select(line => parse.LineEnding + line)));
    }

    /// <summary>
    /// Where declarations are added to the declaration section of a module (<strong>MS-VBAL §5.2</strong>): the start of the line after the last line
    /// written before the first procedure and the comments directly above it, which stay the procedure's. In a module with no procedure, it is the end of
    /// the text.
    /// </summary>
    /// <remarks>
    /// A line is blank when nothing the parse has a range for is written on it, and a comment is directly above a procedure when the lines between them hold
    /// comments and nothing else.
    /// </remarks>
    /// <param name="parse">The parse of the module.</param>
    /// <returns>
    /// The position, or <see langword="null"/> when it is in a branch of an <c>#If</c> block (<strong>MS-VBAL §3.4</strong>), where a declaration would be
    /// compiled only when that branch is live, or when the parse has no syntax tree.
    /// </returns>
    public static SourcePosition? EndOfDeclarations(ModuleParseResult parse)
    {
        if (parse.SyntaxTree is not { } module)
        {
            return null;
        }

        var written = WrittenNodes(parse).Where(node => node.SourceLocation.Range.Start != node.SourceLocation.Range.End).ToArray();
        var procedure = module.Children.OfType<MemberDeclarationNode>().FirstOrDefault(member => member.MemberKind is MemberKind.Procedure
            or MemberKind.Function or MemberKind.PropertyGet or MemberKind.PropertyLet or MemberKind.PropertySet);

        SourcePosition at;
        if (procedure is null)
        {
            at = EndOfText(parse);
        }
        else
        {
            var first = procedure.SourceLocation.Range.Start.Line;
            while (first > 0 && On(written, first - 1) is { Length: > 0 } above && above.All(node => node is CommentTriviaNode or AnnotationTriviaNode))
            {
                first--;
            }

            var last = first - 1;
            while (last >= 0 && On(written, last).Length == 0)
            {
                last--;
            }

            at = last < 0 ? SourcePosition.Zero : new SourcePosition(last + 1, 0);
        }

        return ConditionalCompilationBlocks.Of(parse.PrecompilerTrivia).IsConditional(at) ? null : at;
    }

    /// <summary>
    /// Lines of their own, written at the end of the declaration section of a module (<see cref="EndOfDeclarations"/>): a field, or a declaration a fix
    /// moves to module level. A procedure written directly after them gets a blank line before it.
    /// </summary>
    /// <param name="parse">The parse of the module.</param>
    /// <param name="lines">The lines, without their line ends or indentation; an empty one is a blank line.</param>
    /// <returns>The edit, or <see langword="null"/> when there is no end of the declaration section to write at (<see cref="EndOfDeclarations"/>).</returns>
    public static TextEdit? InsertLinesAfterDeclarations(ModuleParseResult parse, params string[] lines)
    {
        if (EndOfDeclarations(parse) is not { } at)
        {
            return null;
        }

        // the last line of a text that ends no line is ended before them.
        if (at.Character != 0)
        {
            return SyntaxEdits.Insert(at, string.Concat(lines.Select(line => parse.LineEnding + line)));
        }

        var procedureFollows = On(WrittenNodes(parse).ToArray(), at.Line).Length > 0;
        return SyntaxEdits.Insert(at, string.Concat(lines.Select(line => line + parse.LineEnding)) + (procedureFollows ? parse.LineEnding : string.Empty));
    }

    // the nodes that have characters on a line.
    private static SyntaxNode[] On(SyntaxNode[] written, int line)
    {
        var lineStart = new SourcePosition(line, 0);
        var nextLine = new SourcePosition(line + 1, 0);
        return [.. written.Where(node => node.SourceLocation.Range is var range && range.Start < nextLine && range.End > lineStart)];
    }

    private static bool IsAnythingElseOnTheLinesOf(ModuleParseResult parse, SourceRange span)
    {
        var linesStart = new SourcePosition(span.Start.Line, 0);
        var linesEnd = new SourcePosition(span.End.Line + 1, 0);
        foreach (var range in WrittenNodes(parse).Select(node => node.SourceLocation.Range))
        {
            // a range of no characters writes nothing, and a range that ends where the lines start, or starts where they end, writes nothing on them.
            if (range.Start == range.End || range.End <= linesStart || range.Start >= linesEnd)
            {
                continue;
            }

            if (span.Start <= range.Start && range.End <= span.End)
            {
                continue;
            }

            var isAround = range.Start <= span.Start && span.End <= range.End;
            if (isAround && (range.Start.Line < span.Start.Line || range.Start == span.Start) && (range.End.Line > span.End.Line || range.End == span.End))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // what the parse has a range for: the nodes of the tree (not the module, which holds the whole text), its trivia, and the directives under the
    // precompiler's root node.
    private static IEnumerable<SyntaxNode> WrittenNodes(ModuleParseResult parse)
        => parse.SyntaxTree!.Descendants()
            .Concat(parse.Trivia)
            .Concat(parse.PrecompilerTrivia.SelectMany(root => root.Descendants()));

    // the end of the module node, which spans the whole text; never before the end of anything written in it, for a parse whose module node has no range.
    private static SourcePosition EndOfText(ModuleParseResult parse)
        => WrittenNodes(parse).Select(node => node.SourceLocation.Range.End)
            .Aggregate(parse.SyntaxTree?.SourceLocation.Range.End ?? SourcePosition.Zero, (end, each) => each > end ? each : end);
}
