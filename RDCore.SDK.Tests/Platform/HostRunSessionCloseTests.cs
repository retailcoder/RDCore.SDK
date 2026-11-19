using MediatR;
using RDCore.SDK.Client;
using RDCore.SDK.Platform.Components;
using System.Text.Json;

namespace RDCore.Tests.Platform;

/// <summary>
/// <c>rdcore/host/runSession/close</c> answers <see cref="HostRunSessionCloseResult"/>, which says whether the request closed an open session.
/// </summary>
[TestClass]
public sealed class HostRunSessionCloseTests
{
    [TestMethod]
    public void TheCloseRequest_AnswersItsOwnResult()
        => Assert.IsInstanceOfType<IRequest<HostRunSessionCloseResult>>(new HostRunSessionCloseParams { SessionId = "run-1" });

    [TestMethod]
    [DataRow(true, """{"closed":true}""")]
    [DataRow(false, """{"closed":false}""")]
    public void TheCloseResult_CrossesTheWireAsWritten(bool closed, string json)
    {
        var written = JsonSerializer.Serialize(new HostRunSessionCloseResult { Closed = closed }, RDCorePlatformJson.Options);

        Assert.AreEqual(json, written);
        Assert.AreEqual(closed, JsonSerializer.Deserialize<HostRunSessionCloseResult>(written, RDCorePlatformJson.Options)!.Closed);
    }
}
