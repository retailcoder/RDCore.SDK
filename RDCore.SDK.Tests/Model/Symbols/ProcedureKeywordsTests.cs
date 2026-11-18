using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Complex;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// A procedure is declared with its keywords: the three accessors of a property, which share its name and its kind, by <c>Property Get</c>,
/// <c>Property Let</c> and <c>Property Set</c>.
/// </summary>
[TestClass]
public sealed class ProcedureKeywordsTests
{
    private static readonly Uri Root = new("file:///c:/workspace/");
    private static readonly Uri Module = SymbolUri.Module(Root, "Class1");
    private static readonly SourceRange Range = new(1, 4, 1, 9);

    private static IEnumerable<object?[]> Symbols() =>
    [
        [new VBPropertyGetMemberSymbol(Root, Module, ScopeKind.Module, "Value", Range, Range, AccessModifier.Public), "Property Get"],
        [new VBPropertyLetMemberSymbol(Root, Module, "Value", ScopeKind.Module, SymbolKindExt.Property, VBVoidType.TypeInfo, Range, Range, AccessModifier.Public), "Property Let"],
        [new VBPropertySetMemberSymbol(Root, Module, "Value", ScopeKind.Module, SymbolKindExt.Property, VBVoidType.TypeInfo, Range, Range, AccessModifier.Public), "Property Set"],
        [new UnboundVBPropertyGetMemberSymbol(Root, Module, ScopeKind.Module, "Value"), "Property Get"],
        [new UnboundVBPropertyLetMemberSymbol(Root, Module, "Value", ScopeKind.Module, SymbolKindExt.Property, VBVoidType.TypeInfo), "Property Let"],
        [new UnboundVBPropertySetMemberSymbol(Root, Module, "Value", ScopeKind.Module, SymbolKindExt.Property, VBVoidType.TypeInfo), "Property Set"],
        [new VBProcedureMemberSymbol(Root, Module, "DoSomething", ScopeKind.Module, SymbolKindExt.Procedure, VBVoidType.TypeInfo, Range, Range, AccessModifier.Public), "Sub"],
        [new UnboundVBProcedureMemberSymbol(Root, Module, "DoSomething", ScopeKind.Module, SymbolKindExt.Procedure, VBVoidType.TypeInfo), "Sub"],
        [new VBFunctionMemberSymbol(Root, Module, "Compute", ScopeKind.Module, SymbolKindExt.Function, VBLongType.TypeInfo, Range, Range, AccessModifier.Public), "Function"],
        [new UnboundVBFunctionMemberSymbol(Root, Module, "Compute", ScopeKind.Module, SymbolKindExt.Function, VBLongType.TypeInfo), "Function"],
        [new VBExternalSubMemberSymbol(Root, Module, "Sleep", ScopeKind.Module, SymbolKindExt.Procedure, VBVoidType.TypeInfo, Range, Range, AccessModifier.Private, true, "kernel32", null), null],
        [new VBExternalFunctionMemberSymbol(Root, Module, "GetTickCount", ScopeKind.Module, SymbolKindExt.Function, VBLongType.TypeInfo, Range, Range, AccessModifier.Private, true, "kernel32", null), null],
        [new VBConstantMemberSymbol(Root, Module, "Limit", ScopeKind.Module, VBLongType.TypeInfo, Range, Range, AccessModifier.Private), null],
        [new VBEventMemberSymbol(Root, Module, "Changed", ScopeKind.Module, Range, Range, AccessModifier.Public), null],
    ];

    [TestMethod]
    [DynamicData(nameof(Symbols))]
    public void AProcedure_IsDeclaredWithItsKeywords_AndTheAccessorsOfAPropertyWithTheirs(Symbol symbol, string? expected)
        => Assert.AreEqual(expected, ProcedureKeywords.Of(symbol), symbol.GetType().Name);

    [TestMethod]
    [DataRow(MemberKind.Procedure, "Sub")]
    [DataRow(MemberKind.Function, "Function")]
    [DataRow(MemberKind.PropertyGet, "Property Get")]
    [DataRow(MemberKind.PropertyLet, "Property Let")]
    [DataRow(MemberKind.PropertySet, "Property Set")]
    [DataRow(MemberKind.ExternalProcedure, null)]
    [DataRow(MemberKind.ExternalFunction, null)]
    [DataRow(MemberKind.Event, null)]
    [DataRow(MemberKind.ModuleField, null)]
    public void AKindOfProcedure_IsDeclaredWithItsKeywords(MemberKind kind, string? expected)
        => Assert.AreEqual(expected, ProcedureKeywords.Of(kind));
}
