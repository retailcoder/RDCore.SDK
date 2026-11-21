using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;
using RDCore.SDK.Semantics.Flow;

namespace RDCore.Tests.Platform;

/// <summary>
/// What the flow of a procedure tells of its variables travels with the procedure's model, from the environment host to an analyzer: the reads that a
/// variable's initial value may reach, and the assignments whose value nothing reads. Each part of each fact is read back as it was written.
/// </summary>
[TestClass]
public sealed class FlowFactWireTests
{
    private static readonly Uri Procedure = new UriBuilder("file://rdcore-test") { Fragment = "Module1.Work" }.Uri;
    private static readonly Uri Total = new UriBuilder("file://rdcore-test") { Fragment = "Module1.Work.total" }.Uri;
    private static readonly Uri Document = new("file://rdcore-test/Module1.bas");

    private static SourceLocation At(int line, int start, int end)
        => new(Document, new SourceRange(new SourcePosition(line, start), new SourcePosition(line, end)));

    private static ProcedureSemanticsDto RoundTrip(ProcedureSemanticModel procedure)
    {
        var model = new ModuleSemanticModel(Procedure, [], [procedure]);
        return PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single()
            .Procedures.Single();
    }

    [TestMethod]
    public void TheReadsTheInitialValueMayReach_AreReadBackAsTheyWereWritten()
    {
        var always = new InitialValueReadFact(Total, "total", At(4, 16, 21), IsOnlyValue: true);
        var sometimes = always with { Location = At(6, 16, 21), IsOnlyValue = false };

        var read = RoundTrip(new ProcedureSemanticModel(new SemanticId(Procedure), []) { InitialValueReads = [always, sometimes] }).InitialValueReads;

        Assert.IsNotNull(read);
        CollectionAssert.AreEqual(new[] { always, sometimes }, read.Value.ToArray());
        // Uri equality ignores the fragment, which is what names the variable.
        Assert.AreEqual(Total.AbsoluteUri, read.Value[0].Variable.AbsoluteUri);
    }

    [TestMethod]
    public void TheAssignmentsNothingReads_AreReadBackAsTheyWereWritten()
    {
        var assignment = new UnreadAssignmentFact(Total, "total", At(3, 4, 9));

        var read = RoundTrip(new ProcedureSemanticModel(new SemanticId(Procedure), []) { UnreadAssignments = [assignment] }).UnreadAssignments;

        Assert.IsNotNull(read);
        CollectionAssert.AreEqual(new[] { assignment }, read.Value.ToArray());
        Assert.AreEqual(Total.AbsoluteUri, read.Value[0].Variable.AbsoluteUri);
    }

    [TestMethod]
    public void TheJumpsIntoALoop_AreReadBackAsTheyWereWritten()
    {
        var goTo = new JumpIntoLoopFact(JumpStatementKind.GoTo, At(3, 4, 10));
        var resume = new JumpIntoLoopFact(JumpStatementKind.Resume, At(9, 4, 12));

        var read = RoundTrip(new ProcedureSemanticModel(new SemanticId(Procedure), []) { JumpsIntoLoops = [goTo, resume] }).JumpsIntoLoops;

        Assert.IsNotNull(read);
        CollectionAssert.AreEqual(new[] { goTo, resume }, read.Value.ToArray());
    }

    [TestMethod]
    public void AProcedureWhoseFlowStatesNothing_HasNoneOfThem()
    {
        var read = RoundTrip(new ProcedureSemanticModel(new SemanticId(Procedure), []));

        Assert.IsNull(read.InitialValueReads);
        Assert.IsNull(read.UnreadAssignments);
        Assert.IsNull(read.JumpsIntoLoops);
    }
}
