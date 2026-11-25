using RDCore.SDK.Model;
using System.Text.RegularExpressions;

namespace RDCore.SDK.Workspace;

/// <summary>
/// What a block of a source file declares: a class module (<c>Class Name ... End Class</c>) or a standard module (<c>Module Name ... End Module</c>).
/// </summary>
public enum FileBlockKind
{
    /// <summary>
    /// A standard module: <c>Module Name ... End Module</c>.
    /// </summary>
    Module,

    /// <summary>
    /// A class module: <c>Class Name ... End Class</c>.
    /// </summary>
    Class,
}

/// <summary>
/// A module that a block of a source file declares: its name, what kind of module it is, and the lines of the file it spans.
/// </summary>
/// <param name="Name">The module's name, as the block's header writes it.</param>
/// <param name="Kind">Whether the block declares a standard module or a class module.</param>
/// <param name="HeaderLine">The zero-based line of the block's header (<c>Class Name</c>, <c>Module Name</c>).</param>
/// <param name="EndLine">The zero-based line of the block's end (<c>End Class</c>, <c>End Module</c>).</param>
public sealed record class FileBlock(string Name, FileBlockKind Kind, int HeaderLine, int EndLine)
{
    /// <summary>
    /// Whether the block's header writes <c>Partial</c> before its kind (<c>Partial Class Name</c>, <c>Partial Module Name</c>).
    /// </summary>
    /// <remarks>
    /// This is what the header writes, and nothing more: what <c>Partial</c> makes of a module is the language's to say.
    /// </remarks>
    public bool IsPartial { get; init; }

    /// <summary>
    /// Whether the block's header writes <c>Creatable</c> before <c>Class</c> (<c>Creatable Class Name</c>); a <c>Module</c> block's never does.
    /// </summary>
    /// <remarks>
    /// This is what the header writes, and nothing more: what <c>Creatable</c> makes of a class is the language's to say.
    /// </remarks>
    public bool IsCreatable { get; init; }

    /// <summary>
    /// The visibility the block's header states before its kind (<c>Public Class Name</c>, <c>Private Module Name</c>): <see cref="AccessModifier.Public"/>
    /// or <see cref="AccessModifier.Private"/>; <see langword="null"/> when the header states none.
    /// </summary>
    /// <remarks>
    /// This is what the header writes, and nothing more: what a module with no stated visibility is, and what a visibility makes of a module, is the
    /// language's to say.
    /// </remarks>
    public AccessModifier? Visibility { get; init; }
}

/// <summary>
/// Finds the modules a source file declares in blocks, besides its own module: a file may hold several modules, in any dialect.
/// </summary>
/// <remarks>
/// <para>
/// A block starts on a line that holds <c>Class</c> or <c>Module</c> and the module's name, and nothing else but a comment and the header's modifiers,
/// and ends on the first line after it that holds the end of its kind (<c>End Class</c> for a <c>Class</c> block, <c>End Module</c> for a
/// <c>Module</c> block). Blocks do not nest: a header inside a block is the block's code. A header that no matching end follows starts no block. A
/// line that continues the line before it (<c> _</c>) is neither a header nor an end. What is outside every block is the file's own module. A
/// block's name is the name its header writes: an <c>Attribute VB_Name</c> inside it does not rename it, and is not the file's own module's either
/// (<see cref="ModuleName.FromSource"/>).
/// </para>
/// <para>
/// The modifiers come before the kind, each at most once and in this order: a visibility, <c>Public</c> or <c>Private</c>
/// (<see cref="FileBlock.Visibility"/>), then <c>Partial</c> (<see cref="FileBlock.IsPartial"/>), then, for a <c>Class</c> block only,
/// <c>Creatable</c> (<see cref="FileBlock.IsCreatable"/>), as in <c>Public Partial Creatable Class Name</c>. Keywords are read in any case. A line
/// that writes the modifiers in another order, a modifier twice, a modifier after the kind, <c>Creatable</c> before <c>Module</c>, or another
/// visibility (<c>Friend</c>, <c>Global</c>) is not a header.
/// </para>
/// </remarks>
public static partial class FileBlocks
{
    /// <summary>
    /// The blocks of <paramref name="source"/>, in the order they are written.
    /// </summary>
    /// <param name="source">The text of the source file; <see langword="null"/> or empty for none.</param>
    /// <returns>The file's blocks; empty when it declares none.</returns>
    public static IReadOnlyList<FileBlock> Find(string? source)
    {
        // a file that never writes Class or Module has no block, and is not split into lines for nothing.
        if (string.IsNullOrEmpty(source)
            || !(source.Contains("Class", StringComparison.OrdinalIgnoreCase) || source.Contains("Module", StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        var lines = Lines(source);
        var blocks = new List<FileBlock>();
        for (var line = 0; line < lines.Count; line++)
        {
            if (line > 0 && Continues(lines[line - 1]) || Header().Match(lines[line]) is not { Success: true } header)
            {
                continue;
            }

            var kind = string.Equals(header.Groups["kind"].Value, "Class", StringComparison.OrdinalIgnoreCase)
                ? FileBlockKind.Class
                : FileBlockKind.Module;
            var end = EndOf(lines, line + 1, kind);
            if (end is { } endLine)
            {
                blocks.Add(new FileBlock(header.Groups["name"].Value, kind, line, endLine)
                {
                    IsPartial = header.Groups["partial"].Success,
                    IsCreatable = header.Groups["creatable"].Success,
                    Visibility = VisibilityOf(header.Groups["visibility"]),
                });
                line = endLine;
            }
        }

        return blocks;
    }

    // the first line after the header that ends a block of its kind, and not one that continues the line before it.
    private static int? EndOf(IReadOnlyList<string> lines, int from, FileBlockKind kind)
    {
        for (var line = from; line < lines.Count; line++)
        {
            if (!Continues(lines[line - 1]) && End().Match(lines[line]) is { Success: true } end
                && string.Equals(end.Groups["kind"].Value, kind.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        return null;
    }

    // the visibility a header writes, Public or Private; none when it writes none.
    private static AccessModifier? VisibilityOf(Group visibility)
        => !visibility.Success ? null
            : string.Equals(visibility.Value, "Public", StringComparison.OrdinalIgnoreCase) ? AccessModifier.Public : AccessModifier.Private;

    // whether a position of the source is on a line of one of its blocks: what is there is not the file's own module's.
    internal static bool Holds(IReadOnlyList<FileBlock> blocks, string source, int index)
    {
        if (blocks.Count == 0)
        {
            return false;
        }

        var line = LineOf(source, index);
        return blocks.Any(block => block.HeaderLine <= line && line <= block.EndLine);
    }

    // the zero-based line of a position of the source, as Lines counts them: CR LF, LF and CR each end one.
    private static int LineOf(string source, int index)
    {
        var line = 0;
        for (var at = 0; at < index; at++)
        {
            if (source[at] == '\n' || source[at] == '\r' && (at + 1 >= source.Length || source[at + 1] != '\n'))
            {
                line++;
            }
        }

        return line;
    }

    private static bool Continues(string line) => line.TrimEnd().EndsWith(" _", StringComparison.Ordinal) || line.TrimEnd() == "_";

    // the source's lines, each without its line ending: CR LF, LF and CR each end one.
    private static List<string> Lines(string source)
    {
        var lines = new List<string>();
        var start = 0;
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] is '\r' or '\n')
            {
                lines.Add(source[start..index]);
                if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n')
                {
                    index++;
                }

                start = index + 1;
            }
        }

        lines.Add(source[start..]);
        return lines;
    }

    // the modifiers, each once and in this order: a visibility, Partial, and Creatable, which only a Class takes.
    [GeneratedRegex(@"^[ \t]*(?:(?<visibility>Public|Private)[ \t]+)?(?:(?<partial>Partial)[ \t]+)?"
        + @"(?:(?<creatable>Creatable)[ \t]+(?<kind>Class)|(?<kind>Class|Module))[ \t]+(?<name>[A-Za-z][A-Za-z0-9_]*)[ \t]*(?:'.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Header();

    [GeneratedRegex(@"^[ \t]*End[ \t]+(?<kind>Class|Module)[ \t]*(?:'.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex End();
}
