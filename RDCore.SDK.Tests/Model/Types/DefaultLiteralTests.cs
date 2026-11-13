using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;

namespace RDCore.Tests.Model.Types;

/// <summary>
/// <see cref="IntrinsicVBTypes.DefaultLiteralOf"/>: the literal that writes the value a variable of a type is initialized to (MS-VBAL §2.3), and none for a
/// type whose default no literal writes.
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.3 Variables")]
public sealed class DefaultLiteralTests
{
    public static IEnumerable<object?[]> Types =>
    [
        [VBByteType.TypeInfo, "0"],
        [VBIntegerType.TypeInfo, "0"],
        [VBLongType.TypeInfo, "0"],
        [VBLongLongType.TypeInfo, "0"],
        [VBLongPtrType_x64.TypeInfo, "0"],
        [VBLongPtrType_x86.TypeInfo, "0"],
        [VBSingleType.TypeInfo, "0"],
        [VBDoubleType.TypeInfo, "0"],
        [VBCurrencyType.TypeInfo, "0"],
        [VBDecimalType.TypeInfo, "0"],
        [VBStringType.TypeInfo, "\"\""],
        [new VBFixedStringType(4), null],
        [VBBooleanType.TypeInfo, "False"],
        [VBDateType.TypeInfo, "#12:00:00 AM#"],
        [VBVariantType.TypeInfo, "Empty"],
        [VBObjectType.TypeInfo, "Nothing"],
        [VBResizableArrayType.TypeInfo, null],
    ];

    [TestMethod]
    [DynamicData(nameof(Types))]
    public void TheDefaultLiteral_OfAnIntrinsicType(VBType type, string? expected)
        => Assert.AreEqual(expected, IntrinsicVBTypes.DefaultLiteralOf(type));

    [TestMethod]
    public void TheDefaultLiteral_OfAnEnum_IsZero()
    {
        var root = TestUri.WorkspaceRoot();
        var symbol = new VBEnumMemberSymbol(root, TestUri.TestModuleUri(), "Colour", ScopeKind.Module, SymbolKindExt.Enum,
            VBUnknownType.TypeInfo, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public);

        Assert.AreEqual("0", IntrinsicVBTypes.DefaultLiteralOf(new VBEnumType(symbol)));
    }

    [TestMethod]
    public void AnUnknownType_HasNoDefaultLiteral()
        => Assert.IsNull(IntrinsicVBTypes.DefaultLiteralOf(VBUnknownType.TypeInfo));
}
