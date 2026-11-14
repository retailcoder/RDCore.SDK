using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The accessors of the property an expression names, the type its Property Let is given, and the default member of the class an expression is of
/// (MS-VBAL 5.3.1.7, 5.6.2.3), which the expression's fact carries over the wire.
/// </summary>
[TestClass]
public sealed class MemberFactTests
{
    private static readonly Uri Root = new("file://rdcore-test");

    private static ExpressionFactDto RoundTrip(ExpressionFact fact)
        => PlatformJson.Deserialize<ExpressionFactDto>(PlatformJson.Serialize(ExpressionFactDto.From(fact)));

    private static ExpressionFact Fact(ExpressionClassification classification)
        => new(new SyntaxNodeId("/Module1.bas", [2, 0]), new SourceLocation(Root, SourceRange.Empty), VBVariantType.TypeInfo, classification, null, default);

    [TestMethod]
    public void AProperty_TravelsWithItsAccessors_AndTheTypeItsLetIsGiven()
    {
        var read = RoundTrip(Fact(ExpressionClassification.Property) with
        {
            Accessors = PropertyAccessors.Get | PropertyAccessors.Let,
            LetValueType = VBObjectType.TypeInfo,
        });

        Assert.AreEqual(PropertyAccessors.Get | PropertyAccessors.Let, read.Accessors);
        Assert.AreEqual("Object", read.LetValueType);
        Assert.AreEqual(DeclaredTypeKind.Object, read.LetValueTypeKind);
    }

    [TestMethod]
    public void AnExpressionOfAClass_TravelsWithItsDefaultMember()
    {
        var member = new DefaultMemberFact("Value", ExpressionClassification.Property, PropertyAccessors.Get, 0);

        Assert.AreEqual(member, RoundTrip(Fact(ExpressionClassification.Variable) with { DefaultMember = member }).DefaultMember);
    }

    [TestMethod]
    public void AVariable_HasNoAccessors_NorADefaultMember()
    {
        var read = RoundTrip(Fact(ExpressionClassification.Variable));

        Assert.AreEqual(PropertyAccessors.None, read.Accessors);
        Assert.IsNull(read.LetValueType);
        Assert.IsNull(read.LetValueTypeKind);
        Assert.IsNull(read.DefaultMember);
    }
}
