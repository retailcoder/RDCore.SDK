using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Workspace;

namespace RDCore.SDK.Formatting;

/// <summary>
/// A block of a program of the platform's BASIC (<see cref="BasicProgramText"/>) that is open at one of its lines: its opener is at or before the line, and
/// the program has no closer for it yet.
/// </summary>
/// <param name="Opener">The index of the block's opener in the program's statements: its <c>If</c>, <c>Select Case</c>, <c>For</c>, <c>Do</c>, <c>While</c>
/// or <c>With</c> line.</param>
/// <param name="Branch">The index of the line the block's last branch at or before the line starts on: an <c>ElseIf</c>, an <c>Else</c>, a <c>Case</c> or a
/// <c>Case Else</c> line; the opener's when the block has no branch there.</param>
/// <param name="Closer">The keywords that close the block: <c>End If</c>, <c>End Select</c>, <c>Next</c>, <c>Loop</c>, <c>Wend</c> or <c>End With</c>.</param>
/// <param name="HasBody">Whether the block's last branch has a statement at or before the line: the line is not <paramref name="Branch"/>.</param>
public readonly record struct ProgramBlock(int Opener, int Branch, string Closer, bool HasBody);

/// <summary>
/// The blocks of a program of the platform's BASIC (<see cref="BasicProgramText"/>) that a line is in and that are not closed yet, as a shell offers to close
/// them while the program is typed one numbered line at a time.
/// </summary>
/// <remarks>
/// <para>
/// What is a block, and which closer ends it, is what the program's syntax tree says, as <see cref="ProgramIndenter"/> has it: an <c>If</c> block (MS-VBAL
/// 5.4.2.8), a <c>Select Case</c> (5.4.2.10), a <c>For</c> or <c>For Each</c> loop (5.4.2.3, 5.4.2.4), a <c>Do</c> loop (5.4.2.6), a <c>While</c> loop
/// (5.4.2.2) and a <c>With</c> block (5.4.2.21). A one-line <c>If</c> (5.4.2.9) is no block.
/// </para>
/// <para>
/// A block is closed by a statement that starts with its closer on the line it ends on, the last statement of its lines or one after a colon
/// (<c>x = 1: End If</c>); a closer that ends a block within it does not close it as well. A <c>Next</c> that lists names closes one loop for each name.
/// </para>
/// </remarks>
public static class ProgramBlocks
{
    private const string NewLine = "\r\n";

    /// <summary>
    /// The blocks a line of a program is in that the program has no closer for: the line's own block when it opens one, and each block it is in.
    /// </summary>
    /// <param name="statements">The statements of the program, in the order of their line numbers, without their numbers.</param>
    /// <param name="index">The index of the line in <paramref name="statements"/>.</param>
    /// <param name="parse">Parses the text of a module: the program's statements as the body of its procedure.</param>
    /// <returns>
    /// The open blocks, the innermost first; none when the line is in no open block, or when a statement has a line end in it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="statements"/> or <paramref name="parse"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not that of a statement of <paramref name="statements"/>.</exception>
    public static IReadOnlyList<ProgramBlock> OpenAt(IReadOnlyList<string> statements, int index, Func<string, ModuleParseResult> parse)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, statements.Count);
        if (statements.Any(statement => statement.Contains('\n') || statement.Contains('\r')))
        {
            return [];
        }

        var text = BasicProgramText.ToModuleSource(string.Join(NewLine, statements.Select(statement => statement.TrimStart())) + NewLine);
        var lines = text.Split(NewLine);
        var first = BasicProgramText.HeaderLines;
        var line = index + first;
        var open = new List<ProgramBlock>();
        foreach (var (block, closer) in ModuleIndenter.UnclosedBlocks(parse(text), lines))
        {
            var range = block.SourceLocation.Range;
            var last = range.End.Character == 0 && range.End.Line > range.Start.Line ? range.End.Line - 1 : range.End.Line;
            if (range.Start.Line > line || last < line)
            {
                continue;
            }

            var branch = Branches(block).Where(start => start <= line).DefaultIfEmpty(range.Start.Line).Max();
            open.Add(new ProgramBlock(range.Start.Line - first, branch - first, closer, line > branch));
        }

        // the tree's order has a block before the blocks in it: the innermost is the last.
        open.Reverse();
        return open;
    }

    // the lines a block's branches start on.
    private static IEnumerable<int> Branches(StatementNode block)
        => block switch
        {
            IfBlockStatementNode ifBlock => ifBlock.ElseIfBlocks.Select(StartOf).Concat(ifBlock.ElseBlock is { } elseBlock ? [StartOf(elseBlock)] : []),
            SelectCaseStatementNode select => select.CaseExpressionBlocks.Select(StartOf).Concat(select.CaseElseBlock is { } caseElse ? [StartOf(caseElse)] : []),
            _ => [],
        };

    private static int StartOf(StatementNode node) => node.SourceLocation.Range.Start.Line;
}
