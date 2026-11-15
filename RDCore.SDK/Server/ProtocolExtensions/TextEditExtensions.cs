using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace RDCore.SDK.Server.ProtocolExtensions;

/// <summary>
/// The <see cref="TextEdit"/>s of a document, made on its text as a client makes them (<strong>LSP 3.17</strong> §TextEdit): what a fix, a refactoring or a
/// formatting makes of the text, without a client.
/// </summary>
public static class TextEditExtensions
{
    /// <summary>
    /// The text, with the edits made. Every edit is placed by the text as it was before any of them (<strong>LSP 3.17</strong> §TextEdit), so the order of the
    /// edits matters only for edits that insert at the same position.
    /// </summary>
    /// <param name="edits">The edits, none of them overlapping another. Edits that insert at the same position are written in the order they are given.</param>
    /// <param name="text">The text the edits are of.</param>
    /// <returns>The edited text; <paramref name="text"/> itself when there are no edits.</returns>
    /// <remarks>
    /// A line ends at a <c>\r\n</c>, a <c>\n</c> or a <c>\r</c> (<strong>LSP 3.17</strong> §Position). A character offset counts UTF-16 code units, the default
    /// position encoding of the protocol. A position past the end of its line is the end of the line, before its line end; a line past the last one is the end
    /// of the text.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="edits"/> or <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two edits overlap, or an edit ends before it starts.</exception>
    public static string ApplyTo(this IEnumerable<TextEdit> edits, string text)
    {
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(text);

        var lines = LinesOf(text);
        var placed = edits
            .Select((edit, index) => (Start: OffsetOf(text, lines, edit.Range.Start), End: OffsetOf(text, lines, edit.Range.End), edit.NewText, Index: index))
            .OrderBy(edit => edit.Start).ThenBy(edit => edit.Index)
            .ToArray();
        if (placed.Length == 0)
        {
            return text;
        }

        var edited = new StringBuilder(text.Length);
        var at = 0;
        foreach (var (start, end, newText, _) in placed)
        {
            if (end < start)
            {
                throw new ArgumentException("An edit ends before it starts.", nameof(edits));
            }

            if (start < at)
            {
                throw new ArgumentException("Two edits overlap.", nameof(edits));
            }

            edited.Append(text, at, start - at).Append(newText);
            at = end;
        }

        return edited.Append(text, at, text.Length - at).ToString();
    }

    // where each line starts, and where its text ends, before its line end.
    private static List<(int Start, int End)> LinesOf(string text)
    {
        var lines = new List<(int Start, int End)>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is '\r' or '\n')
            {
                lines.Add((start, index));
                if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                start = index + 1;
            }
        }

        lines.Add((start, text.Length));
        return lines;
    }

    private static int OffsetOf(string text, List<(int Start, int End)> lines, Position position)
        => position.Line >= lines.Count
            ? text.Length
            : Math.Min(lines[position.Line].Start + Math.Max(position.Character, 0), lines[position.Line].End);
}
