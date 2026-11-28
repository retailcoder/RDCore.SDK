using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Semantics;

/// <summary>
/// <see cref="DeclaredClassFact.Of"/>: the classes of the workspace a declared type names, with the members of their public interface, and the facts that
/// travel with a declaration (<see cref="DeclarationFact.DeclaredClasses"/>).
/// </summary>
[TestClass]
public sealed class DeclaredClassFactTests
{
    private static readonly Uri Root = new("file:///c:/workspace/");
    private static readonly Uri Library = new("file:///c:/libraries/scripting/");

    private static SourceRange Line(int line) => new(line, 0, line, 10);

    private static VBParameterSymbol Parameter(Uri procedure, string name, VBType type, ParameterKind kind = ParameterKind.ExplicitByVal)
        => new(Root, procedure, name, Line(0), Line(0), kind, type);

    private static readonly VBClassModuleSymbol PersonModule = new(Root, Root, "Person");
    private static readonly VBClassModuleSymbol TeamModule = new(Root, Root, "Team");
    private static readonly VBClassModuleSymbol DictionaryModule = (VBClassModuleSymbol)new VBClassModuleSymbol(Library, Library, "Dictionary")
        .With(SymbolProperties.Library, "Scripting");

    private static VBClassModuleSymbol Person
    {
        get
        {
            var owner = PersonModule.Uri;
            var badge = new VBPropertyGetMemberSymbol(Root, owner, ScopeKind.Instance, "Badge", Line(4), Line(4), AccessModifier.Public)
            {
                ResolvedType = VBStringType.TypeInfo,
            };
            var slot = new VBPropertyLetMemberSymbol(Root, owner, "Slot", ScopeKind.Instance, SymbolKindExt.Property, VBVoidType.TypeInfo, Line(6), Line(6),
                AccessModifier.Public);
            return PersonModule with
            {
                DefaultInterfaceMembers =
                [
                    new VBInstanceFieldVariableMemberSymbol(Root, owner, "Name", Line(1), Line(1), VBStringType.TypeInfo, AccessModifier.Public),
                    new VBInstanceFieldVariableMemberSymbol(Root, owner, "Age", Line(2), Line(2), VBLongType.TypeInfo, AccessModifier.Public),
                    new VBInstanceFieldVariableMemberSymbol(Root, owner, "Hidden", Line(3), Line(3), VBLongType.TypeInfo, AccessModifier.Private),
                    // the Me every member of a class has is no parameter a call gives.
                    badge with { Parameters = [Parameter(badge.Uri, Tokens.Me, VBClassType.FromClassModule(PersonModule), ParameterKind.ImplicitByRef)] },
                    new VBPropertyLetMemberSymbol(Root, owner, "Code", ScopeKind.Instance, SymbolKindExt.Property, VBVoidType.TypeInfo, Line(5), Line(5),
                        AccessModifier.Friend) { Parameters = [Parameter(owner, "value", VBLongType.TypeInfo)] },
                    slot with { Parameters = [Parameter(slot.Uri, "index", VBLongType.TypeInfo), Parameter(slot.Uri, "value", VBDoubleType.TypeInfo)] },
                    new VBProcedureMemberSymbol(Root, owner, "Greet", ScopeKind.Instance, SymbolKindExt.Procedure, VBVoidType.TypeInfo, Line(7), Line(7),
                        AccessModifier.Public),
                    new VBFunctionMemberSymbol(Root, owner, "Score", ScopeKind.Instance, SymbolKindExt.Function, VBDoubleType.TypeInfo, Line(8), Line(8),
                        AccessModifier.Implicit),
                    new VBEventMemberSymbol(Root, owner, "Changed", ScopeKind.Instance, Line(9), Line(9), AccessModifier.Public),
                ],
            };
        }
    }

    private static VBClassModuleSymbol Team
    {
        get
        {
            var owner = TeamModule.Uri;
            var get = new VBPropertyGetMemberSymbol(Root, owner, ScopeKind.Instance, "Lead", Line(2), Line(2), AccessModifier.Public)
            {
                ResolvedType = VBClassType.FromClassModule(PersonModule),
            };
            var set = new VBPropertySetMemberSymbol(Root, owner, "Lead", ScopeKind.Instance, SymbolKindExt.Property, VBVoidType.TypeInfo, Line(3), Line(3),
                AccessModifier.Public);
            return TeamModule with
            {
                DefaultInterfaceMembers =
                [
                    new VBInstanceFieldVariableMemberSymbol(Root, owner, "Name", Line(1), Line(1), VBStringType.TypeInfo, AccessModifier.Public),
                    get,
                    set with { Parameters = [Parameter(set.Uri, "value", VBClassType.FromClassModule(PersonModule))] },
                    new VBInstanceFieldVariableMemberSymbol(Root, owner, "Tags", Line(4), Line(4), VBVariantType.TypeInfo, AccessModifier.Public),
                    // a class module has no Public array variable (in MS-VBA, a compile error): a Property Get returns it.
                    new VBPropertyGetMemberSymbol(Root, owner, ScopeKind.Instance, "Members", Line(5), Line(5), AccessModifier.Public)
                    {
                        ResolvedType = new VBResizableArrayType(VBClassType.FromClassModule(PersonModule)),
                    },
                    new VBInstanceFieldVariableMemberSymbol(Root, owner, "Lookup", Line(6), Line(6), VBClassType.FromClassModule(DictionaryModule),
                        AccessModifier.Public),
                    new VBInstanceFieldVariableMemberSymbol(Root, owner, "Parent", Line(7), Line(7), VBClassType.FromClassModule(TeamModule),
                        AccessModifier.Public),
                ],
            };
        }
    }

    // the class modules of the workspace as the binding has them, and the library's class as its library does.
    private static VBClassModuleSymbol? ClassModuleOf(VBClassType type) => type.Symbol.Name switch
    {
        "Person" => Person,
        "Team" => Team,
        "Dictionary" => DictionaryModule,
        _ => null,
    };

    private static ClassMemberFact MemberOf(DeclaredClassFact declared, string name) => declared.Members.Single(member => member.Name == name);

    [TestMethod]
    public void TheMembersOfAClass_AreItsPublicVariablesPropertiesAndMethods_InTheOrderItDeclaresThem()
    {
        var person = DeclaredClassFact.Of(VBClassType.FromClassModule(PersonModule), ClassModuleOf).Single();

        Assert.AreEqual(PersonModule.Uri.AbsoluteUri, person.Class.AbsoluteUri);
        CollectionAssert.AreEqual(new[] { "Name", "Age", "Badge", "Slot", "Greet", "Score" }, person.Members.Select(member => member.Name).ToArray());
    }

    [TestMethod]
    [DataRow("Name", ExpressionClassification.Variable, PropertyAccessors.None, 0, DisplayName = "a Public variable")]
    [DataRow("Badge", ExpressionClassification.Property, PropertyAccessors.Get, 0, DisplayName = "a Property Get, its Me no parameter")]
    [DataRow("Slot", ExpressionClassification.Property, PropertyAccessors.Let, 1, DisplayName = "a Property Let that takes an index besides its value")]
    [DataRow("Greet", ExpressionClassification.Subroutine, PropertyAccessors.None, 0, DisplayName = "a Sub")]
    [DataRow("Score", ExpressionClassification.Function, PropertyAccessors.None, 0, DisplayName = "a Function with no access modifier")]
    public void AMember_SaysWhatItIs_AndWhatItTakes(string name, ExpressionClassification classification, PropertyAccessors accessors, int parameters)
    {
        var member = MemberOf(DeclaredClassFact.Of(VBClassType.FromClassModule(PersonModule), ClassModuleOf).Single(), name);

        Assert.AreEqual(classification, member.Classification);
        Assert.AreEqual(accessors, member.Accessors);
        Assert.AreEqual(parameters, member.Parameters);
    }

    [TestMethod]
    [DataRow("Name", DeclaredTypeKind.Intrinsic, "String", DisplayName = "a variable's")]
    [DataRow("Badge", DeclaredTypeKind.Intrinsic, "String", DisplayName = "what a Property Get is read as")]
    [DataRow("Slot", DeclaredTypeKind.Intrinsic, "Double", DisplayName = "what a property with no Get is given")]
    [DataRow("Score", DeclaredTypeKind.Intrinsic, "Double", DisplayName = "what a Function returns")]
    [DataRow("Greet", null, null, DisplayName = "a Sub has none")]
    public void AMembersType_IsWhatItIsReadAsOrGiven(string name, DeclaredTypeKind? kind, string? typeName)
    {
        var member = MemberOf(DeclaredClassFact.Of(VBClassType.FromClassModule(PersonModule), ClassModuleOf).Single(), name);

        Assert.AreEqual(kind, member.TypeKind);
        Assert.AreEqual(typeName, member.TypeName);
    }

    [TestMethod]
    public void TheAccessorsOfAProperty_AreOneMember()
    {
        var lead = MemberOf(DeclaredClassFact.Of(VBClassType.FromClassModule(TeamModule), ClassModuleOf).First(), "Lead");

        Assert.AreEqual(PropertyAccessors.Get | PropertyAccessors.Set, lead.Accessors);
        Assert.AreEqual(0, lead.Parameters);
    }

    // Team names Person twice (Lead, Members) and itself (Parent): each class once, the declared one first.
    [TestMethod]
    public void TheClassesTheMembersTypesName_FollowTheDeclaredClass_EachOnce()
    {
        var classes = DeclaredClassFact.Of(VBClassType.FromClassModule(TeamModule), ClassModuleOf);

        CollectionAssert.AreEqual(new[] { "Team", "Person" }, classes.Select(declared => declared.Name).ToArray());
    }

    [TestMethod]
    [DataRow("Lead", false, DisplayName = "a class")]
    [DataRow("Members", true, DisplayName = "an array of a class")]
    public void AMemberOfAClassOfTheWorkspace_NamesItsModule(string name, bool isArray)
    {
        var member = MemberOf(DeclaredClassFact.Of(VBClassType.FromClassModule(TeamModule), ClassModuleOf).First(), name);

        Assert.AreEqual(DeclaredTypeKind.Class, member.TypeKind);
        Assert.AreEqual(isArray, member.IsArray);
        Assert.AreEqual("Person", member.TypeName);
        Assert.AreEqual(PersonModule.Uri.AbsoluteUri, member.TypeClass?.AbsoluteUri);
        Assert.IsNull(member.TypeLibrary);
    }

    [TestMethod]
    public void AMemberOfALibrarysClass_NamesItsLibrary()
    {
        var lookup = MemberOf(DeclaredClassFact.Of(VBClassType.FromClassModule(TeamModule), ClassModuleOf).First(), "Lookup");

        Assert.AreEqual("Dictionary", lookup.TypeName);
        Assert.AreEqual("Scripting", lookup.TypeLibrary);
        Assert.IsNull(lookup.TypeClass);
    }

    [TestMethod]
    public void AnArrayOfAClassOfTheWorkspace_StatesTheClass()
        => CollectionAssert.AreEqual(new[] { "Person" },
            DeclaredClassFact.Of(new VBResizableArrayType(VBClassType.FromClassModule(PersonModule)), ClassModuleOf)
                .Select(declared => declared.Name).ToArray());

    [TestMethod]
    [DataRow("Dictionary", DisplayName = "a library's class")]
    [DataRow("Long", DisplayName = "an intrinsic type")]
    [DataRow("Variant", DisplayName = "Variant")]
    public void ATypeThatIsNoClassOfTheWorkspace_StatesNone(string type)
        => Assert.IsEmpty(DeclaredClassFact.Of(
            type switch
            {
                "Dictionary" => VBClassType.FromClassModule(DictionaryModule),
                "Long" => VBLongType.TypeInfo,
                _ => VBVariantType.TypeInfo,
            },
            ClassModuleOf));

    [TestMethod]
    public void TheDeclaredClasses_AreReadBackAsTheyWereWritten()
    {
        var module = new UriBuilder(Root) { Fragment = "Program" }.Uri;
        var parameter = new UriBuilder(Root) { Fragment = "Program.Post.body" }.Uri;
        var declaration = new DeclarationFact(new SemanticId(parameter), "body", DeclarationKind.Parameter, AccessModifier.Implicit, false,
            new SourceLocation(module, SourceRange.Empty), null)
        {
            DeclaredClasses = DeclaredClassFact.Of(VBClassType.FromClassModule(TeamModule), ClassModuleOf),
        };
        var model = new ModuleSemanticModel(module, [], []) { Declarations = [declaration] };

        var read = PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single()
            .Declarations.Single().DeclaredClasses;

        Assert.IsNotNull(read);
        CollectionAssert.AreEqual(new[] { "Team", "Person" }, read.Value.Select(declared => declared.Name).ToArray());
        var lead = MemberOf(read.Value[0], "Lead");
        Assert.AreEqual(PropertyAccessors.Get | PropertyAccessors.Set, lead.Accessors);
        Assert.AreEqual(PersonModule.Uri.AbsoluteUri, lead.TypeClass?.AbsoluteUri);
        Assert.AreEqual("Scripting", MemberOf(read.Value[0], "Lookup").TypeLibrary);
        Assert.AreEqual(ExpressionClassification.Function, MemberOf(read.Value[1], "Score").Classification);
    }

    [TestMethod]
    public void ADeclarationWithoutDeclaredClasses_TravelsWithoutThem()
    {
        var module = new UriBuilder(Root) { Fragment = "Program" }.Uri;
        var declaration = new DeclarationFact(new SemanticId(new UriBuilder(Root) { Fragment = "Program.count" }.Uri), "count", DeclarationKind.Variable,
            AccessModifier.Private, false, new SourceLocation(module, SourceRange.Empty), null);

        Assert.IsNull(ModuleSemanticsDto.From(new ModuleSemanticModel(module, [], []) { Declarations = [declaration] }).Declarations.Single().DeclaredClasses);
    }
}
