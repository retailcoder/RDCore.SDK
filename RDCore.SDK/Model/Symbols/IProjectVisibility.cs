using RDCore.SDK.Model.Symbols.Abstract;

namespace RDCore.SDK.Model.Symbols;

/// <summary>
/// Decides which members of a module the code of other modules sees (<strong>MS-VBAL §5.2.3</strong>, <strong>§5.6.10</strong>): the other modules of its
/// own project, and the projects that reference it.
/// </summary>
/// <remarks>
/// The SDK places symbols in scopes (<see cref="ScopeTreeBuilder"/>) and resolves module-qualified types (<see cref="VBProjectSymbol.ResolveQualifiedType"/>)
/// by asking this hook; the language implementation decides the rules. A project is identified by its root, the <see cref="Symbol.WorkspaceRoot"/> of its
/// modules, which is also the <see cref="LexicalScope.Uri"/> of its <see cref="LexicalScopeKind.Project"/> scope.
/// </remarks>
public interface IProjectVisibility
{
    /// <summary>
    /// Whether a member of a module is visible to the other modules of the module's own project. A visible member is placed in the scope of the project,
    /// where a name resolver binds it from a sibling module.
    /// </summary>
    /// <param name="member">The member: a symbol whose parent is <paramref name="module"/>.</param>
    /// <param name="module">The module that declares <paramref name="member"/>.</param>
    /// <returns><see langword="true"/> when the other modules of the project see <paramref name="member"/>.</returns>
    bool IsProjectVisible(Symbol member, VBModuleSymbol module);

    /// <summary>
    /// Whether a member of a module is visible from the code of a project: the module's own project, or one that references it.
    /// </summary>
    /// <param name="member">The member: a symbol whose parent is <paramref name="module"/>.</param>
    /// <param name="module">The module that declares <paramref name="member"/>.</param>
    /// <param name="originProject">
    /// The root of the project whose code names <paramref name="member"/> (the <see cref="Symbol.WorkspaceRoot"/> of its modules). A
    /// <see langword="null"/> root stands for code of no known project, which sees none of the module's members.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when code of <paramref name="originProject"/> outside <paramref name="module"/> sees <paramref name="member"/>.
    /// </returns>
    bool IsVisibleFrom(Symbol member, VBModuleSymbol module, Uri? originProject);
}
