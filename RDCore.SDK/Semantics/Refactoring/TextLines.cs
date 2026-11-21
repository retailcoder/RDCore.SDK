using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using System.Text;
using System.Text.RegularExpressions;

namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The lines of a text, read by the positions a syntax tree gives (<see cref="SourcePosition"/>: a zero-based line, and a character in it): where each line
/// starts, what is written on it, and what ends it. What a refactoring reads of a module's text before it writes its edits.
/// </summary>
/// <remarks>
/// A line ends at a carriage return and a line feed, a line feed, or a carriage return (<strong>LSP 3.17</strong> §Position). A character counts UTF-16
/// code units. A position is one of the text's: its line is one the text has, and its character is at most the length of that line. A line that the text
/// does not have throws <see cref="ArgumentOutOfRangeException"/>.
/// <para>
/// A blank is a space or a tab: the white space the parser's lexer reads between tokens. The <c>WSC</c> of <strong>MS-VBAL §3.2.2</strong> also has
/// U+0019, the ideographic space U+3000 and the other spaces of Unicode class Zs, which the lexer does not read as white space and which are not blanks here.
/// A line end is never a blank.
/// </para>
/// </remarks>
public sealed partial class TextLines
{
    private readonly string _text;
    private readonly List<int> _starts = [0];
    private readonly List<int> _lengths = [];

    private TextLines(string text)
    {
        _text = text;
        string? newLine = null;
        foreach (Match end in Ends().Matches(text))
        {
            newLine ??= end.Value;
            _lengths.Add(end.Index - _starts[^1]);
            _starts.Add(end.Index + end.Length);
        }

        _lengths.Add(text.Length - _starts[^1]);
        NewLine = newLine ?? "\r\n";
    }

    /// <summary>
    /// The lines of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Its lines.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static TextLines Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(text);
    }

    /// <summary>
    /// What ends the first line that is ended: what a refactoring ends the lines it writes with. A carriage return and a line feed when no line is ended.
    /// </summary>
    public string NewLine { get; }

    /// <summary>
    /// How many lines the text has, the last one included, which is empty when the text ends its last line.
    /// </summary>
    public int Count => _starts.Count;

    /// <summary>
    /// Whether the text ends its last line: its last line is empty, and is not its only one.
    /// </summary>
    public bool EndsWithNewLine => _lengths[^1] == 0 && Count > 1;

    /// <summary>
    /// The position past the last character of the text.
    /// </summary>
    public SourcePosition End => new(Count - 1, _lengths[^1]);

    private const string Blanks = " \t";

    /// <summary>
    /// Whether nothing but spaces and tabs is written on a line.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> for a line that is empty or has only spaces and tabs.</returns>
    public bool IsBlank(int line) => _text.AsSpan(_starts[line], _lengths[line]).TrimStart(Blanks).IsEmpty;

    /// <summary>
    /// Whether nothing but spaces and tabs is written on a position's line before it: the position starts what is written on the line.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns><see langword="true"/> when the line has only spaces and tabs before the position, or nothing.</returns>
    public bool IsBlankBefore(SourcePosition position) => _text.AsSpan(_starts[position.Line], position.Character).TrimStart(Blanks).IsEmpty;

    /// <summary>
    /// Whether nothing but spaces and tabs is written on a position's line after it: the position ends what is written on the line.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns><see langword="true"/> when the line has only spaces and tabs after the position, or nothing.</returns>
    public bool IsBlankAfter(SourcePosition position)
        => _text.AsSpan(_starts[position.Line] + position.Character, _lengths[position.Line] - position.Character).TrimStart(Blanks).IsEmpty;

    /// <summary>
    /// What is written on a position's line after it, without what ends the line.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns>The rest of the line.</returns>
    public string After(SourcePosition position) => _text.Substring(_starts[position.Line] + position.Character, _lengths[position.Line] - position.Character);

    /// <summary>
    /// The spaces and tabs a line starts with: its indentation.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The spaces and tabs before the first character of the line that is neither; the whole line, when it is blank.</returns>
    public string IndentOf(int line)
    {
        var text = _text.AsSpan(_starts[line], _lengths[line]);
        return text[..(text.Length - text.TrimStart(Blanks).Length)].ToString();
    }

    /// <summary>
    /// The position past the last character of a line, before what ends it.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The end of the line.</returns>
    public SourcePosition EndOf(int line) => new(line, _lengths[line]);

    /// <summary>
    /// What ends a line: a carriage return and a line feed, a line feed, or a carriage return.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>What ends it; an empty string for the last line, which nothing ends.</returns>
    public string NewLineOf(int line)
        => line + 1 < Count ? _text.Substring(_starts[line] + _lengths[line], _starts[line + 1] - _starts[line] - _lengths[line]) : string.Empty;

    /// <summary>
    /// The first position of the line after a line.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The start of the next line; the end of the text, for the last line.</returns>
    public SourcePosition StartOfLineAfter(int line) => line + 1 < Count ? new SourcePosition(line + 1, 0) : EndOf(line);

    /// <summary>
    /// The range of whole lines, from one to another: what a refactoring removes when it removes them.
    /// </summary>
    /// <param name="first">The first line.</param>
    /// <param name="last">The last line, at or after <paramref name="first"/>.</param>
    /// <returns>
    /// The lines with what ends the last one; when the last one is the text's own last line, and is not the first line of the text, the lines with what ends
    /// the line before them instead, so that the text is left with no line end of its own after them.
    /// </returns>
    public SourceRange WholeLines(int first, int last)
        => last + 1 < Count || first == 0 ? new SourceRange(new SourcePosition(first, 0), StartOfLineAfter(last)) : new SourceRange(EndOf(first - 1), EndOf(last));

    /// <summary>
    /// The text of a range.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <returns>What is written from its start to its end, line ends included.</returns>
    public string Of(SourceRange range) => _text[Offset(range.Start)..Offset(range.End)];

    /// <summary>
    /// The text of a range with edits made in it, as it reads once they are made.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <param name="edits">
    /// The edits, in any order, each within <paramref name="range"/> and placed by the text as it is before any of them; none overlaps another
    /// (<strong>LSP 3.17</strong> §TextEdit). Edits that insert at the same position are written in the order they are given.
    /// </param>
    /// <returns>The text of the range, edited.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="edits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Two of the edits overlap, an edit ends before it starts, or an edit is not within <paramref name="range"/>.
    /// </exception>
    public string Of(SourceRange range, IEnumerable<RefactorTextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var (start, end) = (Offset(range.Start), Offset(range.End));
        var edited = new StringBuilder(end - start);
        var at = start;
        foreach (var (from, to, newText, _) in Placed(edits))
        {
            if (from < start || to > end)
            {
                throw new ArgumentException("An edit is not within the range.", nameof(edits));
            }

            edited.Append(_text, at, from - at).Append(newText);
            at = to;
        }

        return edited.Append(_text, at, end - at).ToString();
    }

    /// <summary>
    /// The text with edits made, and where each edit's text is written in it: what a refactoring checks the edited text by, at the places it wrote.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="edits">
    /// The edits, in any order, each placed by the text as it is before any of them; none overlaps another (<strong>LSP 3.17</strong> §TextEdit). Edits that
    /// insert at the same position are written in the order they are given.
    /// </param>
    /// <returns>The edited text, and the range of the edited text each edit writes, in the order of <paramref name="edits"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="edits"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two of the edits overlap, or an edit ends before it starts.</exception>
    public static EditedText Apply(string text, IReadOnlyList<RefactorTextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var lines = Of(text);
        var offsets = new (int Start, int End)[edits.Count];
        var edited = new StringBuilder(text.Length);
        var at = 0;
        foreach (var (from, to, newText, index) in lines.Placed(edits))
        {
            edited.Append(text, at, from - at);
            offsets[index] = (edited.Length, edited.Length + newText.Length);
            edited.Append(newText);
            at = to;
        }

        var result = edited.Append(text, at, text.Length - at).ToString();
        var written = Of(result);
        return new(result, [.. offsets.Select(offset => new SourceRange(written.PositionOf(offset.Start), written.PositionOf(offset.End)))]);
    }

    /// <summary>
    /// The offset of a position in the text: how many UTF-16 code units are written before it.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns>Its offset.</returns>
    public int Offset(SourcePosition position) => _starts[position.Line] + position.Character;

    /// <summary>
    /// The position of an offset of the text.
    /// </summary>
    /// <param name="offset">The offset, from 0 to the length of the text.</param>
    /// <returns>The position on the line that starts at the offset or the last one before it.</returns>
    public SourcePosition PositionOf(int offset)
    {
        var line = _starts.BinarySearch(offset);
        line = line >= 0 ? line : ~line - 1;
        return new SourcePosition(line, offset - _starts[line]);
    }

    // the text of a member or a declaration without what ends its last line.
    internal static string Unended(string text) => TrailingLineEnd().Replace(text, string.Empty);

    // the edits by their offsets, in the order of the text, then of the edits; two that overlap are refused.
    private (int From, int To, string NewText, int Index)[] Placed(IEnumerable<RefactorTextEdit> edits)
    {
        var placed = edits.Select((edit, index) => (From: Offset(edit.Range.Start), To: Offset(edit.Range.End), edit.NewText, Index: index))
            .OrderBy(edit => edit.From).ThenBy(edit => edit.Index)
            .ToArray();
        for (var index = 0; index < placed.Length; index++)
        {
            if (placed[index].To < placed[index].From || index > 0 && placed[index].From < placed[index - 1].To)
            {
                throw new ArgumentException("Two of the edits overlap, or an edit ends before it starts.", nameof(edits));
            }
        }

        return placed;
    }

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex Ends();

    [GeneratedRegex(@"(\r\n|\r|\n)\z")]
    private static partial Regex TrailingLineEnd();
}

/// <summary>
/// A text with edits made (<see cref="TextLines.Apply"/>), and where each edit's text is written in it.
/// </summary>
/// <param name="Text">The edited text.</param>
/// <param name="Written">
/// The range of <paramref name="Text"/> each edit writes, in the order of the edits; an empty range, where it was, for an edit that writes nothing.
/// </param>
public sealed record class EditedText(string Text, IReadOnlyList<SourceRange> Written);
