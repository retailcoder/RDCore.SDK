using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using System.Text;
using System.Text.RegularExpressions;

namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The edits that add declarations to the declaration section of a module, remove one variable of a declaration statement, and add a member to a
/// user-defined type. They keep the comments, the line continuations and the other variables of a statement.
/// </summary>
/// <remarks>
/// The edits are of the text as it is, as <strong>LSP 3.17</strong> <c>TextEdit</c>s of one document are. What they write ends its lines as the text does:
/// with what ends the text's first line, and with a carriage return and a line feed when the text has a single line.
/// </remarks>
public static partial class DeclarationEdits
{
    /// <summary>
    /// Where declarations are added to the declaration section of a module (<strong>MS-VBAL §5.2</strong>): at the start of the line after the last line
    /// that is not blank before the whole text of the first procedure (<see cref="MemberEdits.Extent"/>), so the comments directly above that procedure stay
    /// its own. In a module with no procedure, it is the end of the text.
    /// </summary>
    /// <param name="parse">The module, parsed: its syntax tree, its trivia (the comments) and its precompiler trivia (the <c>#If</c> blocks).</param>
    /// <param name="text">The text the module was parsed from.</param>
    /// <returns>
    /// The position, or <see langword="null"/> when it is in a branch of an <c>#If</c> block (<strong>MS-VBAL §3.4</strong>), where a declaration would be
    /// compiled only when that branch is live.
    /// </returns>
    /// <exception cref="ArgumentException">The module has no syntax tree.</exception>
    public static SourcePosition? SectionEnd(ModuleParseResult parse, string text)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(text);
        var module = parse.SyntaxTree ?? throw new ArgumentException("The module has no syntax tree.", nameof(parse));

        var lines = TextLines.Of(text);
        var procedure = module.Children.OfType<MemberDeclarationNode>().FirstOrDefault(IsProcedure);
        var last = (procedure is null ? lines.Count : MemberEdits.Extent(parse, text, procedure).Range.Start.Line) - 1;
        while (last >= 0 && lines.IsBlank(last))
        {
            last--;
        }

        var at = last < 0 ? SourcePosition.Zero : lines.StartOfLineAfter(last);
        return ConditionalCompilationBlocks.Of(parse.PrecompilerTrivia).IsConditional(at) ? null : at;
    }

    /// <summary>
    /// The edit that writes declarations at the end of the declaration section of a module (<see cref="SectionEnd"/>), on lines of their own, with a blank
    /// line between them and a procedure written directly after them.
    /// </summary>
    /// <param name="parse">The module, parsed.</param>
    /// <param name="text">The text the module was parsed from.</param>
    /// <param name="declarations">The text of the declarations, one or more lines.</param>
    /// <returns>The edit, or <see langword="null"/> when the end of the declaration section is in a branch of an <c>#If</c> block.</returns>
    /// <exception cref="ArgumentException">The module has no syntax tree.</exception>
    public static RefactorTextEdit? Insert(ModuleParseResult parse, string text, string declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        if (SectionEnd(parse, text) is not { } at)
        {
            return null;
        }

        var lines = TextLines.Of(text);

        // the last line of a text that ends no line is ended before them; a procedure that starts on the line they go on gets a blank line before it.
        var endsNoLine = at.Character != 0;
        var procedureFollows = !endsNoLine && at.Line < lines.Count && !lines.IsBlank(at.Line);
        var newText = (endsNoLine ? lines.NewLine : string.Empty) + TextLines.Unended(declarations) + (endsNoLine ? string.Empty : lines.NewLine)
            + (procedureFollows ? lines.NewLine : string.Empty);
        return new RefactorTextEdit { Range = new SourceRange(at, at), NewText = newText };
    }

    /// <summary>
    /// The edits that remove one variable of a declaration statement, at module level (<strong>MS-VBAL §5.2.3.1</strong>) or in a procedure, and keep the others
    /// it declares:
    /// <list type="bullet">
    /// <item>the only variable of its statement takes the statement with it, and its lines when nothing else is written on them; a comment after it stays;</item>
    /// <item>the first variable of several leaves the keywords in front of it to the next one (<c>Dim a As Long, b As String</c> becomes <c>Dim b As String</c>);</item>
    /// <item>any other takes the comma in front of it with it.</item>
    /// </list>
    /// The <c>Attribute</c> lines of the variable written directly after its statement (<c>Attribute Name.VB_VarDescription</c>) are removed with it.
    /// </summary>
    /// <param name="parse">The module, parsed.</param>
    /// <param name="text">The text the module was parsed from.</param>
    /// <param name="variable">The variable, a node of the module's syntax tree.</param>
    /// <returns>
    /// The edits, in the order they are in the text; or <see langword="null"/> when the variable is the only one of a statement that shares its line with
    /// another statement (<c>Dim a As Long: a = 1</c>), which is left to the caller to write as it wants.
    /// </returns>
    /// <exception cref="ArgumentException">The module has no syntax tree, or <paramref name="variable"/> is not in it.</exception>
    public static IReadOnlyList<RefactorTextEdit>? RemoveVariable(ModuleParseResult parse, string text, VariableDeclarationNode variable)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(variable);
        var module = parse.SyntaxTree ?? throw new ArgumentException("The module has no syntax tree.", nameof(parse));
        var parent = module.Descendants().Prepend(module).FirstOrDefault(node => node.Children.Any(child => child.Identity == variable.Identity))
            ?? throw new ArgumentException("The variable is not in the module.", nameof(variable));

        var lines = TextLines.Of(text);
        var siblings = parent.Children;
        var index = siblings.IndexOf(siblings.First(child => child.Identity == variable.Identity));
        var (first, last) = (index, index);
        while (first > 0 && siblings[first - 1] is VariableDeclarationNode before && IsListSeparator(lines, before, (VariableDeclarationNode)siblings[first]))
        {
            first--;
        }

        while (last + 1 < siblings.Length && siblings[last + 1] is VariableDeclarationNode after && IsListSeparator(lines, (VariableDeclarationNode)siblings[last], after))
        {
            last++;
        }

        var edits = new List<RefactorTextEdit>();
        var wholeLines = new List<(int First, int Last)>();
        var range = variable.SourceLocation.Range;
        if (first == last)
        {
            if (!lines.IsBlankBefore(range.Start) || After(lines, range.End) is not { } comment)
            {
                return null;
            }

            if (comment < 0)
            {
                wholeLines.Add((range.Start.Line, range.End.Line));
            }
            else
            {
                edits.Add(new RefactorTextEdit { Range = new SourceRange(range.Start, new SourcePosition(range.End.Line, comment)) });
            }
        }
        else if (index == first)
        {
            edits.Add(new RefactorTextEdit { Range = new SourceRange(DeclaratorStart(lines, variable), siblings[index + 1].SourceLocation.Range.Start) });
        }
        else
        {
            edits.Add(new RefactorTextEdit { Range = new SourceRange(siblings[index - 1].SourceLocation.Range.End, range.End) });
        }

        // the variable's attributes follow its statement, before anything else; lines removed one after another are removed as one, so that the last line of
        // the text takes what ends the line before it only once.
        for (var next = last + 1; next < siblings.Length && siblings[next] is AttributeDirectiveNode attribute; next++)
        {
            if (string.Equals(attribute.Binding, variable.Name, StringComparison.OrdinalIgnoreCase))
            {
                var (from, to) = (attribute.Location.Range.Start.Line, attribute.Location.Range.End.Line);
                if (wholeLines is [.., var previous] && previous.Last + 1 == from)
                {
                    wholeLines[^1] = (previous.First, to);
                }
                else
                {
                    wholeLines.Add((from, to));
                }
            }
        }

        edits.AddRange(wholeLines.Select(span => new RefactorTextEdit { Range = lines.WholeLines(span.First, span.Last) }));
        return edits;
    }

    /// <summary>
    /// The edit that adds a member to a user-defined type (<strong>MS-VBAL §5.2.3.3</strong>): on a line of its own before its <c>End Type</c>, indented as its
    /// last member is, or by four spaces more than its <c>End Type</c> when it has none.
    /// </summary>
    /// <param name="text">The text of the module.</param>
    /// <param name="type">The type, a node of the module's syntax tree.</param>
    /// <param name="member">The text of the member, as <c>Name As Long</c>.</param>
    /// <returns>The edit, an insertion at the start of the line of the <c>End Type</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not a user-defined type.</exception>
    public static RefactorTextEdit AddTypeMember(string text, MemberDeclarationNode type, string member)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(member);
        if (type.MemberKind != MemberKind.UserDefinedType)
        {
            throw new ArgumentException("The member is not a user-defined type.", nameof(type));
        }

        var lines = TextLines.Of(text);
        var endType = type.SourceLocation.Range.End.Line;
        var indent = type.Children.OfType<MemberDeclarationNode>().LastOrDefault(field => field.MemberKind == MemberKind.UserDefinedTypeField) is { } last
            ? lines.IndentOf(last.SourceLocation.Range.Start.Line)
            : lines.IndentOf(endType) + "    ";
        var at = new SourcePosition(endType, 0);
        return new RefactorTextEdit { Range = new SourceRange(at, at), NewText = indent + TextLines.Unended(member) + lines.NewLine };
    }

    /// <summary>
    /// The spaces and tabs a line of a text starts with: what a line written in its place, or next to it at the same level, is indented with.
    /// </summary>
    /// <param name="text">The text, its lines ended by a carriage return and a line feed, a line feed, or a carriage return.</param>
    /// <param name="line">The line, from 0.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="line"/> is not a line of the text.</exception>
    public static string IndentOf(string text, int line)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = TextLines.Of(text);
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(line, lines.Count);
        return lines.IndentOf(line);
    }

    /// <summary>
    /// The range of whole lines of a text, from the start of the first to the start of the line after the last, so that an edit that removes it leaves
    /// no blank line. When the last line is the last of a text that has no line end after it, the range starts where the line before the first ends
    /// instead, and so removes that line end; it is all of the text when the lines are all of it.
    /// </summary>
    /// <param name="text">The text, its lines ended by a carriage return and a line feed, a line feed, or a carriage return.</param>
    /// <param name="first">The first line, from 0.</param>
    /// <param name="last">The last line, from <paramref name="first"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The lines are not lines of the text, or <paramref name="last"/> is before <paramref name="first"/>.</exception>
    public static SourceRange WholeLines(string text, int first, int last)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = TextLines.Of(text);
        ArgumentOutOfRangeException.ThrowIfNegative(first);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(last, lines.Count);
        return lines.WholeLines(first, last);
    }

    /// <summary>
    /// Where a range of a text is after edits of that text: the edits written before it move it by the lines and characters they add or remove, and the
    /// edits written after it leave it where it is. A refactoring finds there what it left of the text, such as a name it did not edit.
    /// </summary>
    /// <param name="range">The range, in the text before the edits.</param>
    /// <param name="edits">
    /// The edits, in the text before them, in any order; they do not overlap (<strong>LSP 3.17</strong> §TextEdit). Text inserted where the range starts
    /// is written before it, and text inserted where it ends is written after it.
    /// </param>
    /// <returns>
    /// The range in the text after the edits, or <see langword="null"/> when an edit replaces any of it, or inserts text inside it: what is written there is
    /// the edit's.
    /// </returns>
    /// <exception cref="ArgumentException">Two of the edits overlap.</exception>
    public static SourceRange? RangeAfter(SourceRange range, IReadOnlyList<RefactorTextEdit> edits)
    {
        var ordered = Ordered(edits);
        if (ordered.Any(edit => (edit.Range.Start < range.End && range.Start < edit.Range.End)
            || (edit.Range.Start == edit.Range.End && range.Start < edit.Range.Start && edit.Range.Start < range.End)))
        {
            return null;
        }

        var (start, end) = (range.Start, range.End);
        foreach (var edit in ordered.Where(edit => edit.Range.End <= range.Start).Reverse())
        {
            var written = Advance(edit.Range.Start, edit.NewText);
            (start, end) = (RelativeTo(edit.Range.End, start).AnchoredAt(written), RelativeTo(edit.Range.End, end).AnchoredAt(written));
        }

        return new SourceRange(start, end);
    }

    /// <summary>
    /// The text that edits of a text leave: each edit's range replaced by what it writes.
    /// </summary>
    /// <param name="text">The text, its lines ended by a carriage return and a line feed, a line feed, or a carriage return.</param>
    /// <param name="edits">The edits, in the text before them, in any order; they do not overlap (<strong>LSP 3.17</strong> §TextEdit).</param>
    /// <exception cref="ArgumentException">Two of the edits overlap.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An edit is of a line the text does not have.</exception>
    public static string ApplyTo(string text, IReadOnlyList<RefactorTextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ordered = Ordered(edits);
        var lines = TextLines.Of(text);
        var applied = new StringBuilder(text.Length);
        var at = 0;
        foreach (var edit in ordered)
        {
            var (from, to) = (lines.Offset(edit.Range.Start), lines.Offset(edit.Range.End));
            applied.Append(text, at, from - at).Append(edit.NewText);
            at = to;
        }

        return applied.Append(text, at, text.Length - at).ToString();
    }

    // the edits in the order of the text; two that overlap are refused.
    internal static List<RefactorTextEdit> Ordered(IReadOnlyList<RefactorTextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var ordered = edits.OrderBy(edit => edit.Range.Start).ToList();
        for (var index = 1; index < ordered.Count; index++)
        {
            if (ordered[index].Range.Start < ordered[index - 1].Range.End)
            {
                throw new ArgumentException("Two of the edits overlap.", nameof(edits));
            }
        }

        return ordered;
    }

    // a position after another, relative to that one: its characters count from that one only on that one's line.
    private static SourcePosition RelativeTo(SourcePosition from, SourcePosition position)
        => new(position.Line - from.Line, position.Line == from.Line ? position.Character - from.Character : position.Character);

    private static bool IsProcedure(MemberDeclarationNode member)
        => member.MemberKind is MemberKind.Procedure or MemberKind.Function or MemberKind.PropertyGet or MemberKind.PropertyLet or MemberKind.PropertySet;

    // whether two variables are declared by one statement: nothing but a comma, blanks and line continuations is written between them.
    private static bool IsListSeparator(TextLines lines, VariableDeclarationNode variable, VariableDeclarationNode next)
        => variable.SourceLocation.Range.End <= next.SourceLocation.Range.Start
            && ListSeparator().IsMatch(lines.Of(new SourceRange(variable.SourceLocation.Range.End, next.SourceLocation.Range.Start)));

    // what is written after a statement on its line: -1 for nothing, the column of a comment, or null for anything else (another statement).
    private static int? After(TextLines lines, SourcePosition end)
    {
        var after = lines.After(end);
        var trimmed = after.TrimStart();
        return trimmed.Length == 0 ? -1
            : trimmed.StartsWith('\'') || CommentKeyword().IsMatch(trimmed) ? end.Character + after.Length - trimmed.Length
            : null;
    }

    // where a variable of a list starts, without the keywords in front of the first one: its WithEvents, or its name.
    private static SourcePosition DeclaratorStart(TextLines lines, VariableDeclarationNode variable)
    {
        var name = variable.NameRange ?? throw new ArgumentException("The variable's name is not known to be anywhere.", nameof(variable));
        if (!variable.IsWithEvents)
        {
            return name.Start;
        }

        var before = lines.Of(new SourceRange(variable.SourceLocation.Range.Start, name.Start));
        var withEvents = WithEventsBefore().Match(before);
        return withEvents.Success ? Advance(variable.SourceLocation.Range.Start, before[..withEvents.Index]) : name.Start;
    }

    // a position moved past some text.
    private static SourcePosition Advance(SourcePosition position, string text)
    {
        var ends = LineEnds().Matches(text);
        return ends.Count == 0
            ? position with { Character = position.Character + text.Length }
            : new SourcePosition(position.Line + ends.Count, text.Length - (ends[^1].Index + ends[^1].Length));
    }

    [GeneratedRegex(@"\A[ \t]*(?:_(?:\r\n|\r|\n)[ \t]*)*,[ \t]*(?:_(?:\r\n|\r|\n)[ \t]*)*\z")]
    private static partial Regex ListSeparator();

    [GeneratedRegex(@"\ARem(?:\s|\z)", RegexOptions.IgnoreCase)]
    private static partial Regex CommentKeyword();

    [GeneratedRegex(@"WithEvents(?:[ \t]|_(?:\r\n|\r|\n))+\z", RegexOptions.IgnoreCase)]
    private static partial Regex WithEventsBefore();

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineEnds();
}
