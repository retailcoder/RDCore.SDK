using RDCore.SDK.Model;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using System.Collections.Immutable;

namespace RDCore.SDK.Runtime.Libraries;

/// <summary>
/// The names a project of the workspace gives the projects that reference it (<see cref="RDCore.SDK.Workspace.RDCoreReference.IsProject"/>): its public
/// entities, which a referencing project binds as it binds a library's (<strong>MS-VBAL §4.1</strong>).
/// </summary>
/// <remarks>
/// <para>
/// A procedural module is accessible to the projects that reference its project unless it is a private module (<c>Option Private Module</c>,
/// <strong>MS-VBAL §5.2.1.4</strong>), and a class module when it is exposed (<c>VB_Exposed</c>, its instancing <c>PublicNotCreatable</c> or
/// <c>PublicCreatable</c>, <strong>MS-VBAL §5.2.4.1.1</strong>).
/// </para>
/// <para>
/// A member is accessible when it is declared <c>Public</c> or <c>Global</c>, or without an access modifier where that makes it public (a procedure,
/// an <c>Enum</c>, a user-defined type), and its module allows it (<strong>MS-VBAL §5.2.3</strong>; for a procedure, <strong>MS-VBAL §5.3.1</strong>):
/// </para>
/// <list type="bullet">
/// <item>a variable, a procedure or an <c>Enum</c> and its members: in a class module, or a procedural module that is not a private module;</item>
/// <item>a constant or a user-defined type: in a procedural module that is not a private module.</item>
/// </list>
/// <para>
/// A <c>Friend</c> member is accessible within its project only, and a class's default instance (<c>VB_PredeclaredId</c>) is a name of its own project only.
/// </para>
/// </remarks>
public static class ReferencedProjectSymbols
{
    /// <summary>
    /// Whether <paramref name="module"/> is accessible to the projects that reference its project: a procedural module that is not a private module, or
    /// an exposed class module.
    /// </summary>
    /// <param name="module">The module.</param>
    /// <returns><see langword="true"/> when a referencing project can name the module.</returns>
    public static bool IsAccessible(VBModuleSymbol module)
    {
        ArgumentNullException.ThrowIfNull(module);

        return module is VBClassModuleSymbol ? module.Directives.Exposed : !module.Directives.PrivateModule;
    }

    /// <summary>
    /// Whether <paramref name="member"/>, declared in <paramref name="module"/>, is accessible to the projects that reference its project.
    /// </summary>
    /// <param name="member">The member of the module.</param>
    /// <param name="module">The module that declares <paramref name="member"/>.</param>
    /// <returns><see langword="true"/> when a referencing project can name the member, or reach it through an object of the class.</returns>
    public static bool IsAccessible(Symbol member, VBModuleSymbol module)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(module);

        var isClass = module is VBClassModuleSymbol;
        if (member is not AccessibleTypedSymbol accessible || !isClass && module.Directives.PrivateModule)
        {
            return false;
        }

        var isPublic = accessible.AccessModifier switch
        {
            AccessModifier.Public or AccessModifier.Global => true,
            AccessModifier.Implicit => member is not (VBModuleFieldVariableMemberSymbol or VBConstantMemberSymbol),
            _ => false,
        };

        return isPublic && (!isClass || member is not (VBConstantMemberSymbol or VBUserDefinedTypeMemberSymbol));
    }

    /// <summary>
    /// The names of a referenced project, as the referencing project binds them: the project itself, its accessible modules, their accessible members, and
    /// the members of its accessible <c>Enum</c> types, each a name of that project at its precedence.
    /// </summary>
    /// <remarks>
    /// Each symbol is the referenced project's own, addressed under its workspace root, and carries <see cref="SymbolProperties.Library"/> (the project's
    /// name) and <see cref="SymbolProperties.LibraryPriority"/>, which the referencing project's name resolver ranks it by among the libraries. The project
    /// symbol is addressed under the referencing project's root, as a library's is. Any other symbol of <paramref name="symbols"/> (the standard library's,
    /// the libraries the referenced project references itself, its own project symbol, a class's default instance) is left out.
    /// </remarks>
    /// <param name="workspaceRoot">The workspace root of the referencing project.</param>
    /// <param name="projectName">The name of the referenced project, as the reference names it.</param>
    /// <param name="symbols">The symbols the referenced project's modules declare.</param>
    /// <param name="priority">
    /// The reference precedence of the project among the referencing project's references (<see cref="ReferencedLibraries.Priorities"/>).
    /// </param>
    /// <returns>The names, the project's symbol first.</returns>
    public static ImmutableArray<Symbol> Of(Uri workspaceRoot, string projectName, IEnumerable<Symbol> symbols, int priority)
    {
        ArgumentNullException.ThrowIfNull(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentNullException.ThrowIfNull(symbols);

        var all = symbols.ToArray();
        var modules = all.OfType<VBModuleSymbol>()
            .Where(module => module.GetProperty(SymbolProperties.Library) is null)
            .GroupBy(module => module.Uri.AbsoluteUri, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var names = ImmutableArray.CreateBuilder<Symbol>();
        names.Add(Tagged(new VBProjectSymbol(workspaceRoot, projectName)));
        names.AddRange(modules.Values.Where(IsAccessible).Select(Tagged));

        // an enum's members parent to the enum, which parents to its module: they are accessible where it is.
        var enums = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in all.Where(symbol => symbol is not VBModuleSymbol and not VBEnumConstMemberSymbol))
        {
            if (modules.TryGetValue(member.ParentUri.AbsoluteUri, out var module) && IsAccessible(member, module))
            {
                if (member is VBEnumMemberSymbol)
                {
                    enums.Add(member.Uri.AbsoluteUri);
                }

                names.Add(Tagged(member));
            }
        }

        names.AddRange(all.OfType<VBEnumConstMemberSymbol>().Where(constant => enums.Contains(constant.ParentUri.AbsoluteUri)).Select(Tagged));
        return names.ToImmutable();

        Symbol Tagged(Symbol symbol) => symbol.With(SymbolProperties.Library, projectName).With(SymbolProperties.LibraryPriority, priority);
    }
}
