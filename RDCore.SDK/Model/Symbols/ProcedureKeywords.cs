using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;

namespace RDCore.SDK.Model.Symbols;

/// <summary>
/// The keywords a procedure is declared with, after its access modifier: <c>Sub</c>, <c>Function</c>, <c>Property Get</c>, <c>Property Let</c> or
/// <c>Property Set</c> (<strong>MS-VBAL §5.3.1</strong>). They are VBA keywords, written as code writes them and never translated.
/// </summary>
/// <remarks>
/// The three accessors of a property share its name and its <see cref="SymbolKindExt.Property"/> kind: their keywords are what tell them apart.
/// </remarks>
public static class ProcedureKeywords
{
    private static readonly string PropertyGet = $"{Tokens.Property} {Tokens.Get}";
    private static readonly string PropertyLet = $"{Tokens.Property} {Tokens.Let}";
    private static readonly string PropertySet = $"{Tokens.Property} {Tokens.Set}";

    /// <summary>
    /// The keywords of a kind of procedure.
    /// </summary>
    /// <param name="kind">The kind of member.</param>
    /// <returns>
    /// The keywords, as in <c>Property Let</c>; <see langword="null"/> for a member that is not a procedure, and for a <c>Declare</c> statement, whose
    /// keywords the kind does not give: it is written with or without <c>PtrSafe</c>.
    /// </returns>
    public static string? Of(MemberKind kind) => kind switch
    {
        MemberKind.Procedure => Tokens.Sub,
        MemberKind.Function => Tokens.Function,
        MemberKind.PropertyGet => PropertyGet,
        MemberKind.PropertyLet => PropertyLet,
        MemberKind.PropertySet => PropertySet,
        _ => null,
    };

    /// <summary>
    /// The keywords of the procedure a symbol is, declared in the workspace or in a library.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>
    /// The keywords, as in <c>Property Let</c>; <see langword="null"/> for a symbol that is not a procedure, and for a procedure a <c>Declare</c> statement
    /// declares.
    /// </returns>
    public static string? Of(Symbol symbol) => symbol switch
    {
        VBExternalSubMemberSymbol or VBExternalFunctionMemberSymbol => null,
        VBPropertyGetMemberSymbol or UnboundVBPropertyGetMemberSymbol => PropertyGet,
        VBPropertyLetMemberSymbol or UnboundVBPropertyLetMemberSymbol => PropertyLet,
        VBPropertySetMemberSymbol or UnboundVBPropertySetMemberSymbol => PropertySet,
        VBFunctionMemberSymbol or UnboundVBFunctionMemberSymbol => Tokens.Function,
        VBProcedureMemberSymbol or UnboundVBProcedureMemberSymbol => Tokens.Sub,
        _ => null,
    };
}
