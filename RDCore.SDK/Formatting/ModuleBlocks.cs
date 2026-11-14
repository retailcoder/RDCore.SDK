using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Statements;

namespace RDCore.SDK.Formatting;

/// <summary>
/// A block of a module that is open at one of its lines: its opener is at or before the line, and the module has no closer for it.
/// </summary>
/// <param name="Opener">The line of the block's opener, from 0: its <c>Sub</c>, <c>Function</c>, <c>Property</c>, <c>Enum</c> or <c>Type</c> line, or its
/// <c>If</c>, <c>Select Case</c>, <c>For</c>, <c>Do</c>, <c>While</c> or <c>With</c> line.</param>
/// <param name="Branch">The line that starts the block's last branch at or before the line: an <c>ElseIf</c>, an <c>Else</c>, a <c>Case</c> or a
/// <c>Case Else</c> line; <paramref name="Opener"/> when the block has no branch there.</param>
/// <param name="Closer">The keywords that close the block: <c>End Sub</c>, <c>End Function</c>, <c>End Property</c>, <c>End Enum</c>, <c>End Type</c>,
/// <c>End If</c>, <c>End Select</c>, <c>Next</c>, <c>Loop</c>, <c>Wend</c> or <c>End With</c>.</param>
/// <param name="HasBody">Whether the line comes after <paramref name="Branch"/>, so that the block's last branch has a line at or before the line.</param>
public readonly record struct ModuleBlock(int Opener, int Branch, string Closer, bool HasBody);

/// <summary>
/// The blocks of a module that a line is in and that the module does not close, and what an editor does with them when a line break is typed: it indents the
/// new line, and closes the block the line before it opens.
/// </summary>
/// <remarks>
/// <para>
/// What is a block, and which closer ends it, is what the module's syntax tree says, as <see cref="ModuleIndenter"/> has it: a <c>Sub</c>, a
/// <c>Function</c> or a <c>Property</c> (MS-VBAL §5.3.1), an <c>Enum</c> (§5.2.3.4), a <c>Type</c> (§5.2.3.3), an <c>If</c> block (§5.4.2.8), a
/// <c>Select Case</c> (§5.4.2.10), a <c>For</c> or <c>For Each</c> loop (§5.4.2.3, §5.4.2.4), a <c>Do</c> loop (§5.4.2.6), a <c>While</c> loop
/// (§5.4.2.2) and a <c>With</c> block (§5.4.2.21). A single-line <c>If</c> (§5.4.2.9) is not a block.
/// </para>
/// <para>
/// A block is closed by a statement that starts with its closer on the line it ends on, the last statement of its lines or one after a colon
/// (<c>x = 1: End If</c>); a closer that ends a block within it does not close it as well. A <c>Next</c> that lists names closes one loop for each name.
/// </para>
/// </remarks>
public static class ModuleBlocks
{
    /// <summary>
    /// The blocks a line of a module is in that the module has no closer for: the line's own block when it opens one, and each block it is in.
    /// </summary>
    /// <param name="parse">The module, as it was parsed from <paramref name="text"/>.</param>
    /// <param name="text">The text of the module. A line ends with a carriage return, a line feed, or both, as the Language Server Protocol counts them.</param>
    /// <param name="line">The line, from 0.</param>
    /// <returns>The open blocks, the innermost first; none when the line is in no open block, or is not a line of the text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parse"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<ModuleBlock> OpenAt(ModuleParseResult parse, string text, int line)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(text);
        var lines = ModuleIndenter.LinesIn(text);
        if (line < 0 || line >= lines.Length)
        {
            return [];
        }

        var open = new List<ModuleBlock>();
        foreach (var (block, closer) in ModuleIndenter.UnclosedBlocks(parse, lines))
        {
            var range = block.SourceLocation.Range;
            var last = range.End.Character == 0 && range.End.Line > range.Start.Line ? range.End.Line - 1 : range.End.Line;
            if (range.Start.Line > line || last < line)
            {
                continue;
            }

            var branch = Branches(block).Where(start => start <= line).DefaultIfEmpty(range.Start.Line).Max();
            open.Add(new ModuleBlock(range.Start.Line, branch, closer, line > branch));
        }

        // the tree's order has a block before the blocks in it: the innermost is the last.
        open.Reverse();
        return open;
    }

    /// <summary>
    /// The edits a line break typed into a module brings, as an editor makes them once it has the new line: the line the break ends and the new line are
    /// indented as <see cref="ModuleIndenter"/> has them, the new line even when it is empty; and when the line before the new one opens a block that the module
    /// has no closer for, and the new line is empty, the block's closer is written on a line of its own after it, at the level of the opener.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edits leave the new line where it is, and what is written on it: the closer goes on the line after it, so that what the user types next is the
    /// block's body. A block that has its closer further down, as one does when its opener is typed again, is not closed a second time.
    /// </para>
    /// <para>
    /// The lines are indented as the module is once the closer is in it, which is parsed again for that: the new line is then in the block's body, as a
    /// procedure's is although the module has nothing after its header yet.
    /// </para>
    /// </remarks>
    /// <param name="parse">The module, as it was parsed from <paramref name="text"/>, the line break in it.</param>
    /// <param name="text">The text of the module, the line break in it. A line ends with a carriage return, a line feed, or both, as the Language Server
    /// Protocol counts them.</param>
    /// <param name="line">The line the break starts, from 0.</param>
    /// <param name="options">How code is indented.</param>
    /// <param name="reparse">Parses the text of the module with the closer in it.</param>
    /// <param name="token">A token that cancels the parse.</param>
    /// <returns>The edits, in the order they are to be made: by line, then by character, and the closer's insertion after the edits of the new line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parse"/>, <paramref name="text"/>, <paramref name="options"/> or <paramref name="reparse"/> is
    /// <see langword="null"/>.</exception>
    public static async Task<IReadOnlyList<TextEdit>> OnLineBreakAsync(ModuleParseResult parse, string text, int line, CodeFormattingOptions options,
        Func<string, CancellationToken, Task<ModuleParseResult>> reparse, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reparse);
        var lines = ModuleIndenter.LinesIn(text);
        if (line < 1 || line >= lines.Length)
        {
            return [];
        }

        // the empty line indented as the code around it is; the procedures left as they are spaced, which a line break does not change.
        var indenting = options with { EmptyLineHandlingMethod = EmptyLineHandlingMethod.Indent, VerticallySpaceProcedures = false };
        if (!string.IsNullOrWhiteSpace(lines[line]) || OpenAt(parse, text, line - 1) is not [{ Opener: var opener, HasBody: false } block, ..] || opener != line - 1)
        {
            return [.. ModuleIndenter.Indent(parse, text, indenting, line - 1, line).Select(Edit)];
        }

        // the closer after the new line, as the opener is indented now: the module with it in it is indented again, the closer's line with the others.
        var ends = ModuleIndenter.LineEndsIn(text);
        var written = lines[opener][..(lines[opener].Length - lines[opener].TrimStart().Length)] + block.Closer;
        var inserted = line + 1 < lines.Length
            ? (At: new Range(line + 1, 0, line + 1, 0), Text: written + ends[line])
            : (At: new Range(line, lines[line].Length, line, lines[line].Length), Text: ends[line - 1] + written);
        var at = Enumerable.Range(0, inserted.At.Start.Line).Sum(index => lines[index].Length + ends[index].Length) + inserted.At.Start.Character;
        var closed = text.Insert(at, inserted.Text);
        var indentation = ModuleIndenter.Indent(await reparse(closed, token), closed, indenting, line - 1, line + 1);
        var closer = ModuleIndenter.LinesIn(ModuleIndenter.Apply(closed, indentation))[line + 1];
        return [.. indentation.Where(edit => edit.Line <= line).Select(Edit),
            new TextEdit { Range = inserted.At, NewText = line + 1 < lines.Length ? closer + ends[line] : ends[line - 1] + closer }];
    }

    private static TextEdit Edit(IndentationEdit edit)
        => new() { Range = new Range(edit.Line, edit.Character, edit.EndLine, edit.EndCharacter), NewText = edit.NewText };

    // the lines a block's branches start on.
    private static IEnumerable<int> Branches(SyntaxNode block)
        => block switch
        {
            IfBlockStatementNode ifBlock => ifBlock.ElseIfBlocks.Select(StartOf).Concat(ifBlock.ElseBlock is { } elseBlock ? [StartOf(elseBlock)] : []),
            SelectCaseStatementNode select => select.CaseExpressionBlocks.Select(StartOf).Concat(select.CaseElseBlock is { } caseElse ? [StartOf(caseElse)] : []),
            _ => [],
        };

    private static int StartOf(StatementNode node) => node.SourceLocation.Range.Start.Line;
}
