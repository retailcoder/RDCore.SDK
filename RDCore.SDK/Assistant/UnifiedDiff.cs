using System.Text;

namespace RDCore.SDK.Assistant;

/// <summary>
/// The change of a document's text as a unified diff: what <see cref="ToolFileChange.Diff"/> says of a document a fix or a rename changes.
/// </summary>
/// <remarks>
/// <para>
/// The diff is in the form <c>diff -u</c> writes: a <c>---</c> and a <c>+++</c> line naming the document, then one hunk per run of changed lines, each with
/// up to <c>context</c> unchanged lines before and after it, and a <c>@@ -start,count +start,count @@</c> header (the count left out when it is 1). A hunk's
/// lines start with a space (unchanged), <c>-</c> (removed) or <c>+</c> (added). A last line with no line break after it is followed by
/// <c>\ No newline at end of file</c>.
/// </para>
/// <para>
/// The texts are compared line by line, whatever ends their lines (<c>CRLF</c>, <c>LF</c> or <c>CR</c>), and the diff's lines end with <c>LF</c>: two
/// texts whose lines end with different line breaks have no diff. A last line that gains or loses its line break is a change. Two runs of changes with no
/// more than twice the context between them are one hunk.
/// </para>
/// </remarks>
public static class UnifiedDiff
{
    // past this many line pairs, the changed lines are not matched up: they are all removed, then all added. The edits of a fix touch few lines, so this is
    // only reached by a whole document rewritten.
    private const long MostPairs = 4_000_000;

    private readonly record struct Line(string Text, bool HasBreak);

    private enum Op { Same, Removed, Added }

    /// <summary>
    /// The unified diff of a document's text, before and after a change.
    /// </summary>
    /// <param name="path">The document, as the diff's <c>---</c> and <c>+++</c> lines name it.</param>
    /// <param name="before">The text before the change.</param>
    /// <param name="after">The text after the change.</param>
    /// <param name="context">How many unchanged lines each hunk shows before and after its changes; 3 by default, as <c>diff -u</c> shows.</param>
    /// <returns>The diff; an empty string when the texts have the same lines.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="context"/> is negative.</exception>
    public static string Of(string path, string before, string after, int context = 3)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentOutOfRangeException.ThrowIfNegative(context);

        var old = Lines(before);
        var @new = Lines(after);
        var ops = Script(old, @new);
        if (ops.All(op => op == Op.Same))
        {
            return string.Empty;
        }

        var diff = new StringBuilder().Append("--- ").Append(path).Append('\n').Append("+++ ").Append(path).Append('\n');
        foreach (var (from, to) in Hunks(ops, context))
        {
            WriteHunk(diff, ops, from, to, old, @new);
        }

        return diff.ToString();
    }

    private static List<Line> Lines(string text)
    {
        var lines = new List<Line>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '\r' or '\n')
            {
                lines.Add(new(text[start..i], true));
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(new(text[start..], false));
        }

        return lines;
    }

    // the edit script: the lines both texts have in common, longest first, found between the lines they start and end with alike.
    private static List<Op> Script(List<Line> old, List<Line> @new)
    {
        var head = 0;
        while (head < old.Count && head < @new.Count && old[head] == @new[head])
        {
            head++;
        }

        var tail = 0;
        while (tail < old.Count - head && tail < @new.Count - head && old[^(tail + 1)] == @new[^(tail + 1)])
        {
            tail++;
        }

        var ops = new List<Op>(old.Count + @new.Count);
        ops.AddRange(Enumerable.Repeat(Op.Same, head));
        ops.AddRange(Middle(old.GetRange(head, old.Count - head - tail), @new.GetRange(head, @new.Count - head - tail)));
        ops.AddRange(Enumerable.Repeat(Op.Same, tail));
        return ops;
    }

    private static IEnumerable<Op> Middle(List<Line> old, List<Line> @new)
    {
        if ((long)old.Count * @new.Count > MostPairs)
        {
            return Enumerable.Repeat(Op.Removed, old.Count).Concat(Enumerable.Repeat(Op.Added, @new.Count));
        }

        // the longest common subsequence of what is left, from the end: common[i, j] is that of old[i..] and new[j..].
        var common = new int[old.Count + 1, @new.Count + 1];
        for (var i = old.Count - 1; i >= 0; i--)
        {
            for (var j = @new.Count - 1; j >= 0; j--)
            {
                common[i, j] = old[i] == @new[j] ? common[i + 1, j + 1] + 1 : Math.Max(common[i + 1, j], common[i, j + 1]);
            }
        }

        var ops = new List<Op>(old.Count + @new.Count);
        int o = 0, n = 0;
        while (o < old.Count || n < @new.Count)
        {
            if (o < old.Count && n < @new.Count && old[o] == @new[n])
            {
                ops.Add(Op.Same);
                o++;
                n++;
            }
            else if (n == @new.Count || (o < old.Count && common[o + 1, n] >= common[o, n + 1]))
            {
                ops.Add(Op.Removed);
                o++;
            }
            else
            {
                ops.Add(Op.Added);
                n++;
            }
        }

        return ops;
    }

    // each hunk as a span of the script: its changes, with the context around them, joined to the next when they would overlap or touch.
    private static IEnumerable<(int From, int To)> Hunks(List<Op> ops, int context)
    {
        var changed = Enumerable.Range(0, ops.Count).Where(i => ops[i] != Op.Same).ToList();
        var from = Math.Max(0, changed[0] - context);
        var to = Math.Min(ops.Count, changed[0] + 1 + context);
        foreach (var at in changed.Skip(1))
        {
            if (at - context <= to)
            {
                to = Math.Min(ops.Count, at + 1 + context);
                continue;
            }

            yield return (from, to);
            from = at - context;
            to = Math.Min(ops.Count, at + 1 + context);
        }

        yield return (from, to);
    }

    private static void WriteHunk(StringBuilder diff, List<Op> ops, int from, int to, List<Line> old, List<Line> @new)
    {
        // where the hunk starts in each text: the lines of that text the script has before it.
        var oldStart = ops.Take(from).Count(op => op != Op.Added);
        var newStart = ops.Take(from).Count(op => op != Op.Removed);
        var hunk = ops.Skip(from).Take(to - from).ToList();
        var oldCount = hunk.Count(op => op != Op.Added);
        var newCount = hunk.Count(op => op != Op.Removed);

        diff.Append("@@ -").Append(Span(oldStart, oldCount)).Append(" +").Append(Span(newStart, newCount)).Append(" @@\n");
        int o = oldStart, n = newStart;
        foreach (var op in hunk)
        {
            Line line;
            if (op == Op.Added)
            {
                line = @new[n++];
            }
            else
            {
                line = old[o++];
                n += op == Op.Same ? 1 : 0;
            }

            diff.Append(op switch { Op.Same => ' ', Op.Removed => '-', _ => '+' }).Append(line.Text).Append('\n');
            if (!line.HasBreak)
            {
                diff.Append("\\ No newline at end of file\n");
            }
        }
    }

    // a hunk's start, 1-based, and its count, left out when it is 1; an empty span starts at the line before it.
    private static string Span(int start, int count) => count switch
    {
        0 => $"{start},0",
        1 => $"{start + 1}",
        _ => $"{start + 1},{count}",
    };
}
