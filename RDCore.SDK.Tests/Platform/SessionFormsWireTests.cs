using RDCore.SDK.Platform.Components;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.Tests.Platform;

/// <summary>
/// What a debugger reads of the forms a program left shown, as the transport writes it: the result that says the program completed with one shown, the number
/// of forms the session has loaded, and the notification that it has none.
/// </summary>
[TestClass]
public sealed class SessionFormsWireTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void WhetherAFormIsShown_IsReadBackAsItWasWritten(bool shown)
        => Assert.AreEqual(shown, Wire.RoundTrip(new ExecuteResult { Outcome = ExecutionOutcome.Completed, FormsShown = shown }).FormsShown);

    [TestMethod]
    public void TheNumberOfFormsLoaded_IsReadBackAsItWasWritten()
        => Assert.AreEqual(3, Wire.RoundTrip(new SessionStatusResult { IsComposed = true, FormsLoaded = 3 }).FormsLoaded);

    [TestMethod]
    public void TheNotificationThatNoFormIsLoaded_IsReadBack()
        => Assert.IsNotNull(Wire.RoundTrip(new HostFormsUnloadedNotification()));
}
