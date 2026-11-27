using RDCore.SDK.Model.Errors;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Runtime.Abstract.Execution;

namespace RDCore.Tests.Runtime;

[TestClass]
[TestCategory("RD-VBAL §2.5 Runtime Values")]
public sealed class ExternalCallDecisionTests
{
    [TestMethod]
    public void Allow_IsNeitherBlockedNorAnswered()
    {
        var decision = ExternalCallDecision.Allow;

        Assert.IsFalse(decision.IsBlocked);
        Assert.IsFalse(decision.IsAnswered);
    }

    [TestMethod]
    public void Block_IsBlocked_AndNotAnswered()
    {
        var decision = ExternalCallDecision.Block("refused");

        Assert.IsTrue(decision.IsBlocked);
        Assert.IsFalse(decision.IsAnswered);
        Assert.AreEqual("refused", decision.Reason);
    }

    [TestMethod]
    public void Block_IsNotHalted()
        => Assert.IsFalse(ExternalCallDecision.Block("refused").IsHalted);

    [TestMethod]
    public void Halt_IsBlockedAndHalted_AndNotAnswered()
    {
        var decision = ExternalCallDecision.Halt("refused");

        Assert.IsTrue(decision.IsBlocked);
        Assert.IsTrue(decision.IsHalted);
        Assert.IsFalse(decision.IsAnswered);
        Assert.AreEqual("refused", decision.Reason);
    }

    [TestMethod]
    public void Answered_IsAnswered_AndNotBlocked()
    {
        var answer = ExternalCallAnswer.Returning(new VBLongValue(6));

        var decision = ExternalCallDecision.Answered(answer);

        Assert.IsTrue(decision.IsAnswered);
        Assert.IsFalse(decision.IsBlocked);
        Assert.AreSame(answer, decision.Answer);
    }

    [TestMethod]
    public void AnAnswer_GivenNoAssignments_WritesNone()
    {
        var answer = ExternalCallAnswer.Returning(null);

        Assert.IsFalse(answer.Assignments.IsDefault);
        Assert.IsEmpty(answer.Assignments);
    }

    [TestMethod]
    public void AnAnswerThatRaises_ReturnsNothing()
    {
        var error = new VBRuntimeErrorInfo(5, default, "Invalid procedure call or argument", string.Empty);

        var answer = ExternalCallAnswer.Raising(error);

        Assert.IsNull(answer.Result);
        Assert.AreSame(error, answer.Error);
    }

    [TestMethod]
    public void AnAnswer_KeepsItsAssignmentsInOrder()
    {
        var answer = ExternalCallAnswer.Returning(null) with
        {
            Assignments = [new(2, new VBLongValue(1)), new(1, new VBLongValue(2))],
        };

        CollectionAssert.AreEqual(new[] { 2, 1 }, answer.Assignments.Select(assignment => assignment.ArgumentIndex).ToArray());
    }
}
