using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics.Flags;
using System.Collections.Immutable;

namespace RDCore.Tests.Semantics.Flags;

/// <summary>
/// The facts of an <c>LSet</c> between two records (<strong>MS-VBAL §5.4.3.6</strong>) reach a diagnostics extension as the host recorded them: each member
/// kind MS-VBA refuses the statement over, on either side, crosses the wire as its own flag, alone or with the others.
/// </summary>
[TestClass]
public sealed class FixedAssignmentSemanticFlagsTests
{
    private const FixedAssignmentSemanticFlags Copy = FixedAssignmentSemanticFlags.UserDefinedTypeCopy;

    private static StatementFactDto Fact(FixedAssignmentSemanticFlags flags)
        => new(new SyntaxNodeId("file:///c:/ws/Module1.bas", [0, 3]), new SourceLocation(new Uri("file:///c:/ws/Module1.bas"), new SourceRange(4, 4, 4, 13)),
            "LSet", flags, WithStatementFlags: null);

    [TestMethod]
    [DataRow(Copy | FixedAssignmentSemanticFlags.SourceHoldsObject)]
    [DataRow(Copy | FixedAssignmentSemanticFlags.DestinationHoldsObject)]
    [DataRow(Copy | FixedAssignmentSemanticFlags.SourceHoldsVariant)]
    [DataRow(Copy | FixedAssignmentSemanticFlags.DestinationHoldsVariant)]
    [DataRow(Copy | FixedAssignmentSemanticFlags.SourceHoldsVariableLengthString | FixedAssignmentSemanticFlags.DestinationHoldsVariableLengthString
        | FixedAssignmentSemanticFlags.SourceHoldsObject | FixedAssignmentSemanticFlags.DestinationHoldsObject
        | FixedAssignmentSemanticFlags.SourceHoldsVariant | FixedAssignmentSemanticFlags.DestinationHoldsVariant)]
    public void TheMembersOfACopy_CrossTheWire_AsTheyWereRecorded(FixedAssignmentSemanticFlags flags)
    {
        var read = PlatformJson.Deserialize<StatementFactDto>(PlatformJson.Serialize(Fact(flags)));

        Assert.AreEqual(flags, read.FixedAssignmentFlags);
        Assert.IsNull(read.WithStatementFlags);
    }

    // a flag of what one record holds says nothing of the other: the source's and the destination's are bits of their own, for each member kind.
    [TestMethod]
    public void EachMemberKind_HasASourceAndADestinationFlag_OfTheirOwn()
    {
        FixedAssignmentSemanticFlags[] members =
        [
            FixedAssignmentSemanticFlags.SourceHoldsVariableLengthString, FixedAssignmentSemanticFlags.DestinationHoldsVariableLengthString,
            FixedAssignmentSemanticFlags.SourceHoldsObject, FixedAssignmentSemanticFlags.DestinationHoldsObject,
            FixedAssignmentSemanticFlags.SourceHoldsVariant, FixedAssignmentSemanticFlags.DestinationHoldsVariant,
        ];

        Assert.HasCount(members.Length, members.Distinct());
        Assert.AreEqual(FixedAssignmentSemanticFlags.None, members.Aggregate(FixedAssignmentSemanticFlags.None, (all, member) => all | member)
            & (FixedAssignmentSemanticFlags.Failed | FixedAssignmentSemanticFlags.StringTarget | Copy));
    }
}
