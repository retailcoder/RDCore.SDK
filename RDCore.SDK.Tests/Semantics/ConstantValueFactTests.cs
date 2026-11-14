using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The constant binding of the constant a name expression names (MS-VBAL §5.2.3.2), which its fact carries over the wire: an analyzer reads what a constant
/// is without the constant's declaration, which may be in another module.
/// </summary>
[TestClass]
public sealed class ConstantValueFactTests
{
    private static readonly Uri Root = new("file://rdcore-test");

    public static IEnumerable<object?[]> Values() =>
    [
        [new VBLongValue(1), "Long"],
        [new VBBooleanValue(true), "Boolean"],
        [new VBDoubleValue(-1.5), "Double"],
        [new VBStringValue("DEV"), "String"],
    ];

    private static ExpressionFactDto RoundTrip(VBTypedValue? value, string declaredType)
    {
        var fact = new ExpressionFact(new SyntaxNodeId("/Module1.bas", [2, 0]), new SourceLocation(Root, SourceRange.Empty), VBLongType.TypeInfo,
            ExpressionClassification.Constant, null, default) { ConstantValue = value };
        var dto = ExpressionFactDto.From(fact) with { DeclaredType = declaredType };
        return PlatformJson.Deserialize<ExpressionFactDto>(PlatformJson.Serialize(dto));
    }

    [TestMethod]
    [DynamicData(nameof(Values))]
    public void TheValueOfAConstant_TravelsWithItsNamesFact(VBTypedValue value, string declaredType)
    {
        var read = RoundTrip(value, declaredType);

        Assert.AreEqual(value, read.ConstantValue);
    }

    [TestMethod]
    public void AnExpressionThatNamesNoConstant_HasNoConstantValue()
        => Assert.IsNull(RoundTrip(null, "Long").ConstantValue);
}
