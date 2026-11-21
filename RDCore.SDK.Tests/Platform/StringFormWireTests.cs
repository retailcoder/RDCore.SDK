using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// The form of a library function that returns a String in place of a Variant (MS-VBAL §6.1.2) travels with the fact of an expression bound to the
/// function.
/// </summary>
[TestClass]
public sealed class StringFormWireTests
{
    private static readonly Uri Root = new("file:///c:/workspace/");

    private static ExpressionFact Fact()
        => new(new SyntaxNodeId("/Module1.bas", [2, 0]), new SourceLocation(Root, SourceRange.Empty), VBVariantType.TypeInfo,
            ExpressionClassification.Function, null, default);

    private static ExpressionFactDto RoundTrip(ExpressionFact fact)
        => PlatformJson.Deserialize<ExpressionFactDto>(PlatformJson.Serialize(ExpressionFactDto.From(fact)));

    [TestMethod]
    public void AFunctionWithAStringForm_TravelsWithItsName()
        => Assert.AreEqual("Left$", RoundTrip(Fact() with { StringForm = "Left$" }).StringForm);

    [TestMethod]
    public void AnExpressionWithNoStringForm_HasNone()
        => Assert.IsNull(RoundTrip(Fact()).StringForm);
}
