using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Model.AST;

/// <summary>
/// A module that a block of a source file declares (<see cref="Workspace.FileBlocks"/>): <c>Class Name ... End Class</c> declares a class module,
/// <c>Module Name ... End Module</c> a standard module.
/// </summary>
/// <remarks>
/// A file may hold several modules in any dialect: its own module, which is what is outside every block, and one module for each block
/// (<see cref="ModuleParseResult.BlockModules"/>). The block's module has an address of its own under the file's
/// (<see cref="Workspace.RDCoreDocument.BlockUri"/>), which its nodes' identities are keyed by (<see cref="Abstract.SyntaxNodeId.DocumentUri"/>). Its
/// nodes' locations are in the file: their <see cref="Source.SourceLocation.Uri"/> is the file's, and their positions are the file's, so that an editor
/// shows each where it is written.
/// </remarks>
/// <param name="Name">The module's name, as the block's header writes it; an <c>Attribute VB_Name</c> inside the block does not change it.</param>
/// <param name="Kind">The kind of module: <see cref="ModuleType.ClassModule"/> for a <c>Class</c> block, <see cref="ModuleType.StdModule"/> for a
/// <c>Module</c> block.</param>
/// <param name="Uri">The module's address: the file's, with the module's name as one more segment (<see cref="Workspace.RDCoreDocument.BlockUri"/>).</param>
/// <param name="Range">Where the block is in the file, from the start of its header to the end of its <c>End Class</c> or <c>End Module</c>.</param>
/// <param name="SyntaxTree">The module's tree: what is between the block's header and its end, read as a module is.</param>
public sealed record class BlockModule(string Name, ModuleType Kind, Uri Uri, SourceRange Range, ModuleNode SyntaxTree)
{
    /// <summary>
    /// Whether the block's header writes <c>Partial</c> before its kind (<see cref="Workspace.FileBlock.IsPartial"/>).
    /// </summary>
    public bool IsPartial { get; init; }

    /// <summary>
    /// Whether the block's header writes <c>Creatable</c> before <c>Class</c> (<see cref="Workspace.FileBlock.IsCreatable"/>); a <c>Module</c>
    /// block's never does.
    /// </summary>
    public bool IsCreatable { get; init; }

    /// <summary>
    /// The visibility the block's header states, <see cref="AccessModifier.Public"/> or <see cref="AccessModifier.Private"/>
    /// (<see cref="Workspace.FileBlock.Visibility"/>); <see langword="null"/> when the header states none.
    /// </summary>
    public AccessModifier? Visibility { get; init; }
}
