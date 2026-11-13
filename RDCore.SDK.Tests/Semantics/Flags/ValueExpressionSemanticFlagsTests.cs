using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;
using RDCore.SDK.Semantics.Flags;

namespace RDCore.Tests.Semantics.Flags;

/// <summary>
/// The target of a <c>Let</c> that gives an object to a <c>Variant</c> property (<strong>MS-VBAL §5.4.3.8</strong>) reaches a diagnostics extension as the
/// host recorded it, with the other flags of the target.
/// </summary>
[TestClass]
public sealed class ValueExpressionSemanticFlagsTests
{
    private static ExpressionFactDto Fact(ValueExpressionSemanticFlags flags)
        => new(new SyntaxNodeId("file:///c:/ws/Module1.bas", [0, 2]), new SourceLocation(new Uri("file:///c:/ws/Module1.bas"), new SourceRange(3, 4, 3, 10)),
            "Variant", ExpressionClassification.Property, new Uri("file:///c:/ws/Class1.cls#Item"), flags, Error: null);

    [TestMethod]
    [DataRow(ValueExpressionSemanticFlags.ObjectGivenToVariantProperty)]
    [DataRow(ValueExpressionSemanticFlags.ObjectGivenToVariantProperty | ValueExpressionSemanticFlags.AssignmentTarget)]
    [DataRow(ValueExpressionSemanticFlags.ObjectGivenToVariantProperty | ValueExpressionSemanticFlags.AssignmentTarget
        | ValueExpressionSemanticFlags.ProcedureCall | ValueExpressionSemanticFlags.TrueValueIsNotMinusOne)]
    public void TheTargetOfTheLet_CrossesTheWire_AsItWasRecorded(ValueExpressionSemanticFlags flags)
    {
        var read = PlatformJson.Deserialize<ExpressionFactDto>(PlatformJson.Serialize(Fact(flags)));

        Assert.AreEqual(flags, read.Flags);
    }
}
