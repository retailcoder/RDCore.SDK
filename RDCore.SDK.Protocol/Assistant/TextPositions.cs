using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Assistant;

/// <summary>
/// A position in the text of a document as the tools of an assistant say it: a 1-based line and a 1-based column, the first character of a document
/// being at <c>1:1</c>.
/// </summary>
/// <remarks>
/// A column counts UTF-16 code units, as LSP does. The tools read and write positions 1-based, as a listing numbers its lines, where every LSP message is
/// 0-based (<see cref="SourcePosition"/>).
/// </remarks>
/// <param name="Line">The line, from 1.</param>
/// <param name="Column">The column, from 1.</param>
public readonly record struct TextPosition(int Line, int Column)
{
    /// <summary>
    /// The position a 0-based position is.
    /// </summary>
    /// <param name="position">The 0-based position.</param>
    /// <returns>The same position, 1-based.</returns>
    public static TextPosition Of(SourcePosition position) => new(position.Line + 1, position.Character + 1);

    /// <summary>
    /// The 0-based position this position is.
    /// </summary>
    /// <returns>The same position, 0-based.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The line or the column is less than 1.</exception>
    public SourcePosition ToSourcePosition()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Line, 1, nameof(Line));
        ArgumentOutOfRangeException.ThrowIfLessThan(Column, 1, nameof(Column));
        return new(Line - 1, Column - 1);
    }

    /// <summary>
    /// The position as a listing writes it: <c>12:5</c>.
    /// </summary>
    /// <returns>The line and the column, with a colon between them.</returns>
    public override string ToString() => $"{Line}:{Column}";
}

/// <summary>
/// A range of the text of a document, 1-based (<see cref="TextPosition"/>): where it starts, and the position just after its last character.
/// </summary>
/// <param name="Start">Where the range starts.</param>
/// <param name="End">The position just after its last character; <see cref="Start"/> for an empty range.</param>
public readonly record struct TextRange(TextPosition Start, TextPosition End)
{
    /// <summary>
    /// The range a 0-based range is.
    /// </summary>
    /// <param name="range">The 0-based range.</param>
    /// <returns>The same range, 1-based.</returns>
    public static TextRange Of(SourceRange range) => new(TextPosition.Of(range.Start), TextPosition.Of(range.End));

    /// <summary>
    /// The 0-based range this range is.
    /// </summary>
    /// <returns>The same range, 0-based.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A line or a column is less than 1.</exception>
    public SourceRange ToSourceRange() => new(Start.ToSourcePosition(), End.ToSourcePosition());
}

/// <summary>
/// A range of a document of the workspace, as the tools of an assistant say it.
/// </summary>
/// <param name="Path">
/// The document's path relative to the workspace root, with <c>/</c> between folders (<c>src/Module1.bas</c>); absolute for a document outside the root.
/// </param>
/// <param name="Range">The range, 1-based.</param>
public sealed record class TextLocation(string Path, TextRange Range)
{
    /// <summary>
    /// The location of a range of a document, its path said from the workspace root (<see cref="PathOf"/>).
    /// </summary>
    /// <param name="workspaceRoot">The workspace root: a folder.</param>
    /// <param name="document">The document: a <c>file:</c> URI.</param>
    /// <param name="range">The range, 0-based.</param>
    /// <returns>The location.</returns>
    public static TextLocation Of(string workspaceRoot, Uri document, SourceRange range) => new(PathOf(workspaceRoot, document), TextRange.Of(range));

    /// <summary>
    /// The path of a document as a tool says it: relative to the workspace root, with <c>/</c> between folders; the absolute path of a document outside the
    /// root.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root: a folder.</param>
    /// <param name="document">The document: a <c>file:</c> URI.</param>
    /// <returns>The path.</returns>
    public static string PathOf(string workspaceRoot, Uri document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var local = document.IsFile ? document.LocalPath : document.ToString();
        if (string.IsNullOrEmpty(workspaceRoot) || !document.IsFile)
        {
            return local;
        }

        var relative = System.IO.Path.GetRelativePath(workspaceRoot, local);
        return relative == ".." || relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) || System.IO.Path.IsPathRooted(relative)
            ? local
            : relative.Replace(System.IO.Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// The document a tool's argument names: a path relative to the workspace root (with <c>/</c> or the platform's separator between folders), or absolute.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root: a folder.</param>
    /// <param name="path">The path the argument gives.</param>
    /// <returns>The document's <c>file:</c> URI.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty, or is not a path.</exception>
    public static Uri UriOf(string workspaceRoot, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var native = path.Replace('/', System.IO.Path.DirectorySeparatorChar);
        var full = System.IO.Path.IsPathRooted(native) ? System.IO.Path.GetFullPath(native) : System.IO.Path.GetFullPath(System.IO.Path.Combine(workspaceRoot, native));
        return new Uri(full);
    }

    /// <summary>
    /// The location as a listing writes it: <c>src/Module1.bas:12:5</c>.
    /// </summary>
    /// <returns>The path and the start of the range.</returns>
    public override string ToString() => $"{Path}:{Range.Start}";
}
