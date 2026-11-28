using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;

namespace RDCore.Tests.Model.Types;

/// <summary>
/// What the candidates of an inferable type resolve to (<see cref="IVBInferableTypeExtensions.Resolve"/>), and how a candidate joins them
/// (<see cref="IVBInferableTypeExtensions.MergeCandidate"/>).
/// </summary>
[TestClass]
public sealed class VBInferableTypeExtensionsTests
{
    private static readonly Uri Root = new("file:///c:/ws/");

    private static VBDeferredModuleType Deferred() => new("Widget", new Uri("file:///c:/ws/#Widget"));

    private static readonly VBClassType Widget = new(new VBClassModuleSymbol(Root, Root, "Widget"), []);
    private static readonly VBClassType Gadget = new(new VBClassModuleSymbol(Root, Root, "Gadget"), []);

    private static VBType TypeNamed(string name) => name switch
    {
        "Byte" => VBByteType.TypeInfo,
        "Integer" => VBIntegerType.TypeInfo,
        "Long" => VBLongType.TypeInfo,
        "LongLong" => VBLongLongType.TypeInfo,
        "Single" => VBSingleType.TypeInfo,
        "Double" => VBDoubleType.TypeInfo,
        "Currency" => VBCurrencyType.TypeInfo,
        "Decimal" => VBDecimalType.TypeInfo,
        "Boolean" => VBBooleanType.TypeInfo,
        "String" => VBStringType.TypeInfo,
        "Date" => VBDateType.TypeInfo,
        "Object" => VBObjectType.TypeInfo,
        "Variant" => VBVariantType.TypeInfo,
        "Widget" => Widget,
        "Gadget" => Gadget,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "a type the grid does not name"),
    };

    private static IVBInferableType WithCandidates(string candidates)
        => candidates.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate((IVBInferableType)Deferred(), (inferable, name) => inferable.WithCandidateType(TypeNamed(name)));

    [TestMethod]
    public void Resolve_Null_WhenThereAreNoCandidates()
        => Assert.IsNull(Deferred().Resolve());

    // what the writes give a variable (candidates), and the type it is declared as (resolved).
    [TestMethod]
    [DataRow("Integer", "Integer", DisplayName = "one candidate keeps its exact type")]
    [DataRow("Byte", "Byte")]
    [DataRow("Single", "Single")]
    [DataRow("LongLong", "LongLong")]
    [DataRow("Byte Integer", "Integer", DisplayName = "the widest numeric")]
    [DataRow("Integer Long", "Long")]
    [DataRow("Long Single", "Single")]
    [DataRow("Single Double", "Double")]
    [DataRow("Byte Integer Long Single Double", "Double")]
    [DataRow("Integer Integer", "Integer", DisplayName = "the same candidate twice is one")]
    [DataRow("Boolean", "Boolean")]
    [DataRow("Boolean Integer", "Variant", DisplayName = "Boolean is never widened to a number")]
    [DataRow("Boolean Byte", "Variant")]
    [DataRow("Boolean Double", "Variant")]
    [DataRow("String", "String")]
    [DataRow("String Integer", "Variant")]
    [DataRow("Date Double", "Variant")]
    [DataRow("Currency Long", "Variant")]
    [DataRow("Currency Decimal", "Variant")]
    [DataRow("LongLong Long", "Variant", DisplayName = "LongLong is all-or-nothing")]
    [DataRow("Widget", "Widget", DisplayName = "one class")]
    [DataRow("Widget Gadget", "Object", DisplayName = "several classes")]
    [DataRow("Widget Object", "Object")]
    [DataRow("Object", "Object")]
    [DataRow("Widget Long", "Variant", DisplayName = "a class and a value")]
    [DataRow("Variant Long", "Variant")]
    public void Resolve(string candidates, string resolved)
        => Assert.AreEqual(TypeNamed(resolved), WithCandidates(candidates).Resolve());

    [TestMethod]
    public void WithCandidateType_KeepsTheExactTypeOfEachCandidate()
    {
        var inferable = WithCandidates("Byte Integer");

        CollectionAssert.AreEquivalent(new VBType[] { VBByteType.TypeInfo, VBIntegerType.TypeInfo }, inferable.CandidateTypes.ToArray());
    }

    [TestMethod]
    public void WithCandidateType_MergesAnotherInferableTypesCandidates_InsteadOfAddingItOpaquely()
    {
        var other = Deferred().WithCandidateType(VBStringType.TypeInfo);

        var inferable = Deferred().WithCandidateType(VBLongType.TypeInfo).WithCandidateType((VBDeferredModuleType)other);

        CollectionAssert.AreEquivalent(new VBType[] { VBLongType.TypeInfo, VBStringType.TypeInfo }, inferable.CandidateTypes.ToArray());
    }

    [TestMethod]
    public void WithCandidateType_AddsAClassNoModuleDeclares_AsItself()
    {
        // Set w = New Thing, where no module declares Thing: the candidate is the class the code names, which has no candidates of its own.
        var thing = new VBDeferredClassType("Thing", new Uri("file:///c:/ws/#Thing"));

        var inferable = Deferred().WithCandidateType(thing);

        Assert.AreEqual(thing, inferable.Resolve());
    }
}
