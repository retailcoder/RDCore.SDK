using System.Collections.Immutable;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime;
using RDCore.SDK.Semantics;
using RDCore.SDK.Semantics.Facts;
using RDCore.SDK.Semantics.Flags;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The default members an access of a default member the code does not name goes through (MS-VBAL §5.6.13, §5.6.14), which its fact carries over the wire,
/// in the order they are called, each with what it is and the arguments it requires.
/// </summary>
[TestClass]
public sealed class DefaultMemberChainFactTests
{
    private static readonly Uri Root = new("file://rdcore-test");
    private static readonly SyntaxNodeId Node = new("/Module1.bas", [2, 0]);
    private static readonly SourceLocation Location = new(Root, SourceRange.Empty);

    private const MemberAccessOperationSemanticFlags Recursive =
        MemberAccessOperationSemanticFlags.Implicit | MemberAccessOperationSemanticFlags.DefaultMember | MemberAccessOperationSemanticFlags.Recursive;

    private static OperatorFactDto RoundTrip(ImmutableArray<DefaultMemberFact>? chain)
    {
        var fact = new MemberAccessFact(Node, Location, Tokens.MemberAccess, null, true, null, chain?.FirstOrDefault()?.Name, Recursive)
        {
            DefaultMembers = chain,
        };
        return PlatformJson.Deserialize<OperatorFactDto>(PlatformJson.Serialize(OperatorFactDto.From(fact)));
    }

    [TestMethod]
    public void TheDefaultMembersAnAccessGoesThrough_TravelInTheOrderTheyAreCalled()
    {
        var inner = new DefaultMemberFact("Inner", ExpressionClassification.Property, PropertyAccessors.Get, 0);
        var child = new DefaultMemberFact("Child", ExpressionClassification.Function, PropertyAccessors.None, 0);
        var item = new DefaultMemberFact("Item", ExpressionClassification.Property, PropertyAccessors.Get | PropertyAccessors.Let, 1);

        var read = RoundTrip([inner, child, item]).DefaultMembers;

        Assert.IsNotNull(read);
        CollectionAssert.AreEqual(
            new[] { inner, child, item }, read.Value.ToArray(), "each with its name, what it is, its accessors and its required arguments");
    }

    [TestMethod]
    public void AnAccessWhoseChainIsNotKnown_HasNoDefaultMembers()
        => Assert.IsNull(RoundTrip(null).DefaultMembers);

    [TestMethod]
    public void AnOperationThatIsNoMemberAccess_HasNoDefaultMembers()
    {
        var fact = new ArithmeticOperatorFact(Node, Location, Tokens.AdditionOp, null, true, null, default);

        Assert.IsNull(OperatorFactDto.From(fact).DefaultMembers);
    }
}
