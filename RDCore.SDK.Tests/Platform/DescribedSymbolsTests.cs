using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Complex;
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

    [TestMethod]
    public void TheModelsFacts_DescribeTheSymbol()
    {
        var field = new VBModuleFieldVariableMemberSymbol(Root, Module, "total", ScopeKind.Module, VBLongType.TypeInfo, Range, Range, AccessModifier.Private)
            .With(SymbolProperties.DocString, "The running total.");

        var described = DescribedSymbols.Describe(field, "Private total As Long");

        Assert.AreEqual(field.Uri, described.Id);
        Assert.AreEqual("total", described.Name);
        Assert.AreEqual("Private total As Long", described.Declaration);
        Assert.AreEqual(DescribedSymbolKind.Variable, described.Kind);
        Assert.AreEqual(DescribedScope.Module, described.Scope);
        Assert.AreEqual("Long", described.DeclaredType);
        Assert.AreEqual(DeclaredTypeKind.Intrinsic, described.TypeKind);
        Assert.IsFalse(described.IsArray);
        Assert.AreEqual(AccessModifier.Private, described.Access);
        Assert.AreEqual("The running total.", described.Documentation);
        Assert.IsNull(described.Library);
        Assert.IsNull(described.IsImplicit);
    }

    [TestMethod]
    public void ASub_HasNoDeclaredType()
    {
        var sub = new VBProcedureMemberSymbol(Root, Module, "DoSomething", ScopeKind.Module, SymbolKindExt.Procedure, VBVoidType.TypeInfo, Range, Range, AccessModifier.Public);

        var described = DescribedSymbols.Describe(sub, "Public Sub DoSomething()");

        Assert.IsNull(described.DeclaredType);
        Assert.IsNull(described.TypeKind);
    }

    [TestMethod]
    public void TheOutline_HasOneEntryPerMember_ThePropertyAccessorsAsOne_AndTheEnumConstantsAsChildren()
    {
        var sub = new VBProcedureMemberSymbol(Root, Module, "DoSomething", ScopeKind.Module, SymbolKindExt.Procedure, VBVoidType.TypeInfo, new(1, 0, 3, 7), new(1, 4, 1, 15), AccessModifier.Public);
        var local = new VBLocalVariableSymbol(Root, sub.Uri, "count", ScopeKind.Local, new(2, 4, 2, 19), new(2, 8, 2, 13));
        var get = new VBPropertyGetMemberSymbol(Root, Module, ScopeKind.Module, "Size", new(5, 0, 7, 12), new(5, 13, 5, 17), AccessModifier.Public);
        var let = new VBPropertyLetMemberSymbol(Root, Module, "size", ScopeKind.Module, SymbolKindExt.Property, VBVoidType.TypeInfo, new(9, 0, 11, 12), new(9, 13, 9, 17), AccessModifier.Public);
        var colors = new VBEnumMemberSymbol(Root, Module, "Colors", ScopeKind.Module, SymbolKindExt.Enum, VBUnknownType.TypeInfo, new(13, 0, 15, 8), new(13, 12, 13, 18), AccessModifier.Public);
        var red = new VBEnumConstMemberSymbol(Root, colors.Uri, "Red", ScopeKind.Module, SymbolKindExt.EnumMember, new(14, 4, 14, 7), new(14, 4, 14, 7));

        var entries = DescribedSymbols.OutlineOf(Module, [sub, local, get, let, colors, red], symbol => $"declaration of {symbol.Name}");

        CollectionAssert.AreEqual(new[] { "DoSomething", "Size", "Colors" }, entries.Select(entry => entry.Name).ToArray());
        Assert.AreEqual(0, entries[0].Children.Count);
        Assert.AreEqual(new SourceRange(1, 4, 1, 15), entries[0].SelectionRange);

        var size = entries[1];
        Assert.AreEqual(DescribedSymbolKind.Property, size.Kind);
        CollectionAssert.AreEqual(new[] { "Get", "Let" }, size.Accessors.ToArray());
        Assert.AreEqual(new SourceRange(5, 0, 11, 12), size.Range);
        Assert.AreEqual(new SourceRange(5, 13, 5, 17), size.SelectionRange);
        Assert.AreEqual("declaration of Size", size.Declaration);

        var constant = entries[2].Children.Single();
        Assert.AreEqual("Red", constant.Name);
        Assert.AreEqual(DescribedSymbolKind.EnumMember, constant.Kind);
    }

    [TestMethod]
    public void TheExpressionAtAPosition_IsTheInnermostThatHoldsIt()
    {
        var procedure = SymbolUri.Member(Module, "DoSomething");
        ExpressionFactDto Fact(int start, int end, string type) => new(
            new SyntaxNodeId(Module.ToString(), [0, start]), new SourceLocation(Module, new SourceRange(2, start, 2, end)), type,
            ExpressionClassification.Value, null, default, null);
        var semantics = new ModuleSemanticsDto(Module, true, [], [new ProcedureSemanticsDto(procedure, true, [], [Fact(4, 20, "Double"), Fact(10, 15, "Long")])], []);

        Assert.AreEqual("Long", ExpressionDescription.At(semantics, new SourcePosition(2, 12))?.DeclaredType);
        Assert.AreEqual("Double", ExpressionDescription.At(semantics, new SourcePosition(2, 5))?.DeclaredType);
        Assert.IsNull(ExpressionDescription.At(semantics, new SourcePosition(2, 20)));
        Assert.IsNull(ExpressionDescription.At(semantics, new SourcePosition(3, 0)));
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
