using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// The edits of a fix (<see cref="EditFix"/>) that remove or move a comment annotation (<strong>RD-VBAL §3.0.1.1</strong>), found in the parse of the
/// module it is in: the comment that holds it, its text, the range that removes it, and the edits that write it on a line of its own elsewhere.
/// </summary>
/// <remarks>
/// An annotation is written in a comment, after its <c>'</c>, and a comment can hold several (<c>'@Folder("Tools") @Ignore RDC00315</c>). Removing one
/// leaves the others; removing the last removes the comment's line when nothing else is written on it
/// (<see cref="LineEdits.WholeLinesOf(ModuleParseResult, SyntaxNode)"/>).
/// </remarks>
public static class AnnotationEdits
{
    /// <summary>
    /// The comment that holds the annotation, when both are written on one line.
    /// </summary>
    /// <param name="parse">The parse of the module the annotation is in.</param>
    /// <param name="annotation">The annotation, one of the parse's <see cref="ModuleParseResult.Trivia"/>.</param>
    /// <returns>
    /// The comment, or <see langword="null"/> when no comment of the parse holds it, or when the comment or the annotation runs on past its line (a line
    /// continuation).
    /// </returns>
    public static CommentTriviaNode? CommentOf(ModuleParseResult parse, AnnotationTriviaNode annotation)
        => Locate(parse, annotation)?.Comment;

    /// <summary>
    /// The annotation as it is written, from its <c>@</c> to the end of its arguments: <c>@Ignore RDC00315</c> of <c>' @Ignore RDC00315 ' unused</c>.
    /// </summary>
    /// <param name="parse">The parse of the module the annotation is in.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The text, or <see langword="null"/> when the annotation has no one-line comment (<see cref="CommentOf"/>).</returns>
    public static string? TextOf(ModuleParseResult parse, AnnotationTriviaNode annotation)
        => Locate(parse, annotation) is var (comment, offset)
            ? comment.Value.Substring(offset, annotation.SourceLocation.Range.End.Character - annotation.SourceLocation.Range.Start.Character)
            : null;

    /// <summary>
    /// The range a fix removes to remove the annotation: the comment's whole lines when the annotation is all the comment says and nothing else is written on
    /// its line, else the annotation's own range, which leaves the rest of the comment.
    /// </summary>
    /// <param name="parse">The parse of the module the annotation is in.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The range to remove.</returns>
    public static SourceRange RemovalOf(ModuleParseResult parse, AnnotationTriviaNode annotation)
    {
        var at = annotation.SourceLocation.Range;
        return Locate(parse, annotation) is var (comment, offset)
            && comment.Value.Remove(offset, at.End.Character - at.Start.Character).Trim() == "'"
            && LineEdits.WholeLinesOf(parse, comment) is { } lines
                ? lines
                : at;
    }

    /// <summary>
    /// The edits that move the annotation onto a comment line of its own before the line a range starts on, with its arguments as they are written, and
    /// remove it where it is (<see cref="RemovalOf"/>), such as a module annotation moved above the header of a block
    /// (<see cref="Model.AST.Annotations.GetHeaderAnnotations"/>).
    /// </summary>
    /// <remarks>
    /// The line is written without indentation, as <see cref="LineEdits"/> writes lines.
    /// </remarks>
    /// <param name="parse">The parse of the module the annotation and the range are in.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="range">The range the annotation is written above: a block's <see cref="BlockModule.Range"/>, or a declaration's.</param>
    /// <param name="name">The name the annotation is written under there, such as the short form of a module annotation; its own name when omitted.</param>
    /// <returns>The insertion, then the removal; <see langword="null"/> when the annotation has no one-line comment (<see cref="CommentOf"/>).</returns>
    public static TextEdit[]? MoveAbove(ModuleParseResult parse, AnnotationTriviaNode annotation, SourceRange range, string? name = null)
        => TextOf(parse, annotation) is { } text
            ? [LineEdits.InsertLinesBefore(parse, range, $"'@{name ?? annotation.Name}{text[(1 + annotation.Name.Length)..]}"),
                SyntaxEdits.Remove(RemovalOf(parse, annotation))]
            : null;

    /// <summary>
    /// The edit that renames the annotation where it is, its arguments left as they are written: <c>'@IgnoreModule RDC00315</c> renamed <c>Ignore</c> is
    /// <c>'@Ignore RDC00315</c>.
    /// </summary>
    /// <param name="parse">The parse of the module the annotation is in.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="name">The name it is written under, without the <c>@</c>.</param>
    /// <returns>
    /// The edit that replaces the name after the <c>@</c>; <see langword="null"/> when the annotation has no one-line comment (<see cref="CommentOf"/>),
    /// or when its text does not start with its name.
    /// </returns>
    public static TextEdit? Rename(ModuleParseResult parse, AnnotationTriviaNode annotation, string name)
    {
        if (TextOf(parse, annotation) is not { } text || !text.StartsWith($"@{annotation.Name}", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var start = annotation.SourceLocation.Range.Start;
        return SyntaxEdits.Replace(SyntaxEdits.Following(new SourcePosition(start.Line, start.Character + 1), annotation.Name.Length), name);
    }

    // the one-line comment that holds the annotation, and where the annotation starts in its text.
    private static (CommentTriviaNode Comment, int Offset)? Locate(ModuleParseResult parse, AnnotationTriviaNode annotation)
    {
        var at = annotation.SourceLocation.Range;
        var comment = parse.Trivia.OfType<CommentTriviaNode>().FirstOrDefault(candidate => candidate.SourceLocation.Uri == annotation.SourceLocation.Uri
            && candidate.SourceLocation.Range.Start <= at.Start && at.End <= candidate.SourceLocation.Range.End);
        if (comment?.SourceLocation.Range is not { } range || range.Start.Line != range.End.Line || at.Start.Line != at.End.Line)
        {
            return null;
        }

        var offset = at.Start.Character - range.Start.Character;
        return offset >= 0 && offset + (at.End.Character - at.Start.Character) <= comment.Value.Length ? (comment, offset) : null;
    }
}
