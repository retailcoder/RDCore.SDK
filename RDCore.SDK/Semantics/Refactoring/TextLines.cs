using RDCore.SDK.Model.Source;
using System.Text.RegularExpressions;

namespace RDCore.SDK.Semantics.Refactoring;

// the lines of a text: where each starts, how long it is without what ends it, and what ends the first one that is ended.
internal sealed partial class TextLines
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

    public static TextLines Of(string text) => new(text);

    public string NewLine { get; }

    // the lines, the last one included, which is empty when the text ends its last line.
    public int Count => _starts.Count;

    public bool EndsWithNewLine => _lengths[^1] == 0 && Count > 1;

    public SourcePosition End => new(Count - 1, _lengths[^1]);

    public bool IsBlank(int line) => _text.AsSpan(_starts[line], _lengths[line]).IsWhiteSpace();

    // whether nothing but blanks is written before a position on its line.
    public bool IsBlankBefore(SourcePosition position) => _text.AsSpan(_starts[position.Line], position.Character).IsWhiteSpace();

    // what is written after a position on its line.
    public string After(SourcePosition position) => _text.Substring(_starts[position.Line] + position.Character, _lengths[position.Line] - position.Character);

    // the blanks a line starts with.
    public string IndentOf(int line)
    {
        var text = _text.AsSpan(_starts[line], _lengths[line]);
        var length = 0;
        while (length < text.Length && char.IsWhiteSpace(text[length]))
        {
            length++;
        }

        return text[..length].ToString();
    }

    // the position of what ends a line: past its last character.
    public SourcePosition EndOf(int line) => new(line, _lengths[line]);

    // the first character of the line after a line; the end of the text, on its last line.
    public SourcePosition StartOfLineAfter(int line) => line + 1 < Count ? new SourcePosition(line + 1, 0) : EndOf(line);

    // the whole lines from one to another, what ends the last one included; on the last line of the text, what ends the line before them instead.
    public SourceRange WholeLines(int first, int last)
        => last + 1 < Count || first == 0 ? new SourceRange(new SourcePosition(first, 0), StartOfLineAfter(last)) : new SourceRange(EndOf(first - 1), EndOf(last));

    public string Of(SourceRange range) => _text[Offset(range.Start)..Offset(range.End)];

    private int Offset(SourcePosition position) => _starts[position.Line] + position.Character;

    // the text of a member or a declaration without what ends its last line.
    public static string Unended(string text) => TrailingLineEnd().Replace(text, string.Empty);

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex Ends();

    [GeneratedRegex(@"(\r\n|\r|\n)\z")]
    private static partial Regex TrailingLineEnd();
}
