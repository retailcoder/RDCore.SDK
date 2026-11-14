using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;
using System.Collections.Immutable;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The kind of a declared type (MS-VBAL §2.1), which an expression's fact carries over the wire: an analyzer tells an object of a class from a record of a
/// user-defined type by it, without the type's declaration.
/// </summary>
[TestClass]
public sealed class DeclaredTypeKindsTests
{
    private static readonly Uri Root = new("file://rdcore-test");

    private static VBClassType Widget() => new(new VBClassModuleSymbol(Root, Root, "Widget"), []);

    private static VBUserDefinedType Point()
    {
        var uri = new Uri("file://rdcore-test#Point");
        return new VBUserDefinedType(new VBUserDefinedTypeMemberSymbol(uri, uri, "Point", ScopeKind.Module, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public), []);
    }

    private static VBEnumType Colour() => new(new VBEnumMemberSymbol(Root, Root, "Colour", ScopeKind.Module, SymbolKindExt.Enum, VBUnknownType.TypeInfo,
        SourceRange.Empty, SourceRange.Empty, AccessModifier.Public));

    public static IEnumerable<object?[]> Types() =>
    [
        [VBLongType.TypeInfo, DeclaredTypeKind.Intrinsic, false],
        [VBStringType.TypeInfo, DeclaredTypeKind.Intrinsic, false],
        [VBVariantType.TypeInfo, DeclaredTypeKind.Variant, false],
        [VBEmptyType.TypeInfo, DeclaredTypeKind.Variant, false],
        [VBObjectType.TypeInfo, DeclaredTypeKind.Object, false],
        [Widget(), DeclaredTypeKind.Class, false],
        [Point(), DeclaredTypeKind.UserDefinedType, false],
        [Colour(), DeclaredTypeKind.Enum, false],
        [new VBResizableArrayType(Widget()), DeclaredTypeKind.Class, true],
        [new VBFixedSizeArrayType(VBLongType.TypeInfo), DeclaredTypeKind.Intrinsic, true],
        [VBUnknownType.TypeInfo, null, false],
        [null, null, false],
    ];

    [TestMethod]
    [DynamicData(nameof(Types))]
    public void TheKindOfADeclaredType_IsWhatAValueOfItIs(VBType? type, DeclaredTypeKind? kind, bool isArray)
        => Assert.AreEqual((kind, isArray), DeclaredTypeKinds.Of(type));

    [TestMethod]
    public void AnExpressionsFact_TravelsWithTheKindOfItsDeclaredType()
    {
        var fact = new ExpressionFact(new SyntaxNodeId("/Module1.bas", [2, 0]), new SourceLocation(Root, SourceRange.Empty), new VBResizableArrayType(Point()),
            ExpressionClassification.Variable, null, default);

        var dto = ExpressionFactDto.From(fact);

        Assert.AreEqual(DeclaredTypeKind.UserDefinedType, dto.TypeKind);
        Assert.IsTrue(dto.IsArray);
        var read = PlatformJson.Deserialize<ExpressionFactDto>(PlatformJson.Serialize(dto));
        Assert.AreEqual(DeclaredTypeKind.UserDefinedType, read.TypeKind);
        Assert.IsTrue(read.IsArray);
    }
}
