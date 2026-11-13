using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using System.Text.RegularExpressions;

namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The whole text of a member of a module, as a refactoring that removes, inserts or moves it takes it (<see cref="MemberEdits"/>).
/// </summary>
/// <param name="Member">The member.</param>
/// <param name="Range">
/// Its whole lines: the comments directly above it, each on lines of its own with no blank line between them and the member (the annotations among them), its
/// header, its body, which holds its <c>Attribute Member.VB_*</c> lines, and its last line with what ends that line. It starts at the first character of its first
/// line, and ends at the first character of the line after its last, or at the end of the text.
/// </param>
/// <param name="IsConditional">
/// Whether the member is in a branch of an <c>#If</c> block (<strong>MS-VBAL §3.4</strong>), and so is compiled only when that branch is live: moving it out of the
/// block, or another member into it, changes what is compiled.
/// </param>
public sealed record class MemberExtent(MemberDeclarationNode Member, SourceRange Range, bool IsConditional);

/// <summary>
/// The edits that remove, insert and move whole members of a module, which keep their comments, annotations, line continuations and <c>Attribute</c> lines: each
/// takes a member's whole lines (<see cref="MemberExtent"/>), and keeps one blank line between two members.
/// </summary>
/// <remarks>
/// The edits are of the text as it is, none of them overlapping another, as <strong>LSP 3.17</strong> <c>TextEdit</c>s of one document are. What they write ends
/// its lines as the text does: with what ends the text's first line, and with a carriage return and a line feed when the text has a single line.
/// </remarks>
public static partial class MemberEdits
{
    /// <summary>
    /// The whole text of a member of a module.
    /// </summary>
    /// <param name="parse">The module, parsed: its syntax tree, its trivia (the comments) and its precompiler trivia (the <c>#If</c> blocks).</param>
    /// <param name="text">The text the module was parsed from.</param>
    /// <param name="member">A member of the module.</param>
    public static MemberExtent Extent(ModuleParseResult parse, string text, MemberDeclarationNode member)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(member);

        var lines = Lines.Of(text);
        var range = member.SourceLocation.Range;

        // a comment directly above is the member's when it is all its lines hold: a comment after code on its line is that code's.
        var comments = parse.Trivia.OfType<CommentTriviaNode>().Select(comment => comment.SourceLocation.Range).ToArray();
        var first = range.Start.Line;
        while (first > 0 && Array.FindIndex(comments, comment => comment.End.Line == first - 1 && lines.IsBlankBefore(comment.Start)) is var above and >= 0)
        {
            first = comments[above].Start.Line;
        }

        var blocks = ConditionalCompilationBlocks.Of(parse.PrecompilerTrivia);
        return new MemberExtent(member, new SourceRange(new SourcePosition(first, 0), lines.StartOfLineAfter(range.End.Line)), blocks.IsConditional(range.Start));
    }

    /// <summary>
    /// The edit that removes a member and the blank lines after it. When nothing but blank lines follows the member, the blank lines before it are removed
    /// with it as well.
    /// </summary>
    /// <param name="text">The text of the module.</param>
    /// <param name="extent">The member's whole text (<see cref="Extent"/>).</param>
    public static RefactorTextEdit Remove(string text, MemberExtent extent)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(extent);
        var lines = Lines.Of(text);
        var next = extent.Range.End.Character == 0 ? extent.Range.End.Line : lines.Count;
        while (next < lines.Count && lines.IsBlank(next))
        {
            next++;
        }

        if (next < lines.Count)
        {
            return new RefactorTextEdit { Range = new SourceRange(extent.Range.Start, new SourcePosition(next, 0)) };
        }

        // the last member: the blank lines before it go with it, and the line end before them stays the line's it ends.
        var first = extent.Range.Start.Line;
        while (first > 0 && lines.IsBlank(first - 1))
        {
            first--;
        }

        var start = first > 0 ? lines.EndOf(first - 1) : SourcePosition.Zero;
        var removed = new RefactorTextEdit { Range = new SourceRange(start, lines.End) };
        return first > 0 && lines.EndsWithNewLine ? removed with { NewText = lines.NewLine } : removed;
    }

    /// <summary>
    /// The edit that writes a member after another, a blank line between them.
    /// </summary>
    /// <param name="text">The text of the module.</param>
    /// <param name="after">The whole text of the member it goes after (<see cref="Extent"/>).</param>
    /// <param name="member">The whole text of the member written: the text of a <see cref="MemberExtent.Range"/>, or a new member's.</param>
    public static RefactorTextEdit InsertAfter(string text, MemberExtent after, string member)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(member);
        var lines = Lines.Of(text);
        var at = after.Range.End;

        // a member on the last line of a text that ends no line is ended before the one written after it, which ends none either.
        var endsNoLine = at.Character != 0;
        var newText = (endsNoLine ? lines.NewLine : string.Empty) + lines.NewLine + Unended(member) + (endsNoLine ? string.Empty : lines.NewLine);
        return new RefactorTextEdit { Range = new SourceRange(at, at), NewText = newText };
    }

    /// <summary>
    /// The edit that writes a member before another, a blank line between them: before the comments directly above that other one, which are its.
    /// </summary>
    /// <param name="text">The text of the module.</param>
    /// <param name="before">The whole text of the member it goes before (<see cref="Extent"/>).</param>
    /// <param name="member">The whole text of the member written: the text of a <see cref="MemberExtent.Range"/>, or a new member's.</param>
    public static RefactorTextEdit InsertBefore(string text, MemberExtent before, string member)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(member);
        var newLine = Lines.Of(text).NewLine;
        var at = before.Range.Start;
        return new RefactorTextEdit { Range = new SourceRange(at, at), NewText = Unended(member) + newLine + newLine };
    }

    /// <summary>
    /// The edits that move a member after another, or before it: its removal (<see cref="Remove"/>), and its text written at its new place
    /// (<see cref="InsertAfter"/>, <see cref="InsertBefore"/>), in the order they are in the text.
    /// </summary>
    /// <param name="text">The text of the module.</param>
    /// <param name="moved">The whole text of the member moved (<see cref="Extent"/>).</param>
    /// <param name="target">The whole text of the member it goes next to.</param>
    /// <param name="after">Whether it goes after <paramref name="target"/>; before it when not.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="target"/> overlaps <paramref name="moved"/>, or <paramref name="moved"/> is already on the side of <paramref name="target"/> that
    /// <paramref name="after"/> names, with nothing but blank lines between them.
    /// </exception>
    public static IReadOnlyList<RefactorTextEdit> Move(string text, MemberExtent moved, MemberExtent target, bool after)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(moved);
        ArgumentNullException.ThrowIfNull(target);
        if (moved.Range.Start < target.Range.End && target.Range.Start < moved.Range.End)
        {
            throw new ArgumentException("The member it goes next to overlaps the one moved.", nameof(target));
        }

        // nothing but blank lines between the two: the member is where it would go already.
        var lines = Lines.Of(text);
        var (from, to) = after ? (target.Range.End, moved.Range.Start) : (moved.Range.End, target.Range.Start);
        if (from <= to && Enumerable.Range(from.Line, to.Line - from.Line).All(lines.IsBlank))
        {
            throw new ArgumentException("The member is where it would go already.", nameof(target));
        }

        var removal = Remove(text, moved);
        var insertion = after ? InsertAfter(text, target, lines.Of(moved.Range)) : InsertBefore(text, target, lines.Of(moved.Range));
        return insertion.Range.Start < removal.Range.Start ? [insertion, removal] : [removal, insertion];
    }

    /// <summary>
    /// The edits that write members in another order: for each run of members written one after another, with nothing but blank lines between them, one
    /// edit that writes them in the order <paramref name="comparer"/> gives, with one blank line between two of them. A run whose order does not change has
    /// no edit.
    /// </summary>
    /// <param name="text">The text of the module.</param>
    /// <param name="members">
    /// The whole texts of the members (<see cref="Extent"/>), in the order they are written. Anything but blank lines between a member and the next, such as a
    /// comment on lines of its own or an <c>#If</c> directive, ends a run: no member is written past it.
    /// </param>
    /// <param name="comparer">The order the members of a run are written in. Members it orders alike keep the order they are written in.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="members"/> are not in the order they are written, or two of them overlap.
    /// </exception>
    public static IReadOnlyList<RefactorTextEdit> Reorder(string text, IReadOnlyList<MemberExtent> members, IComparer<MemberExtent> comparer)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(comparer);
        for (var index = 1; index < members.Count; index++)
        {
            if (members[index].Range.Start < members[index - 1].Range.End)
            {
                throw new ArgumentException("The members are not in the order they are written, or two of them overlap.", nameof(members));
            }
        }

        var lines = Lines.Of(text);
        var edits = new List<RefactorTextEdit>();
        var run = new List<MemberExtent>();
        foreach (var member in members)
        {
            if (run is [.., var last] && !IsBlankBetween(lines, last, member))
            {
                AddReordered(lines, run, comparer, edits);
                run.Clear();
            }

            run.Add(member);
        }

        AddReordered(lines, run, comparer, edits);
        return edits;
    }

    // whether nothing but blank lines is written between a member and the next: a member that ends the text ends no line, and nothing follows it.
    private static bool IsBlankBetween(Lines lines, MemberExtent member, MemberExtent next)
        => member.Range.End.Character == 0 && next.Range.Start.Character == 0
            && Enumerable.Range(member.Range.End.Line, next.Range.Start.Line - member.Range.End.Line).All(lines.IsBlank);

    // the edit that writes a run of members in their order, when it is not the order they are written in; the run ends as its last member did.
    private static void AddReordered(Lines lines, List<MemberExtent> run, IComparer<MemberExtent> comparer, List<RefactorTextEdit> edits)
    {
        var ordered = run.OrderBy(member => member, comparer).ToList();
        if (ordered.SequenceEqual(run))
        {
            return;
        }

        var newText = string.Join(lines.NewLine + lines.NewLine, ordered.Select(member => Unended(lines.Of(member.Range))))
            + (run[^1].Range.End.Character == 0 ? lines.NewLine : string.Empty);
        edits.Add(new RefactorTextEdit { Range = new SourceRange(run[0].Range.Start, run[^1].Range.End), NewText = newText });
    }

    // the text of a member without what ends its last line.
    private static string Unended(string member) => TrailingLineEnd().Replace(member, string.Empty);

    [GeneratedRegex(@"(\r\n|\r|\n)\z")]
    private static partial Regex TrailingLineEnd();

    // the lines of a text: where each starts, how long it is without what ends it, and what ends the first one that is ended.
    private sealed partial class Lines
    {
        private readonly string _text;
        private readonly List<int> _starts = [0];
        private readonly List<int> _lengths = [];

        private Lines(string text)
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

        public static Lines Of(string text) => new(text);

        public string NewLine { get; }

        // the lines, the last one included, which is empty when the text ends its last line.
        public int Count => _starts.Count;

        public bool EndsWithNewLine => _lengths[^1] == 0 && Count > 1;

        public SourcePosition End => new(Count - 1, _lengths[^1]);

        public bool IsBlank(int line) => _text.AsSpan(_starts[line], _lengths[line]).IsWhiteSpace();

        // whether nothing but blanks is written before a position on its line.
        public bool IsBlankBefore(SourcePosition position) => _text.AsSpan(_starts[position.Line], position.Character).IsWhiteSpace();

        // the position of what ends a line: past its last character.
        public SourcePosition EndOf(int line) => new(line, _lengths[line]);

        // the first character of the line after a line; the end of the text, on its last line.
        public SourcePosition StartOfLineAfter(int line) => line + 1 < Count ? new SourcePosition(line + 1, 0) : EndOf(line);

        public string Of(SourceRange range)
            => _text[(_starts[range.Start.Line] + range.Start.Character)..(_starts[range.End.Line] + range.End.Character)];

        [GeneratedRegex(@"\r\n|\r|\n")]
        private static partial Regex Ends();
    }
}
