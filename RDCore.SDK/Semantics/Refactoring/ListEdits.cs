using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The edits of a list of items separated by commas: the parameters of a procedure (<strong>MS-VBAL §5.3.1.5</strong>), the arguments of a call
/// (<strong>MS-VBAL §5.6.13.1</strong>). They keep the line continuations and the spacing of the items that stay.
/// </summary>
/// <remarks>
/// A list is the text between the parentheses that hold it (<c>Sub F(a, b)</c>, <c>F(a, b)</c>), or the text after the name of the procedure a call
/// statement calls without them (<c>F a, b</c>), up to the end of the statement. A comma in a string literal (<c>"a, b"</c>), in parentheses
/// (<c>G(1, 2)</c>) or in brackets (<c>[a, b]</c>) separates no item of the list; an apostrophe outside a string literal starts a comment, which ends the
/// list. An item can be empty: an argument that is omitted (<c>F a, , c</c>).
/// </remarks>
public static class ListEdits
{
    /// <summary>
    /// The items of a list: the range of each, without the blanks and the line continuations around it, in the order they are written.
    /// </summary>
    /// <param name="text">The text of the module.</param>
    /// <param name="list">The range of the list in <paramref name="text"/>: inside its parentheses, or after the name a call statement calls.</param>
    /// <returns>
    /// The ranges, one per item. An empty item is the empty range at the comma after it, or at the end of the list. A list with no text but blanks has no
    /// item.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="list"/> is not a range of <paramref name="text"/>.</exception>
    public static IReadOnlyList<SourceRange> ItemsOf(string text, SourceRange list)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = TextLines.Of(text);
        return [.. Items(text, lines, list).Select(item => new SourceRange(lines.PositionOf(item.Start), lines.PositionOf(item.End)))];
    }

    /// <summary>
    /// The edit that removes one item of a list, with the comma that separates it from the others. An item that is not the last is removed up to the item
    /// after it. The last is removed from the end of the item before it that is not empty, so the omitted arguments before it go with it: <c>F a, , c</c>
    /// without <c>c</c> is <c>F a</c>. Without its only item that is not empty, the list is empty: <c>F(a)</c> becomes <c>F()</c>, and <c>F a</c> becomes
    /// <c>F</c>.
    /// </summary>
    /// <param name="text">The text of the module.</param>
    /// <param name="list">The range of the list in <paramref name="text"/> (<see cref="ItemsOf"/>).</param>
    /// <param name="index">The position of the item in the list, from 0.</param>
    /// <returns>The edit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="list"/> is not a range of <paramref name="text"/>, or the list has no item at <paramref name="index"/>.
    /// </exception>
    public static RefactorTextEdit RemoveItem(string text, SourceRange list, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = TextLines.Of(text);
        var items = Items(text, lines, list);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, items.Count);

        var before = index - 1;
        while (before >= 0 && items[before].End == items[before].Start)
        {
            before--;
        }

        var (start, end) = index < items.Count - 1 ? (items[index].Start, items[index + 1].Start)
            : before >= 0 ? (items[before].End, items[index].End)
            : (lines.Offset(list.Start), items[index].End);

        return new RefactorTextEdit { Range = new SourceRange(lines.PositionOf(start), lines.PositionOf(end)), NewText = string.Empty };
    }

    // the items, as offsets: split at each comma outside a string literal, parentheses and brackets, up to a comment.
    private static List<(int Start, int End)> Items(string text, TextLines lines, SourceRange list)
    {
        if (list.End < list.Start || list.End > lines.End)
        {
            throw new ArgumentOutOfRangeException(nameof(list), "The range is not one of the text.");
        }

        var (from, to) = (lines.Offset(list.Start), lines.Offset(list.End));
        var items = new List<(int Start, int End)>();
        var (depth, inString, start) = (0, false, from);
        var at = from;
        for (; at < to; at++)
        {
            var c = text[at];
            if (inString)
            {
                inString = c != '"';
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    items.Add(Trimmed(text, start, at));
                    start = at + 1;
                    break;
                case '\'':
                    to = at;
                    break;
            }
        }

        var last = Trimmed(text, start, to);
        if (items.Count > 0 || last.End > last.Start)
        {
            items.Add(last);
        }

        return items;
    }

    // an item without the blanks, line ends and line continuations around it; an empty one is at its end.
    private static (int Start, int End) Trimmed(string text, int start, int end)
    {
        while (end > start && IsBlank(text[end - 1]) || end - 1 > start && text[end - 1] == '_' && IsBlank(text[end - 2]))
        {
            end--;
        }

        while (start < end && (IsBlank(text[start]) || text[start] == '_' && (start + 1 == end || IsBlank(text[start + 1]))))
        {
            start++;
        }

        return start == end ? (end, end) : (start, end);
    }

    private static bool IsBlank(char c) => c is ' ' or '\t' or '\r' or '\n';
}
