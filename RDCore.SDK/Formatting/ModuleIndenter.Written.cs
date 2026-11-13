using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics.Refactoring;
using System.Text;

namespace RDCore.SDK.Formatting;

public static partial class ModuleIndenter
{
    /// <summary>
    /// Edits of a module, with the lines they write indented as <see cref="Indent"/> indents them in the module the edits leave: a line an edit writes from
    /// its start is at the level of what encloses it there, whatever indent the edit gave it. The rest of what the edits write, and the lines they do not
    /// write, are as they were.
    /// </summary>
    /// <param name="text">The text of the module, which the edits are edits of.</param>
    /// <param name="edits">The edits, in any order; they do not overlap (<strong>LSP 3.17</strong> §TextEdit).</param>
    /// <param name="edited">
    /// The module as it is parsed from the text the edits leave (<see cref="DeclarationEdits.ApplyTo"/> of <paramref name="text"/>).
    /// </param>
    /// <param name="options">How code is indented.</param>
    /// <returns>
    /// The edits, in the order of the text. An edit whose last line is indented replaces the spaces and tabs that line goes on with after it, too.
    /// </returns>
    /// <exception cref="ArgumentException">Two of the edits overlap.</exception>
    public static IReadOnlyList<RefactorTextEdit> IndentWritten(string text, IReadOnlyList<RefactorTextEdit> edits, ModuleParseResult edited, CodeFormattingOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(options);
        var ordered = DeclarationEdits.Ordered(edits);
        var before = TextLines.Of(text);
        var after = DeclarationEdits.ApplyTo(text, ordered);
        var lines = TextLines.Of(after);

        var indented = new List<RefactorTextEdit>(ordered.Count);
        var shift = 0;
        foreach (var edit in ordered)
        {
            var (from, to) = (before.Offset(edit.Range.Start), before.Offset(edit.Range.End));
            var start = from + shift;
            shift += edit.NewText.Length - (to - from);
            indented.Add(Indented(edit, after, lines, start, start + edit.NewText.Length, edited, options));
        }

        return indented;
    }

    // an edit with the lines it writes from their start indented; what it writes is from start to end of the text the edits leave.
    private static RefactorTextEdit Indented(
        RefactorTextEdit edit, string after, TextLines lines, int start, int end, ModuleParseResult edited, CodeFormattingOptions options)
    {
        var (first, last) = (lines.PositionOf(start), lines.PositionOf(end));
        var (firstLine, lastLine) = (first.Character == 0 ? first.Line : first.Line + 1, last.Character > 0 ? last.Line : last.Line - 1);
        if (edit.NewText.Length == 0 || firstLine > lastLine)
        {
            return edit;
        }

        var written = new StringBuilder();
        var at = start;
        foreach (var (line, indent) in Indents(edited, after, lines, options, firstLine, lastLine))
        {
            var from = lines.Offset(new SourcePosition(line, 0));
            written.Append(after, at, from - at).Append(indent.Wanted);
            at = from + indent.Length;
        }

        if (at <= end)
        {
            return edit with { NewText = written.Append(after, at, end - at).ToString() };
        }

        // the indent of the last line goes on past what the edit writes, on what was after the edit on its line.
        var range = edit.Range with { End = edit.Range.End with { Character = edit.Range.End.Character + at - end } };
        return edit with { Range = range, NewText = written.ToString() };
    }

    // the lines whose indent changes, with how long their indent is and what it is to be: the edits of a line within its indent, made to that indent.
    private static IEnumerable<(int Line, (int Length, string Wanted) Indent)> Indents(
        ModuleParseResult edited, string after, TextLines lines, CodeFormattingOptions options, int firstLine, int lastLine)
    {
        foreach (var group in Indent(edited, after, options, firstLine, lastLine)
            .Where(edit => edit.Line >= firstLine && edit.Line <= lastLine && edit.EndLine == edit.Line)
            .GroupBy(edit => edit.Line)
            .OrderBy(group => group.Key))
        {
            var indent = lines.IndentOf(group.Key);
            var within = group.Where(edit => edit.EndCharacter <= indent.Length).OrderByDescending(edit => edit.Character).ToList();
            if (within.Count == 0)
            {
                continue;
            }

            var wanted = within.Aggregate(indent, (text, edit) => text[..edit.Character] + edit.NewText + text[edit.EndCharacter..]);
            yield return (group.Key, (indent.Length, wanted));
        }
    }
}
