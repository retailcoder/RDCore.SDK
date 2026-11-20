using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The Set-coercion of an expression's value to a class (MS-VBAL 5.5.2, 5.4.3.9, 5.3.1.11), which the expression's fact carries over the wire.
/// </summary>
[TestClass]
public sealed class SetCoercionFactTests
{
    private static readonly Uri Root = new("file://rdcore-test");

    private static ExpressionFactDto RoundTrip(ExpressionFact fact)
        => PlatformJson.Deserialize<ExpressionFactDto>(PlatformJson.Serialize(ExpressionFactDto.From(fact)));

    private static ExpressionFact Fact()
        => new(new SyntaxNodeId("/Module1.bas", [2, 0]), new SourceLocation(Root, SourceRange.Empty), VBObjectType.TypeInfo,
            ExpressionClassification.Variable, null, default);

    [TestMethod]
    [DataRow(ClassRelation.Same)]
    [DataRow(ClassRelation.Implements)]
    [DataRow(ClassRelation.ImplementedBy)]
    [DataRow(ClassRelation.SharedImplementer)]
    [DataRow(ClassRelation.Unrelated)]
    public void AValueSetToAClass_TravelsWithTheClass_AndHowItsOwnRelatesToIt(ClassRelation relation)
    {
        var coercion = new SetCoercionFact("IShape", relation);

        Assert.AreEqual(coercion, RoundTrip(Fact() with { SetCoercion = coercion }).SetCoercion);
    }

    [TestMethod]
    public void AnExpressionThatIsNotSetToAClass_HasNoSetCoercion()
        => Assert.IsNull(RoundTrip(Fact()).SetCoercion);
}
