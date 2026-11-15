using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// A symbol of the language model is described by its kind and its scope, and by the facts the environment host has of its declaration.
/// </summary>
[TestClass]
public sealed class DescribedSymbolsTests
{
    private static readonly Uri Root = new("file:///c:/workspace/");
    private static readonly Uri Module = SymbolUri.Module(Root, "Module1");
    private static readonly Uri Procedure = SymbolUri.Member(Module, "DoSomething");
    private static readonly SourceRange Range = new(1, 4, 1, 9);

    private static IEnumerable<object[]> Symbols() =>
    [
        [new VBStandardModuleSymbol(Root, Root, "Module1"), DescribedSymbolKind.Module, DescribedScope.Module],
        [new VBClassModuleSymbol(Root, Root, "Class1"), DescribedSymbolKind.Class, DescribedScope.Module],
        [new VBLocalVariableSymbol(Root, Procedure, "count", ScopeKind.Local, Range, Range), DescribedSymbolKind.Variable, DescribedScope.Procedure],
        [new VBParameterSymbol(Root, Procedure, "value", Range, Range, ParameterKind.ImplicitByRef, VBLongType.TypeInfo), DescribedSymbolKind.Parameter, DescribedScope.Procedure],
        [new UnboundVBParameterSymbol(Root, Procedure, "value", ParameterKind.ImplicitByRef, VBLongType.TypeInfo), DescribedSymbolKind.Parameter, DescribedScope.Procedure],
        [new VBModuleFieldVariableMemberSymbol(Root, Module, "total", ScopeKind.Module, VBLongType.TypeInfo, Range, Range, AccessModifier.Private), DescribedSymbolKind.Variable, DescribedScope.Module],
        [new VBFunctionMemberSymbol(Root, Module, "Compute", ScopeKind.Module, SymbolKindExt.Function, VBLongType.TypeInfo, Range, Range, AccessModifier.Public), DescribedSymbolKind.Procedure, DescribedScope.Module],
        [new VBEnumConstMemberSymbol(Root, Module, "First", ScopeKind.Module, SymbolKindExt.EnumMember, Range, Range), DescribedSymbolKind.EnumMember, DescribedScope.Module],
    ];

    [TestMethod]
    [DynamicData(nameof(Symbols))]
    public void ASymbol_IsDescribedByWhatItIs_AndWhereItIsDeclared(Symbol symbol, DescribedSymbolKind kind, DescribedScope scope)
    {
        Assert.AreEqual(kind, DescribedSymbols.KindOf(symbol));
        Assert.AreEqual(scope, DescribedSymbols.ScopeOf(symbol));
    }

    [TestMethod]
    public void AControlOfADesigner_IsAControl()
    {
        var control = new VBModuleFieldVariableMemberSymbol(Root, Module, "OkButton", ScopeKind.Module, VBLongType.TypeInfo, Range, Range, AccessModifier.Private);
        var symbol = control with { Properties = control.Properties.Add(SymbolProperties.DesignerControl, true) };

        Assert.AreEqual(DescribedSymbolKind.Control, DescribedSymbols.KindOf(symbol));
    }

    [TestMethod]
    public void ASymbolOfALibrary_IsInTheLibraryScope()
    {
        var member = new VBFunctionMemberSymbol(Root, Module, "Len", ScopeKind.Global, SymbolKindExt.Function, VBLongType.TypeInfo, Range, Range, AccessModifier.Public);
        var symbol = member with { Properties = member.Properties.Add(SymbolProperties.Library, "VBA") };

        Assert.AreEqual(DescribedScope.Library, DescribedSymbols.ScopeOf(symbol));
    }

    [TestMethod]
    public void TheHostsFacts_AreAddedToTheDescription()
    {
        var symbol = SymbolUri.Member(Procedure, "count");
        var fact = new DeclarationFactDto(
            symbol, "count", DeclarationKind.Variable, AccessModifier.Implicit, IsImplicit: true, new SourceLocation(Module, Range), new DeclarationReferences(2, 1, 0),
            Role: DeclarationRole.InterfaceImplementation, Hides: new HiddenDeclaration(SymbolUri.Member(Module, "count"), "Module1.count"),
            AutoInstantiates: SymbolUri.Module(Root, "Class1"));

        var described = Description(symbol).WithFacts(fact);

        Assert.IsTrue(described.IsImplicit);
        Assert.AreEqual(DeclarationRole.InterfaceImplementation, described.Role);
        Assert.AreEqual(new DeclarationReferences(2, 1, 0), described.References);
        Assert.AreEqual("Module1.count", described.Hides);
        Assert.AreEqual("Class1", described.AutoInstantiates);
    }

    [TestMethod]
    public void WithNoFactFromTheHost_TheFactsAreNotKnown()
    {
        var described = Description(SymbolUri.Member(Procedure, "count")).WithFacts(null);

        Assert.IsNull(described.IsImplicit);
        Assert.IsNull(described.Role);
        Assert.IsNull(described.References);
        Assert.IsNull(described.Hides);
        Assert.IsNull(described.AutoInstantiates);
    }

    private static SymbolDescription Description(Uri symbol) => new()
    {
        Id = symbol,
        Name = "count",
        Declaration = "Dim count As Long",
        Kind = DescribedSymbolKind.Variable,
        Scope = DescribedScope.Procedure,
    };
}
