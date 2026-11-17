using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using RDCore.SDK.Assistant;
using RDCore.SDK.Client;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Server.Assistant;
using RDCore.SDK.Server.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmniSharpSerializer = OmniSharp.Extensions.JsonRpc.ISerializer;

namespace RDCore.Tests.Platform;

/// <summary>
/// The <c>rdcore/tools/*</c> messages hold JSON (schemas, arguments, structured results), which the platform writes through <c>System.Text.Json</c>: a client
/// with the Protocol package alone and the platform's Newtonsoft serializers write and read them the same, with the shape the Model Context Protocol gives
/// them. The tools' own arguments and results travel inside them as JSON, written with the Protocol options.
/// </summary>
[TestClass]
public sealed class AssistantToolsWireTests
{
    private static IEnumerable<OmniSharpSerializer> PlatformSerializers() => [new LspSerializer(), new PullDiagnosticsLspSerializer()];

    private static ToolsListResult Listed() => new()
    {
        Tools =
        [
            new ToolDescriptor
            {
                Name = CoreAssistantTools.References,
                Title = "References",
                Description = "The names that refer to the symbol at a position. Lines and columns are 1-based.",
                InputSchema = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["path"] = new JsonObject { ["type"] = "string" } }, ["required"] = new JsonArray("path") },
                OutputSchema = new JsonObject { ["type"] = "object" },
                Annotations = new ToolAnnotations { ReadOnlyHint = true, IdempotentHint = true },
            },
        ],
    };

    private const string ListedJson = """{"tools":[{"name":"references","title":"References","description":"The names that refer to the symbol at a position. Lines and columns are 1-based.","inputSchema":{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]},"outputSchema":{"type":"object"},"annotations":{"readOnlyHint":true,"destructiveHint":false,"idempotentHint":true,"openWorldHint":false}}]}""";

    private static ToolCallParams Call() => new() { Name = CoreAssistantTools.References, Arguments = new JsonObject { ["path"] = "Module1.bas", ["line"] = 12, ["column"] = 5 } };

    private const string CallJson = """{"name":"references","arguments":{"path":"Module1.bas","line":12,"column":5}}""";

    private static ToolCallResult Answered() => ToolCallResult.Of("Module1.bas:12:5", new JsonObject { ["total"] = 1, ["truncated"] = false });

    private const string AnsweredJson = """{"content":[{"type":"text","text":"Module1.bas:12:5"}],"structuredContent":{"total":1,"truncated":false},"isError":false}""";

    private const string RefusedJson = """{"content":[{"type":"text","text":"the document changed"}],"isError":true}""";

    private static IEnumerable<(object Message, object OnTheWire, string Json)> Messages()
    {
        yield return (new ToolsListParams(), new ToolsListRequest(), "{}");
        yield return (Listed(), new ToolsListReply(Listed()), ListedJson);
        yield return (Call(), new ToolCallRequest(Call()), CallJson);
        yield return (Answered(), new ToolCallReply(Answered()), AnsweredJson);
        yield return (ToolCallResult.Refusal("the document changed"), new ToolCallReply(ToolCallResult.Refusal("the document changed")), RefusedJson);
    }

    private static void AssertSameJson(string expected, string actual, string what)
        => Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"{what}\nexpected: {expected}\nactual:   {actual}");

    [TestMethod]
    public void EveryMessage_IsWrittenAsItsGoldenJsonSays_ByTheProtocolOptionsAndByThePlatform()
    {
        foreach (var (message, onTheWire, json) in Messages())
        {
            AssertSameJson(json, JsonSerializer.Serialize(message, message.GetType(), RDCorePlatformJson.Options), message.GetType().Name);
            foreach (var serializer in PlatformSerializers())
            {
                AssertSameJson(json, serializer.SerializeObject(onTheWire), $"{onTheWire.GetType().Name}, {serializer.GetType().Name}");
            }
        }
    }

    [TestMethod]
    public void WhatTheProtocolOptionsWrite_ThePlatformReads_AsTheSameMessage()
    {
        foreach (var serializer in PlatformSerializers())
        {
            foreach (var (message, onTheWire, json) in Messages())
            {
                var read = serializer.DeserializeObject(JsonSerializer.Serialize(message, message.GetType(), RDCorePlatformJson.Options), onTheWire.GetType());

                Assert.IsInstanceOfType(read, onTheWire.GetType());
                AssertSameJson(json, serializer.SerializeObject(read), $"{onTheWire.GetType().Name}, {serializer.GetType().Name}");
            }
        }
    }

    [TestMethod]
    public void AToolThatRunsCode_SaysSoAsHexIDEsChatReadsIt_AndBackOverEveryWire()
    {
        var listed = new ToolsListResult { Tools = [Listed().Tools[0] with { Annotations = new ToolAnnotations { RunsCodeHint = true } }] };
        const string annotations = """{"readOnlyHint":false,"destructiveHint":false,"idempotentHint":false,"openWorldHint":false,"hexide.runsCode":true}""";

        var written = JsonSerializer.Serialize(listed, RDCorePlatformJson.Options);
        AssertSameJson(annotations, JsonNode.Parse(written)!["tools"]![0]!["annotations"]!.ToJsonString(), "Protocol options");
        Assert.IsTrue(JsonSerializer.Deserialize<ToolsListResult>(written, RDCorePlatformJson.Options)!.Tools[0].Annotations.RunsCodeHint, "Protocol options, read back");
        foreach (var serializer in PlatformSerializers())
        {
            var onTheWire = serializer.SerializeObject(new ToolsListReply(listed));
            AssertSameJson(annotations, JsonNode.Parse(onTheWire)!["tools"]![0]!["annotations"]!.ToJsonString(), serializer.GetType().Name);
            var read = (ToolsListReply)serializer.DeserializeObject(onTheWire, typeof(ToolsListReply))!;
            Assert.IsTrue(read.Tools[0].Annotations.RunsCodeHint, $"{serializer.GetType().Name}, read back");
        }
    }

    [TestMethod]
    public void AToolThatRunsNoCode_LeavesTheKeyOut()
    {
        var written = JsonNode.Parse(JsonSerializer.Serialize(Listed(), RDCorePlatformJson.Options))!["tools"]![0]!["annotations"]!.AsObject();

        Assert.IsFalse(written.ContainsKey("hexide.runsCode"), written.ToJsonString());
        Assert.IsFalse(JsonSerializer.Deserialize<ToolsListResult>(ListedJson, RDCorePlatformJson.Options)!.Tools[0].Annotations.RunsCodeHint);
    }

    [TestMethod]
    public void ACall_RoundTripsThePlatformsWire_WithItsArguments()
    {
        var read = Wire.RoundTrip(new ToolCallRequest(Call()));

        Assert.AreEqual(CoreAssistantTools.References, read.Name);
        Assert.AreEqual(12, (int)read.Arguments!["line"]!);
        Assert.AreEqual("Module1.bas", (string?)read.Arguments["path"]);
    }

    private static TextLocation At(int line, int column) => new("src/Module1.bas", new TextRange(new(line, column), new(line, column + 4)));

    /// <summary>
    /// The arguments and results of the core tools, as the Protocol options write them: what an assistant reads, and the schemas describe.
    /// </summary>
    private static IEnumerable<(object Record, string Json)> ToolRecords()
    {
        yield return (new DiagnosticsToolArguments { Path = "src/Module1.bas" }, """{"path":"src/Module1.bas"}""");
        yield return (new FindSymbolToolArguments { Query = "gtnm" }, """{"query":"gtnm"}""");
        yield return (new PositionToolArguments { Path = "src/Module1.bas", Line = 12, Column = 5 }, """{"path":"src/Module1.bas","line":12,"column":5}""");
        yield return (new ReferencesToolArguments { Path = "src/Module1.bas", Line = 12, Column = 5, IncludeDeclaration = false },
            """{"includeDeclaration":false,"path":"src/Module1.bas","line":12,"column":5}""");
        yield return (new DiagnosticsToolResult
        {
            Diagnostics = [new ToolDiagnostic { Location = At(3, 9), Code = "RDC01011", Severity = ToolDiagnosticSeverity.Warning, Message = "m", HelpUrl = "https://example.org/rdc01011", HasFix = true }],
            Total = 201,
            Truncated = true,
        }, """{"diagnostics":[{"location":{"path":"src/Module1.bas","range":{"start":{"line":3,"column":9},"end":{"line":3,"column":13}}},"code":"RDC01011","severity":"Warning","message":"m","helpUrl":"https://example.org/rdc01011","hasFix":true}],"total":201,"truncated":true}""");
        yield return (new SymbolsToolResult { Symbols = [new ToolSymbol { Name = "GetName", Kind = "Function", Container = "Module1", Location = At(2, 17) }], Total = 1 },
            """{"symbols":[{"name":"GetName","kind":"Function","container":"Module1","location":{"path":"src/Module1.bas","range":{"start":{"line":2,"column":17},"end":{"line":2,"column":21}}}}],"total":1,"truncated":false}""");
        yield return (new LocationsToolResult { Locations = [At(4, 5)], Total = 1, Incomplete = "a late-bound member may refer to it" },
            """{"locations":[{"path":"src/Module1.bas","range":{"start":{"line":4,"column":5},"end":{"line":4,"column":9}}}],"total":1,"truncated":false,"incomplete":"a late-bound member may refer to it"}""");
    }

    [TestMethod]
    public void EveryToolRecord_IsWrittenAsItsGoldenJsonSays_AndReadBack()
    {
        foreach (var (record, json) in ToolRecords())
        {
            var written = JsonSerializer.Serialize(record, record.GetType(), RDCorePlatformJson.Options);
            AssertSameJson(json, written, record.GetType().Name);
            var read = JsonSerializer.Deserialize(written, record.GetType(), RDCorePlatformJson.Options);
            AssertSameJson(json, JsonSerializer.Serialize(read, record.GetType(), RDCorePlatformJson.Options), record.GetType().Name + ", read back");
        }
    }

    [TestMethod]
    public void APosition_IsOneBased_AndConvertsToTheLspPosition()
    {
        Assert.AreEqual(new TextPosition(1, 1), TextPosition.Of(SourcePosition.Zero));
        Assert.AreEqual(new SourcePosition(11, 4), new TextPosition(12, 5).ToSourcePosition());
        Assert.AreEqual(new SourceRange(0, 0, 0, 4), new TextRange(new(1, 1), new(1, 5)).ToSourceRange());
        Assert.AreEqual("src/Module1.bas:3:9", At(3, 9).ToString());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TextPosition(0, 1).ToSourcePosition());
    }

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-tools", "workspace");

    [TestMethod]
    public void APathInTheWorkspace_IsSaidFromItsRoot_WithSlashes()
    {
        var document = new Uri(Path.Combine(Root, "src", "Module1.bas"));

        Assert.AreEqual("src/Module1.bas", TextLocation.PathOf(Root, document));
        Assert.AreEqual(new TextLocation("src/Module1.bas", new TextRange(new(1, 1), new(1, 5))), TextLocation.Of(Root, document, new SourceRange(0, 0, 0, 4)));
    }

    [TestMethod]
    public void APathOutsideTheWorkspace_IsSaidAbsolute()
    {
        var outside = Path.Combine(Path.GetTempPath(), "rdcore-tools", "elsewhere", "Module2.bas");

        Assert.AreEqual(outside, TextLocation.PathOf(Root, new Uri(outside)));
    }

    [TestMethod]
    public void APathArgument_NamesTheDocument_RelativeToTheRootOrAbsolute()
    {
        var document = new Uri(Path.Combine(Root, "src", "Module1.bas"));

        Assert.AreEqual(document, TextLocation.UriOf(Root, "src/Module1.bas"));
        Assert.AreEqual(document, TextLocation.UriOf(Root, document.LocalPath));
        Assert.ThrowsExactly<ArgumentException>(() => TextLocation.UriOf(Root, " "));
    }

    [TestMethod]
    public void TheInputSchemaOfAToolsArguments_SaysWhatIsRequired_AndDescribesEachArgument()
    {
        var schema = AssistantToolSchemas.Of(typeof(ReferencesToolArguments), argument => argument == "line" ? "The line, from 1." : null);

        Assert.AreEqual("object", (string?)schema["type"]);
        CollectionAssert.AreEquivalent(new[] { "path", "line", "column" }, schema["required"]!.AsArray().Select(name => (string?)name).ToArray());
        var properties = schema["properties"]!.AsObject();
        CollectionAssert.AreEquivalent(new[] { "path", "line", "column", "includeDeclaration" }, properties.Select(property => property.Key).ToArray());
        Assert.AreEqual("The line, from 1.", (string?)properties["line"]!["description"]);
        Assert.IsNull(properties["path"]!["description"]);
    }

    [TestMethod]
    public void TheOutputSchemaOfAResult_NamesTheSeverities()
    {
        var schema = AssistantToolSchemas.Of(typeof(DiagnosticsToolResult), _ => null);

        Assert.AreEqual("object", (string?)schema["type"]);
        var severity = schema["properties"]!["diagnostics"]!["items"]!["properties"]!["severity"]!;
        CollectionAssert.AreEqual(new[] { "Error", "Warning", "Information", "Hint" }, severity["enum"]!.AsArray().Select(name => (string?)name).ToArray());
    }
}
