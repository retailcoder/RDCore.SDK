using System.Collections.Immutable;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime;
using RDCore.SDK.Semantics.Facts;
using RDCore.SDK.Semantics.Flags;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The members a late-bound member access may resolve to when it runs (MS-VBAL 5.6.3), which its fact carries over the wire: none known, none
/// accessible, or each one with its access modifier.
/// </summary>
[TestClass]
public sealed class LateBoundCandidateFactTests
{
    private static readonly Uri Root = new("file://rdcore-test");
    private static readonly SyntaxNodeId Node = new("/Module1.bas", [2, 0]);
    private static readonly SourceLocation Location = new(Root, SourceRange.Empty);

    private static OperatorFactDto RoundTrip(ImmutableArray<LateBoundCandidate>? candidates)
    {
        var fact = new MemberAccessFact(Node, Location, Tokens.MemberAccess, null, true, null, null, MemberAccessOperationSemanticFlags.LateBound)
        {
            LateBoundCandidates = candidates,
        };
        return PlatformJson.Deserialize<OperatorFactDto>(PlatformJson.Serialize(OperatorFactDto.From(fact)));
    }

    [TestMethod]
    public void TheMembersALateBoundAccessMayReach_TravelWithTheirAccessModifiers()
    {
        var friend = new LateBoundCandidate(new Uri(Root, "/Crew.cls#Badge"), AccessModifier.Friend);
        var shown = new LateBoundCandidate(new Uri(Root, "/Squad.cls#Badge"), AccessModifier.Public);

        var read = RoundTrip([friend, shown]).LateBoundCandidates;

        Assert.IsNotNull(read);
        CollectionAssert.AreEqual(new[] { friend, shown }, read.Value.ToArray());
    }

    [TestMethod]
    public void AnAccessWhoseClassesAreKnown_AndReachNoMember_HasAnEmptyArray()
    {
        var read = RoundTrip([]).LateBoundCandidates;

        Assert.IsNotNull(read);
        Assert.IsEmpty(read.Value);
    }

    [TestMethod]
    public void AnAccessWhoseClassesAreNotKnown_HasNoCandidates()
        => Assert.IsNull(RoundTrip(null).LateBoundCandidates);

    [TestMethod]
    public void AnOperationThatIsNoMemberAccess_HasNoCandidates()
    {
        var fact = new ArithmeticOperatorFact(Node, Location, Tokens.AdditionOp, null, true, null, default);

        Assert.IsNull(OperatorFactDto.From(fact).LateBoundCandidates);
    }
}
