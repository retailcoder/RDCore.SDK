using System.Text.RegularExpressions;
using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Workspace;

/// <summary>
/// Reads what a VBA module's header says about the module itself.
/// </summary>
/// <remarks>
/// A class or designer module (a <c>.cls</c>, a <c>UserForm</c>) is exported with a <c>VERSION</c>
/// header line (<c>VERSION 1.0 CLASS</c> for a class, <c>VERSION 5.00</c> for a designer) followed by
/// a <c>BEGIN … END</c> attribute block. A standard module has no such header and opens directly on
/// its <c>Attribute VB_Name</c>. The <em>presence of the VERSION line</em> is the signal, independent
/// of the file extension: RD-VBA determines a module's kind from its source, not from <c>.cls</c> vs
/// <c>.bas</c>.
/// <para>
/// A document module's export (a <c>.doccls</c>) carries no header: what makes a module one is the
/// project's word for it, which <see cref="ProjectModuleKinds.Of(DocClassType?, string?)"/> reads before this.
/// </para>
/// </remarks>
public static partial class ModuleHeader
{
    /// <summary>
    /// Whether <paramref name="source"/> carries a module <c>VERSION</c> header: it is a class
    /// or designer module. <c>null</c> when there is no source to inspect.
    /// </summary>
    /// <remarks>
    /// The header is the file's own module's: a <c>VERSION</c> line inside a block of the file (<see cref="FileBlocks"/>) is the block's code.
    /// </remarks>
    /// <param name="source">The raw module source, or <c>null</c> when it could not be read.</param>
    public static bool? IsClassModule(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return null;
        }

        var blocks = FileBlocks.Find(source);
        for (var match = VersionHeader().Match(source); match.Success; match = match.NextMatch())
        {
            if (!FileBlocks.Holds(blocks, source, match.Index))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Where the first <c>Attribute VB_Name</c> line of <paramref name="source"/> writes the module's name: the range between its quotes.
    /// </summary>
    /// <remarks>
    /// Lines are the text's, split at each line feed; a carriage return before one is part of its line. The range is zero-based, on one line. The
    /// name is the file's own module's: an <c>Attribute VB_Name</c> line inside a block of the file (<see cref="FileBlocks"/>) is not it.
    /// </remarks>
    /// <param name="source">The module file's text, or <c>null</c> when it could not be read.</param>
    /// <returns>The name's range, which is empty for <c>Attribute VB_Name = ""</c>; or <c>null</c> when no line of the file's own module is an
    /// <c>Attribute VB_Name</c> line.</returns>
    public static SourceRange? NameRangeOf(string? source)
    {
        var lines = LinesOf(source);
        var blocks = FileBlocks.Find(source);
        for (int line = 0, start = 0; line < lines.Length; start += lines[line].Length + 1, line++)
        {
            if (VBNameLine().Match(lines[line]) is { Success: true } match && !FileBlocks.Holds(blocks, source!, start))
            {
                var name = match.Groups["name"];
                return new SourceRange(line, name.Index, line, name.Index + name.Length);
            }
        }

        return null;
    }

    /// <summary>
    /// The line of <paramref name="source"/> a module's first attribute goes on, which is where an edit inserts its <c>Attribute VB_Name</c>
    /// when its source writes none. A module's name is its <c>VB_Name</c> (<strong>MS-VBAL §4.2</strong>); a module whose source writes none is named
    /// by its file.
    /// </summary>
    /// <remarks>
    /// The line follows the header: after the <c>VERSION</c> line, the <c>Object</c> lines of the controls the module uses, and the header's
    /// <c>Begin</c> … <c>End</c> block, at the <c>End</c> of its own <c>Begin</c> (a designer's block holds its controls' blocks). A module
    /// without a <c>VERSION</c> first line has no header: its attributes go on its first line. A block whose <c>End</c> the text never writes
    /// ends with the text, and the attributes go on its last line. Lines are split as <see cref="NameRangeOf"/> splits them.
    /// </remarks>
    /// <param name="source">The module file's text, or <c>null</c> when it could not be read.</param>
    /// <returns>The zero-based line; <c>0</c> for no text.</returns>
    public static int AttributesLineOf(string? source)
    {
        var lines = LinesOf(source);
        if (lines.Length == 0 || !lines[0].TrimStart().StartsWith("VERSION ", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var line = 1;
        while (line < lines.Length - 1 && lines[line].TrimStart().StartsWith("Object ", StringComparison.OrdinalIgnoreCase))
        {
            line++;
        }

        if (line < lines.Length && IsBegin(lines[line]))
        {
            for (var depth = 0; line < lines.Length; line++)
            {
                depth += IsBegin(lines[line]) ? 1 : lines[line].Trim().Equals("End", StringComparison.OrdinalIgnoreCase) ? -1 : 0;
                if (depth == 0)
                {
                    break;
                }
            }

            line = Math.Min(line + 1, lines.Length - 1);
        }

        return Math.Min(line, lines.Length - 1);
    }

    private static string[] LinesOf(string? source) => string.IsNullOrEmpty(source) ? [] : source.Split('\n');

    // a line that opens a block of the header: BEGIN, or Begin and what it describes; BeginProperty opens a property group inside a block
    private static bool IsBegin(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("Begin", StringComparison.OrdinalIgnoreCase) && (trimmed.Length == 5 || char.IsWhiteSpace(trimmed[5]));
    }

    [GeneratedRegex(@"^\s*Attribute\s+VB_Name\s*=\s*""(?<name>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VBNameLine();

    // the `VERSION <n>[.<n>][ CLASS]` line VBA writes as the first physical line of a class or
    // designer module, on its own line. `\r?` so a CRLF ending does not defeat the end anchor; the
    // strict shape (digits and an optional CLASS keyword only) keeps a stray comment from matching.
    [GeneratedRegex(@"^[ \t]*VERSION[ \t]+[0-9]+(?:\.[0-9]+)?(?:[ \t]+CLASS)?[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionHeader();
}
