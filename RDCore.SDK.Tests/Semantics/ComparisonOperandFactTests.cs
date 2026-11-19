using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime;
using RDCore.SDK.Semantics.Facts;

namespace RDCore.Tests.Semantics;

/// <summary>
/// What a comparison's <c>String</c> operand is Let-coerced to, and the types a <c>Case</c> clause is compared in (MS-VBAL 5.6.9.5, 5.4.2.10), which their
/// facts carry over the wire.
/// </summary>
[TestClass]
public sealed class ComparisonOperandFactTests
{
    private static readonly Uri Root = new("file://rdcore-test");
    private static readonly SyntaxNodeId Node = new("/Module1.bas", [2, 0]);
    private static readonly SourceLocation Location = new(Root, SourceRange.Empty);

    private static OperatorFactDto RoundTrip(StringOperandFact? operand)
    {
        var fact = new ComparisonOperatorFact(Node, Location, Tokens.CompareEqualOp, VBDoubleType.TypeInfo, true, null, default, new StringComparisonRules())
        {
            StringOperand = operand,
        };
        return PlatformJson.Deserialize<OperatorFactDto>(PlatformJson.Serialize(OperatorFactDto.From(fact)));
    }

    [TestMethod]
    public void AStringComparedWithANumber_TravelsWithItsValueBothWays()
    {
        var operand = new StringOperandFact(true, "Integer", new VBDoubleValue(2.5), null, new VBIntegerValue(2), null, new VBIntegerValue(2));

        var read = RoundTrip(operand).StringOperand;

        Assert.IsNotNull(read);
        Assert.IsTrue(read.IsLeft);
        Assert.AreEqual("Integer", read.SpecifiedEffectiveType);
        Assert.AreEqual(new VBDoubleValue(2.5), read.AsDouble);
        Assert.AreEqual(new VBIntegerValue(2), read.AsSpecified);
        Assert.AreEqual(new VBIntegerValue(2), read.Number);
    }

    [TestMethod]
    public void AStringThatIsNoNumber_TravelsWithTheErrorOfEachCoercion()
    {
        var read = RoundTrip(new StringOperandFact(false, "Long", null, 13, null, 13, null)).StringOperand!;

        Assert.AreEqual((13, 13), (read.AsDoubleError, read.AsSpecifiedError));
        Assert.IsNull(read.AsDouble);
        Assert.IsNull(read.Number);
    }

    [TestMethod]
    public void AComparisonOfNoStringWithANumber_HasNoStringOperand()
        => Assert.IsNull(RoundTrip(null).StringOperand);

    [TestMethod]
    public void ACaseClause_TravelsWithTheTypesItIsComparedIn()
    {
        var fact = new CaseClauseStatementFact(Node, Location, Tokens.Case, default, true)
        {
            ComparedIn = VBIntegerType.TypeInfo,
            SpecifiedEffectiveType = VBStringType.TypeInfo,
        };

        var read = PlatformJson.Deserialize<StatementFactDto>(PlatformJson.Serialize(StatementFactDto.From(fact)));

        Assert.AreEqual(("Integer", "String"), (read.ComparedIn, read.SpecifiedEffectiveType));
    }
}
