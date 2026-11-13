using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.Source;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// The edits of a fix (<see cref="EditFix"/>), placed by the positions of the syntax tree an analyzer is given: what to write where, without the text around it.
/// </summary>
/// <remarks>
/// An analyzer is given the syntax tree of a module and not its text (<see cref="ModuleAnalysisContext"/>), so an edit is placed only where the tree says what
/// is there: at the start or the end of a node, between two nodes, or over a keyword whose length the tree implies, such as the <c>Rem</c> a comment node
/// starts with. Where the tree does not say what is between two positions, the width of the span is what tells: a span of one character between the name of
/// a callee and its first argument can only be the <c>(</c>. Where nothing tells, the fix is not offered.
/// </remarks>
public static class SyntaxEdits
{
    /// <summary>
    /// Text written at a position, before what is there.
    /// </summary>
    /// <param name="at">The position the text is written at.</param>
    /// <param name="text">The text.</param>
    public static TextEdit Insert(SourcePosition at, string text) => new() { Range = RangeOf(new SourceRange(at, at)), NewText = text };

    /// <summary>
    /// Text written in place of what a range holds.
    /// </summary>
    /// <param name="range">The range whose text is replaced, which may span lines.</param>
    /// <param name="text">The text written in its place.</param>
    public static TextEdit Replace(SourceRange range, string text) => new() { Range = RangeOf(range), NewText = text };

    /// <summary>
    /// What a range holds, removed.
    /// </summary>
    /// <param name="range">The range whose text is removed, which may span lines.</param>
    public static TextEdit Remove(SourceRange range) => Replace(range, string.Empty);

    /// <summary>
    /// The range of <paramref name="length"/> characters from a position, on its line: a keyword that starts a node, such as <c>Dim</c> or <c>ByRef</c>.
    /// </summary>
    /// <param name="at">The position the range starts at.</param>
    /// <param name="length">How many characters the range is.</param>
    public static SourceRange Following(SourcePosition at, int length) => new(at, new SourcePosition(at.Line, at.Character + length));

    /// <summary>
    /// The range of <paramref name="length"/> characters up to a position, on its line: a type-declaration character that ends a name, or a keyword that ends
    /// a block, such as <c>Wend</c>.
    /// </summary>
    /// <param name="at">The position the range ends at.</param>
    /// <param name="length">How many characters the range is.</param>
    public static SourceRange Preceding(SourcePosition at, int length) => new(new SourcePosition(at.Line, at.Character - length), at);

    /// <summary>
    /// The range from the start of a line to the start of the line after another, their line ends included: removed, the lines after them move up.
    /// </summary>
    /// <remarks>
    /// The range ends at the start of the line after <paramref name="last"/>, so it takes the line end of <paramref name="last"/> only where there is one. The
    /// last line of a text that does not end with a line end has none: where the lines may be the last of the text, the text is what places the range
    /// (<c>DeclarationEdits.WholeLines</c>).
    /// </remarks>
    /// <param name="first">The first line.</param>
    /// <param name="last">The last line, the same as <paramref name="first"/> or after it.</param>
    public static SourceRange LinesThrough(int first, int last) => new(new SourcePosition(first, 0), new SourcePosition(last + 1, 0));

    /// <summary>
    /// The start of the line after the last line of a node: where lines written after the node go, ended with their own line ends.
    /// </summary>
    /// <param name="node">The range of the node.</param>
    public static SourcePosition LineAfter(SourceRange node) => new(node.End.Line + 1, 0);

    /// <summary>
    /// How many characters there are from one position to another, when both are on the same line; <c>-1</c> when they are not, where the tree does not say
    /// how many there are.
    /// </summary>
    /// <param name="from">The first position.</param>
    /// <param name="to">The second position, on or after the first.</param>
    public static int Width(SourcePosition from, SourcePosition to) => from.Line == to.Line ? to.Character - from.Character : -1;

    /// <summary>
    /// A fix of one or more edits of the document a finding is in, under the title a code action offers it by.
    /// </summary>
    /// <remarks>
    /// The fix is made of the edits as they are given: they are applied together, and none of them may overlap another.
    /// </remarks>
    /// <param name="title">The title of the code action, which says what the fix writes.</param>
    /// <param name="edits">The edits, in any order.</param>
    public static EditFix Fix(string title, params TextEdit[] edits) => new() { Title = title, Edits = edits };

    /// <summary>
    /// The protocol range of a range of the source, which an edit or a diagnostic of the language server is placed at.
    /// </summary>
    /// <param name="range">The range of the source.</param>
    public static Range RangeOf(SourceRange range) => new(range.Start.Line, range.Start.Character, range.End.Line, range.End.Character);
}
