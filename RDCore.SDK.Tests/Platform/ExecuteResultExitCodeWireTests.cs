using RDCore.SDK.Platform.Protocol;

namespace RDCore.Tests.Platform;

/// <summary>
/// The code a program says its process ends with travels with the result of the run, from the environment host through the language server to the client.
/// </summary>
[TestClass]
public sealed class ExecuteResultExitCodeWireTests
{
    [TestMethod]
    [DataRow(7, DisplayName = "a code the program said")]
    [DataRow(-1, DisplayName = "a negative code")]
    public void TheExitCodeOfAProgramThatEnded_IsReadBackAsItWasWritten(int exitCode)
        => Assert.AreEqual(exitCode, Wire.RoundTrip(new ExecuteResult { Outcome = ExecutionOutcome.Halted, ExitCode = exitCode }).ExitCode);

    [TestMethod]
    public void AResultThatSaysNoExitCode_IsReadBackWith0()
        => Assert.AreEqual(0, Wire.RoundTrip(new ExecuteResult { Outcome = ExecutionOutcome.Completed }).ExitCode);
}
