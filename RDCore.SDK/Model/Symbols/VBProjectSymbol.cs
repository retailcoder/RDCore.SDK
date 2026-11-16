using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Runtime.Abstract.Execution;
using RDCore.SDK.Runtime.Shared;

namespace RDCore.SDK.Model.Symbols;

/// <summary>
/// A VBA project, as a resolvable, named symbol: the enclosing project, or a library it references. It is a namespace of the
/// <em>type binding context</em> (<strong>MS-VBAL §5.6.4</strong>): a qualified type reference like <c>Project.ClassName</c> resolves
/// <c>Project</c> to a project symbol before looking up <c>ClassName</c> among that project's modules.
/// </summary>
/// <remarks>
/// The enclosing project's symbol has no <see cref="SymbolProperties.Library"/>. A referenced library is a project symbol of its own, named for
/// the library, with <see cref="SymbolProperties.Library"/> set to that name, as are the symbols it declares (<c>LibrarySymbolReader</c>, and
/// the standard library's symbol provider). The property is what tells a library's types from those of the enclosing project, and from
/// those of another library (<see cref="ISymbolResolver.ResolveProjectType"/>).
/// </remarks>
/// <param name="WorkspaceRoot">A <c>Uri</c> representing the absolute path to the workspace.</param>
/// <param name="Name">The project's own name (its <c>.rdproj</c>/VBA project name).</param>
public sealed record class VBProjectSymbol(Uri WorkspaceRoot, string Name)
    : Symbol(WorkspaceRoot, StaticSymbol.GlobalUri, Name, ScopeKind.Global, SymbolKindExt.Project)
{
    /// <summary>
    /// Resolves the type <paramref name="name"/> in the type binding context (<strong>MS-VBAL §5.6.4</strong>),
    /// honoring an optional <paramref name="qualifier"/> (a project name, e.g. the <c>Project</c> in
    /// <c>Project.ClassName</c>).
    /// </summary>
    /// <remarks>
    /// The lookup is positional. A bare name, and the last part of a qualified one, is bound by
    /// <see cref="ISymbolResolver.ResolveType"/>; the qualifier is a namespace, bound by
    /// <see cref="ISymbolResolver.ResolveQualifier"/>, where neither a user-defined type nor an Enum type is a
    /// candidate, since neither can contain a type. A <c>Type</c> of the enclosing module that shares the project's
    /// name therefore does not stand in for the project in <c>Project.ClassName</c>, though it is still what the bare
    /// name <c>Project</c> means. When the qualifier is a project, the enclosing one or a referenced library,
    /// <paramref name="name"/> is looked up among that project's types (<see cref="ISymbolResolver.ResolveProjectType"/>), not from
    /// <paramref name="handle"/>, so nothing declared in the enclosing module can hide the project's module or type it names. When the qualifier
    /// is a module, <paramref name="name"/> is a <c>Type</c> or an <c>Enum</c> of that module (<strong>MS-VBAL §5.6.12</strong>). A qualifier that
    /// resolves to neither a project nor a module stays unbound.
    /// </remarks>
    /// <param name="resolver">The resolver names are bound by.</param>
    /// <param name="qualifier">The qualifier, a project's or a module's name; <see langword="null"/> for a bare name.</param>
    /// <param name="name">The name of the type.</param>
    /// <param name="handle">The address of the symbol whose scope a bare name, and the qualifier, are bound from.</param>
    /// <returns>The type, or <see cref="SymbolResolutionResult.Unbound"/>.</returns>
    public static SymbolResolutionResult ResolveQualifiedType(ISymbolResolver resolver, string? qualifier, string name, Uri handle)
    {
        if (qualifier is null)
        {
            return resolver.ResolveType(name, ScopeKind.Global, handle);
        }

        return resolver.ResolveQualifier(qualifier, ScopeKind.Global, handle).Symbol switch
        {
            VBProjectSymbol project => resolver.ResolveProjectType(project, name),
            VBModuleSymbol module => ResolveTypeOfModule(resolver, module, name, handle),
            _ => SymbolResolutionResult.Unbound,
        };
    }

    /// <summary>
    /// Gets the class module a type was built from, as the composition has it now.
    /// </summary>
    /// <remarks>
    /// A declared type carries the class as it was when the type was built, and the class is what its members are read from. It is found by the name it was
    /// declared with <em>and</em> the library that declared it: the order of the references decides what a bare name means, and this is not a bare name.
    /// A class of the workspace has no library, and is found by its name as it always was.
    /// </remarks>
    /// <param name="resolver">The resolver names are bound by.</param>
    /// <param name="declared">The class module a type holds.</param>
    /// <returns>The class as it is now, or <see langword="null"/> when the composition no longer has it.</returns>
    public static VBClassModuleSymbol? ResolveClass(ISymbolResolver resolver, VBClassModuleSymbol declared)
        => ResolveQualifiedType(resolver, declared.GetProperty(SymbolProperties.Library), declared.Name, StaticSymbol.GlobalUri).Symbol as VBClassModuleSymbol;

    /// <summary>
    /// Gets the class of a referenced library whose object a document module's document is (<see cref="VBClassModuleSymbol.HostClass"/>), by the name of
    /// its library and its own.
    /// </summary>
    /// <param name="resolver">The resolver names are bound by.</param>
    /// <param name="module">The class module.</param>
    /// <returns>The host's class, or <see langword="null"/> for a module that is no document module, and when the project does not reference its library.</returns>
    public static VBClassModuleSymbol? ResolveHostClass(ISymbolResolver resolver, VBClassModuleSymbol module)
        => module.HostClass is { } host
            ? ResolveQualifiedType(resolver, host.Library, host.ClassName, StaticSymbol.GlobalUri).Symbol as VBClassModuleSymbol
            : null;

    /// <summary>
    /// The members of the interface of a class, as its declaration is now: its default interface, and, for a document module, the members of its host's class
    /// that it does not declare itself (<see cref="ResolveHostClass"/>) - an event is no member a name reaches.
    /// </summary>
    /// <param name="resolver">The resolver names are bound by.</param>
    /// <param name="declared">The class module a type holds.</param>
    public static IReadOnlyList<VBTypeMemberSymbol> InterfaceMembersOf(ISymbolResolver resolver, VBClassModuleSymbol declared)
    {
        var current = ResolveClass(resolver, declared) ?? declared;
        if (ResolveHostClass(resolver, current) is not { } host)
        {
            return current.DefaultInterfaceMembers;
        }

        var own = current.DefaultInterfaceMembers.Select(member => member.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. current.DefaultInterfaceMembers, .. host.DefaultInterfaceMembers.Where(member => member is not VBEventMemberSymbol && !own.Contains(member.Name))];
    }

    // MS-VBAL §5.6.12: "<l-expression> is classified as a procedural module or a type referencing a class defined in a class module", and the module has an
    // accessible UDT or Enum definition of the name. The type is the module's own: a type that the module's scope reaches by being the project's is not the
    // module's, and one that is Private to the module is not accessible to anything outside it.
    private static SymbolResolutionResult ResolveTypeOfModule(ISymbolResolver resolver, VBModuleSymbol module, string name, Uri handle)
    {
        var found = resolver.ResolveType(name, ScopeKind.Global, module.Uri);
        if (found.Symbol is not { } type || !string.Equals(type.ParentUri.AbsoluteUri, module.Uri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
        {
            return SymbolResolutionResult.Unbound;
        }

        return IsWithin(handle, module) || ScopeTreeBuilder.IsProjectVisible(type) ? found : SymbolResolutionResult.Unbound;
    }

    // the symbol at the handle is the module, or declared in it: a symbol's address is its parent's with the fragment extended by its own name.
    private static bool IsWithin(Uri handle, VBModuleSymbol module)
    {
        var inside = SymbolUri.PathOf(handle);
        var path = SymbolUri.PathOf(module.Uri);
        return handle.GetLeftPart(UriPartial.Path) == module.Uri.GetLeftPart(UriPartial.Path)
            && (string.Equals(inside, path, StringComparison.OrdinalIgnoreCase) || inside.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase));
    }
}
