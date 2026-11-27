using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using RDCore.SDK.Client;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Server.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmniSharpSerializer = OmniSharp.Extensions.JsonRpc.ISerializer;

namespace RDCore.Tests.Platform;

/// <summary>
/// An invocation's result holds the procedure's value as JSON, which the platform writes through <c>System.Text.Json</c>
/// (<see cref="ProgramInvocationReply"/>): a client with the Protocol package alone and the platform's Newtonsoft serializers write and read it the same, the
/// value as the JSON it holds.
/// </summary>
[TestClass]
public sealed class ProgramInvocationWireTests
{
    private static IEnumerable<OmniSharpSerializer> PlatformSerializers() => [new LspSerializer(), new PullDiagnosticsLspSerializer()];

    // what a Scripting.Dictionary of a name, a count and a list is as JSON.
    private static ProgramInvocationResult Returned()
        => ProgramInvocationResult.Returned(new JsonObject { ["name"] = "Duck", ["count"] = 42, ["lines"] = new JsonArray(1, "two", null) });

    private const string ReturnedJson
        = """{"outcome":0,"hasValue":true,"value":{"name":"Duck","count":42,"lines":[1,"two",null]},"errorNumber":0,"errorMessage":"","errorCode":""}""";

    private const string NullJson = """{"outcome":0,"hasValue":true,"value":null,"errorNumber":0,"errorMessage":"","errorCode":""}""";

    private const string RuntimeErrorJson
        = """{"outcome":3,"hasValue":false,"value":null,"errorNumber":11,"errorMessage":"Division by zero","errorCode":"VBR00011"}""";

    private static IEnumerable<(ProgramInvocationResult Result, string Json)> Results()
    {
        yield return (Returned(), ReturnedJson);
        yield return (ProgramInvocationResult.Returned(null), NullJson);
        yield return (ProgramInvocationResult.RuntimeError(11, "Division by zero", "VBR00011"), RuntimeErrorJson);
    }

    private static void AssertSameJson(string expected, string actual, string what)
        => Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"{what}\nexpected: {expected}\nactual:   {actual}");

    [TestMethod]
    public void TheReply_IsWrittenByThePlatform_AsTheProtocolOptionsWriteTheResult()
    {
        foreach (var serializer in PlatformSerializers())
        {
            foreach (var (result, json) in Results())
            {
                AssertSameJson(json, JsonSerializer.Serialize(result, RDCorePlatformJson.Options), "the Protocol options");
                AssertSameJson(json, serializer.SerializeObject(new ProgramInvocationReply(result)), serializer.GetType().Name);
            }
        }
    }

    [TestMethod]
    public void WhatAClientWrites_ThePlatformReads_TheValueAsTheJsonItHolds()
    {
        foreach (var serializer in PlatformSerializers())
        {
            var read = (ProgramInvocationReply?)serializer.DeserializeObject(ReturnedJson, typeof(ProgramInvocationReply));

            Assert.IsNotNull(read, serializer.GetType().Name);
            Assert.AreEqual(ExecutionOutcome.Completed, read.Outcome);
            Assert.IsTrue(read.HasValue);
            Assert.IsTrue(JsonNode.DeepEquals(Returned().Value, read.Value), $"{serializer.GetType().Name}: {read.Value?.ToJsonString()}");
        }
    }
}
