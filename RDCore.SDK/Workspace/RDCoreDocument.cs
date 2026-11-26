using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Workspace;

/// <summary>
/// A source file and the modules it holds: its own module, which is what is outside every block, and the module of each of its blocks
/// (<see cref="ModuleParseResult.BlockModules"/>).
/// </summary>
/// <remarks>
/// A client edits files, and the platform compiles modules. A file of one module is its own module, addressed by the file's URI. A block's module is
/// addressed under it, by the file's URI with the module's name as one more segment (<see cref="BlockUri"/>): <c>file:///c:/ws/Backup.vbs/Logger</c>.
/// That is no file on disk, so what is decided by a file (its language, whether it is a script, the extension it has) is decided by the file the module is
/// in (<see cref="FileOf(string)"/>).
/// </remarks>
/// <param name="File">The file's URI.</param>
/// <param name="Parse">The file's parse: its own module, and its blocks' modules.</param>
public sealed record class RDCoreDocument(Uri File, ModuleParseResult Parse)
{
    /// <summary>
    /// The address of every module of the file: the file's own first, then each block's, in the order they are written.
    /// </summary>
    public IEnumerable<Uri> ModuleUris => [File, .. Parse.BlockModules.Select(block => block.Uri)];

    /// <summary>
    /// The module a position of the file is in: the block whose range holds it, its end included, and the file's own module otherwise.
    /// </summary>
    /// <param name="position">A position in the file.</param>
    /// <returns>The address of the module: a block's (<see cref="BlockUri"/>), or the file's.</returns>
    public Uri ModuleAt(SourcePosition position)
        => Parse.BlockModules.FirstOrDefault(block => block.Range.Contains(position) || block.Range.End == position)?.Uri ?? File;

    /// <summary>
    /// The tree of one of the file's modules.
    /// </summary>
    /// <param name="module">The address of the module: the file's, or a block's.</param>
    /// <returns>The module's tree; <see langword="null"/> when the file holds no module at that address, or its own module could not be read.</returns>
    public ModuleNode? SyntaxTreeOf(Uri module)
        => module == File ? Parse.SyntaxTree : Parse.BlockModules.FirstOrDefault(block => block.Uri == module)?.SyntaxTree;

    /// <summary>
    /// The parse of one of the file's modules: its tree, and what of the file's trivia, directives and syntax errors is that module's.
    /// </summary>
    /// <remarks>
    /// The file's parse holds the trivia (<see cref="ModuleParseResult.Trivia"/>), the directives (<see cref="ModuleParseResult.PrecompilerTrivia"/>) and the
    /// syntax errors (<see cref="ModuleParseResult.SyntaxErrors"/>) of all its modules. An item is the module's whose lines hold its start: a block's module
    /// has the lines between its header and its end, and the file's own module every other line. An item that runs on past the module's lines (a <c>#If</c>
    /// left open) is still the module's it starts in. The header and the end lines of a block (<see cref="BlockBoundaryTriviaNode"/>) are the file's, and no
    /// module's: a comment on either is the file's own module's. Positions are the file's, as in the file's parse.
    /// <para>
    /// A block's module has the file's <see cref="ModuleParseResult.LineEnding"/>, and no <see cref="ModuleParseResult.Designer"/>; the file's own module has
    /// the file's line ending and designer. Neither has <see cref="ModuleParseResult.BlockModules"/>. A file with no block has the file's parse as its own
    /// module's.
    /// </para>
    /// </remarks>
    /// <param name="module">The address of the module: the file's (<see cref="File"/>), or a block's (<see cref="BlockUri"/>).</param>
    /// <returns>
    /// The module's parse; <see langword="null"/> when the file holds no module at that address. When two blocks have one name, the first block's.
    /// </returns>
    public ModuleParseResult? ParseOf(Uri module)
    {
        if (Parse.BlockModules.IsEmpty)
        {
            return module == File ? Parse : null;
        }

        var block = Parse.BlockModules.FirstOrDefault(each => each.Uri == module);
        if (block is null && module != File)
        {
            return null;
        }

        bool Holds(SourcePosition start) => block is null
            ? !Parse.BlockModules.Any(each => IsInside(each, start))
            : IsInside(block, start);

        return new ModuleParseResult
        {
            SyntaxTree = block is null ? Parse.SyntaxTree : block.SyntaxTree,
            Trivia = [.. Parse.Trivia.Where(node => node is not BlockBoundaryTriviaNode && Holds(node.SourceLocation.Range.Start))],
            PrecompilerTrivia = [.. Parse.PrecompilerTrivia.Where(node => Holds(node.SourceLocation.Range.Start))],
            SyntaxErrors = [.. Parse.SyntaxErrors.Where(error => Holds(error.Location.Range.Start))],
            LineEnding = Parse.LineEnding,
            Designer = block is null ? Parse.Designer : null,
        };
    }

    // whether a position is on a line between a block's header and its end.
    private static bool IsInside(BlockModule block, SourcePosition position)
        => position.Line > block.Range.Start.Line && position.Line < block.Range.End.Line;

    /// <inheritdoc cref="FileBlocks.BlockUri(Uri, string)"/>
    public static Uri BlockUri(Uri file, string name) => FileBlocks.BlockUri(file, name);

    /// <inheritdoc cref="FileBlocks.FileOf(Uri)"/>
    public static Uri FileOf(Uri module) => FileBlocks.FileOf(module);

    /// <inheritdoc cref="FileBlocks.FileOf(string)"/>
    public static string FileOf(string path) => FileBlocks.FileOf(path);
}
