using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Complex;

namespace RDCore.Tests.Model.Types;

/// <summary>
/// <see cref="VBArrayType.SameElementType"/>: whether an array given is an array of the element type a parameter declares.
/// </summary>
[TestClass]
public sealed class VBArrayTypeTests
{
    private static readonly Uri Root = TestUri.WorkspaceRoot();

    private static VBEnumType Enum(string name)
        => new(new VBEnumMemberSymbol(Root, TestUri.TestModuleUri(), name, ScopeKind.Module, SymbolKindExt.Enum,
            VBUnknownType.TypeInfo, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public), members: null);

    [TestMethod]
    public void AnElementTypeOfTheSameKindAndName_IsTheSame()
    {
        Assert.IsTrue(VBArrayType.SameElementType(VBLongType.TypeInfo, VBLongType.TypeInfo));
        Assert.IsTrue(VBArrayType.SameElementType(VBStringType.TypeInfo, VBStringType.TypeInfo));
    }

    [TestMethod]
    public void AnotherIntrinsicType_IsNotTheSame()
    {
        Assert.IsFalse(VBArrayType.SameElementType(VBLongType.TypeInfo, VBIntegerType.TypeInfo));
        Assert.IsFalse(VBArrayType.SameElementType(VBVariantType.TypeInfo, VBLongType.TypeInfo));
    }

    [TestMethod]
    public void AFixedLengthString_IsAStringOfAnotherKind()
    {
        Assert.IsFalse(VBArrayType.SameElementType(VBStringType.TypeInfo, new VBFixedStringType(10)));
        Assert.IsFalse(VBArrayType.SameElementType(new VBFixedStringType(10), VBStringType.TypeInfo));
    }

    [TestMethod]
    public void AnEnum_IsNotALong_AndAnyTwoEnumsAreTheSame()
    {
        Assert.IsFalse(VBArrayType.SameElementType(VBLongType.TypeInfo, Enum("Colour")));
        Assert.IsFalse(VBArrayType.SameElementType(Enum("Colour"), VBLongType.TypeInfo));
        Assert.IsTrue(VBArrayType.SameElementType(Enum("Colour"), Enum("Shape")));
    }

    [TestMethod]
    public void ATypeThatIsNotKnown_IsNotATypeThatDiffers()
    {
        Assert.IsTrue(VBArrayType.SameElementType(VBUnknownType.TypeInfo, VBLongType.TypeInfo));
        Assert.IsTrue(VBArrayType.SameElementType(VBStringType.TypeInfo, VBUnknownType.TypeInfo));
    }
}
