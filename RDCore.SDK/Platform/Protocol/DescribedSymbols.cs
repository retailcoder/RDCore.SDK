using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Semantics;

namespace RDCore.SDK.Platform.Protocol;

/// <summary>
/// What a <see cref="SymbolDescription"/> says of a symbol of the language model: its kind, its scope, and the facts the environment host has of its
/// declaration.
/// </summary>
public static class DescribedSymbols
{
    /// <summary>
    /// What a symbol is.
    /// </summary>
    /// <remarks>
    /// A control of a form's designer is a <see cref="DescribedSymbolKind.Control"/> (<see cref="SymbolProperties.DesignerControl"/>), and the accessors of a
    /// property are each a <see cref="DescribedSymbolKind.Property"/>. A symbol of a library is described by its <see cref="Symbol.Kind"/>: a field, and
    /// any kind with no counterpart, is a <see cref="DescribedSymbolKind.Variable"/>.
    /// </remarks>
    /// <param name="symbol">The symbol.</param>
    public static DescribedSymbolKind KindOf(Symbol symbol) => symbol switch
    {
        _ when symbol.GetProperty(SymbolProperties.DesignerControl) => DescribedSymbolKind.Control,
        VBModuleSymbol module => module.Kind == SymbolKindExt.Class ? DescribedSymbolKind.Class : DescribedSymbolKind.Module,
        VBPropertyGetMemberSymbol or VBPropertyLetMemberSymbol or VBPropertySetMemberSymbol => DescribedSymbolKind.Property,
        VBEventMemberSymbol => DescribedSymbolKind.Event,
        VBEnumMemberSymbol => DescribedSymbolKind.Enum,
        VBEnumConstMemberSymbol => DescribedSymbolKind.EnumMember,
        VBUserDefinedTypeMemberSymbol => DescribedSymbolKind.Type,
        VBUserDefinedTypeFieldSymbol => DescribedSymbolKind.TypeMember,
        VBParameterSymbol or UnboundVBParameterSymbol or ParamArrayParameterSymbol => DescribedSymbolKind.Parameter,
        VBLocalConstantSymbol or VBConstantMemberSymbol => DescribedSymbolKind.Constant,
        VBLocalVariableSymbol or VBModuleFieldVariableMemberSymbol or VBInstanceFieldVariableMemberSymbol => DescribedSymbolKind.Variable,
        VBFunctionMemberSymbol or VBProcedureMemberSymbol or VBExternalFunctionMemberSymbol or VBExternalSubMemberSymbol => DescribedSymbolKind.Procedure,
        _ => symbol.Kind switch
        {
            SymbolKindExt.Module => DescribedSymbolKind.Module,
            SymbolKindExt.Class or SymbolKindExt.Interface => DescribedSymbolKind.Class,
            SymbolKindExt.Procedure or SymbolKindExt.Function or SymbolKindExt.Operator => DescribedSymbolKind.Procedure,
            SymbolKindExt.Property => DescribedSymbolKind.Property,
            SymbolKindExt.Event => DescribedSymbolKind.Event,
            SymbolKindExt.Enum => DescribedSymbolKind.Enum,
            SymbolKindExt.EnumMember => DescribedSymbolKind.EnumMember,
            SymbolKindExt.UserDefinedType => DescribedSymbolKind.Type,
            SymbolKindExt.Constant => DescribedSymbolKind.Constant,
            _ => DescribedSymbolKind.Variable,
        },
    };

    /// <summary>
    /// Where a symbol is declared: in a procedure (a local, a parameter), in a library (<see cref="SymbolProperties.Library"/>), or else in a module.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    public static DescribedScope ScopeOf(Symbol symbol) => symbol switch
    {
        VBLocalVariableSymbol or VBLocalConstantSymbol or VBParameterSymbol or UnboundVBParameterSymbol or ParamArrayParameterSymbol => DescribedScope.Procedure,
        _ when symbol.GetProperty(SymbolProperties.Library) is { Length: > 0 } => DescribedScope.Library,
        _ => DescribedScope.Module,
    };

    /// <summary>
    /// What the language model has of a symbol: its kind and scope, its library, its declared type, its access and its documentation.
    /// </summary>
    /// <remarks>
    /// The facts only the environment host has are not known (<see cref="WithFacts"/> adds them), and neither are where the symbol is declared, its container
    /// and the class of the control array it is an element of: the caller has them from where it found the symbol.
    /// </remarks>
    /// <param name="symbol">The symbol.</param>
    /// <param name="declaration">The symbol's declaration, as code would write it.</param>
    /// <returns>The symbol's description, without the facts of the environment host.</returns>
    public static SymbolDescription Describe(Symbol symbol, string declaration)
    {
        var type = symbol is ITypedSymbol typed ? typed.ResolvedType : null;
        var (typeKind, isArray) = DeclaredTypeKinds.Of(type);
        return new SymbolDescription
        {
            Id = symbol.Uri,
            Name = symbol.Name,
            Declaration = declaration,
            Kind = KindOf(symbol),
            Scope = ScopeOf(symbol),
            Library = symbol.GetProperty(SymbolProperties.Library) is { Length: > 0 } library ? library : null,
            DeclaredType = typeKind is null ? null : type!.Name,
            TypeKind = typeKind,
            IsArray = isArray,
            Access = AccessOf(symbol),
            Documentation = symbol.GetProperty(SymbolProperties.DocString) is { Length: > 0 } documentation ? documentation : null,
        };
    }

    /// <summary>
    /// The outline of what a module declares: one entry for each of its members, in the order of the symbols.
    /// </summary>
    /// <remarks>
    /// The accessors of a property are one entry, whose range spans them all, whose selection range and declaration are its <c>Property Get</c>'s when it has
    /// one, and whose <see cref="OutlinedDeclaration.Accessors"/> names each of them. The constants of an enumeration and the members of a user-defined type are
    /// the children of its entry. A symbol declared in more than one conditional-compilation branch is at its primary declaration
    /// (<see cref="WorkspaceSymbol.PrimaryRange"/>). The locals and parameters of a procedure are not in the outline.
    /// </remarks>
    /// <param name="module">The address of the module (<see cref="SymbolUri.Module(Uri, string)"/>).</param>
    /// <param name="symbols">The module's symbols, and the symbols of its members.</param>
    /// <param name="declarationOf">A symbol's declaration, as code would write it.</param>
    /// <returns>The module's entries, an empty list when it declares no member.</returns>
    public static IReadOnlyList<OutlineEntry> OutlineOf(Uri module, IEnumerable<Symbol> symbols, Func<Symbol, string> declarationOf)
    {
        // Uri.Equals ignores the fragment, which is where a member's address is: parentage is compared by the full string.
        var children = symbols.ToLookup(symbol => symbol.ParentUri.ToString());
        var members = new List<List<WorkspaceSymbol>>();
        var properties = new Dictionary<string, List<WorkspaceSymbol>>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in children[module.ToString()].OfType<AccessibleTypedSymbol>())
        {
            if (member is not IVBPropertyMemberSymbol)
            {
                members.Add([member]);
            }
            else if (properties.TryGetValue(member.Name, out var accessors))
            {
                accessors.Add(member);
            }
            else
            {
                members.Add(properties[member.Name] = [member]);
            }
        }

        return [.. members.Select(declarations => EntryOf(declarations, children, declarationOf))];
    }

    private static OutlineEntry EntryOf(IReadOnlyList<WorkspaceSymbol> declarations, ILookup<string, Symbol> children, Func<Symbol, string> declarationOf)
    {
        var main = declarations.FirstOrDefault(declaration => declaration is VBPropertyGetMemberSymbol) ?? declarations[0];
        var type = main is ITypedSymbol typed ? typed.ResolvedType : null;
        var (typeKind, isArray) = DeclaredTypeKinds.Of(type);
        var accessors = declarations.Aggregate(PropertyAccessors.None, (all, declaration) => all | declaration switch
        {
            VBPropertyGetMemberSymbol => PropertyAccessors.Get,
            VBPropertyLetMemberSymbol => PropertyAccessors.Let,
            VBPropertySetMemberSymbol => PropertyAccessors.Set,
            _ => PropertyAccessors.None,
        });

        return new OutlineEntry
        {
            Name = main.Name,
            Id = main.Uri,
            Kind = KindOf(main),
            Declaration = declarationOf(main),
            Access = AccessOf(main),
            DeclaredType = typeKind is null ? null : type!.Name,
            TypeKind = typeKind,
            IsArray = isArray,
            Accessors = FlagNames.Of(accessors),
            Range = new SourceRange(declarations.Min(declaration => declaration.PrimaryRange.Start), declarations.Max(declaration => declaration.PrimaryRange.End)),
            SelectionRange = main.PrimarySelectionRange,
            Children = [.. children[main.Uri.ToString()]
                .OfType<WorkspaceSymbol>()
                .Where(child => child is VBEnumConstMemberSymbol or VBUserDefinedTypeFieldSymbol)
                .Select(child => EntryOf([child], children, declarationOf))],
        };
    }

    private static AccessModifier AccessOf(Symbol symbol) => symbol is AccessibleTypedSymbol accessible ? accessible.AccessModifier : AccessModifier.Implicit;

    /// <summary>
    /// The description, with what the environment host knows of the symbol's declaration: whether it is implicit, its role, its references, what it hides,
    /// and the class it auto-instantiates.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="fact">
    /// The host's fact of the declaration (<see cref="ModuleSemanticsDto.Declarations"/>); <see langword="null"/> when the host has none, which leaves those
    /// facts <see langword="null"/>: not known.
    /// </param>
    public static SymbolDescription WithFacts(this SymbolDescription description, DeclarationFactDto? fact) => fact is null ? description : description with
    {
        IsImplicit = fact.IsImplicit,
        Role = fact.Role,
        References = fact.References,
        Hides = fact.Hides?.QualifiedName,
        AutoInstantiates = fact.AutoInstantiates is { } created ? SymbolUri.NameOf(created) : null,
    };
}
