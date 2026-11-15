using RDCore.SDK.Platform.Components;
using RDCore.SDK.Server;

namespace RDCore.Tests.Platform;

/// <summary>
/// What a debugger asks of a program while it runs is handled as it arrives (<see cref="HandledOnArrivalAttribute"/>): a request left in order would wait for the
/// request that runs the program, which is done when the program is.
/// </summary>
[TestClass]
public sealed class HostDebugHandledOnArrivalTests
{
    [TestMethod]
    [DataRow(typeof(HostDebugPauseParams))]
    [DataRow(typeof(HostDebugTerminateParams))]
    [DataRow(typeof(HostDebugErrorBreakParams))]
    [DataRow(typeof(HostDebugBreakpointsParams))]
    public void ARequestThatActsOnARunningProgram_IsHandledOnArrival(Type request)
        => Assert.IsTrue(Attribute.IsDefined(request, typeof(HandledOnArrivalAttribute)), request.Name);

    [TestMethod]
    public void TheRequestThatRunsTheProgram_IsHandledInOrder()
        => Assert.IsFalse(Attribute.IsDefined(typeof(HostDebugResumeParams), typeof(HandledOnArrivalAttribute)));
}
