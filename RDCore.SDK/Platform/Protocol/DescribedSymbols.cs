using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;

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
