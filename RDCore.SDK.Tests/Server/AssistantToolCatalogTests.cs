using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using RDCore.SDK.Assistant;
using RDCore.SDK.Server.Assistant;
using System.Globalization;
using System.Text.Json.Nodes;

namespace RDCore.Tests.Server;

/// <summary>
/// The catalog lists its tools in the client's language and calls them by name: a refusal is a result the assistant reads, an unknown name is a
/// JSON-RPC error, and the text of a result is cut at its cap. A server whose catalog has a tool declares its tools in its <c>initialize</c> result.
/// </summary>
[TestClass]
public sealed class AssistantToolCatalogTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-CA");

    private static IAssistantTool Tool(string name, ToolCallResult? answer = null)
    {
        var tool = Substitute.For<IAssistantTool>();
        tool.Name.Returns(name);
        tool.Describe(Arg.Any<CultureInfo>()).Returns(call => new ToolDescriptor { Name = name, Description = $"{name} ({call.Arg<CultureInfo>().Name})" });
        tool.CallAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).Returns(answer ?? ToolCallResult.Of("done"));
        return tool;
    }

    [TestMethod]
    public void List_DescribesEveryTool_InOrder_InTheClientsLanguage()
    {
        var catalog = new AssistantToolCatalog([Tool("get_diagnostics"), Tool("references")]);

        var listed = catalog.List(French);

        CollectionAssert.AreEqual(new[] { "get_diagnostics", "references" }, listed.Tools.Select(tool => tool.Name).ToArray());
        Assert.AreEqual("references (fr-CA)", listed.Tools[1].Description);
    }

    [TestMethod]
    public void TwoToolsOfOneName_AreRefused()
        => Assert.ThrowsExactly<ArgumentException>(() => new AssistantToolCatalog([Tool("references"), Tool("references")]));

    [TestMethod]
    [DataRow("")]
    [DataRow("find symbol")]
    [DataRow("trouvé")]
    [DataRow("a0123456789012345678901234567890123456789012345678901234567890123")]
    public void AToolNamedOtherwiseThanMcpNamesATool_IsRefused(string name)
        => Assert.ThrowsExactly<ArgumentException>(() => new AssistantToolCatalog([Tool(name)]));

    [TestMethod]
    public async Task ACallOfAToolTheCatalogDoesNotHave_IsAnInvalidParamsError()
    {
        var catalog = new AssistantToolCatalog([Tool("references")]);

        var error = await Assert.ThrowsExactlyAsync<RpcErrorException>(() => catalog.CallAsync(new ToolCallParams { Name = "rename" }, CancellationToken.None));

        Assert.AreEqual(AssistantToolCatalog.UnknownToolCode, error.Code);
        Assert.Contains("rename", error.Message);
    }

    [TestMethod]
    public async Task ACall_GivesTheToolItsArguments_AndAnEmptyObjectForNone()
    {
        var tool = Tool("references");
        var catalog = new AssistantToolCatalog([tool]);

        await catalog.CallAsync(new ToolCallParams { Name = "references", Arguments = new JsonObject { ["path"] = "Module1.bas" } }, CancellationToken.None);
        await catalog.CallAsync(new ToolCallParams { Name = "references" }, CancellationToken.None);

        await tool.Received(1).CallAsync(Arg.Is<JsonObject>(arguments => (string?)arguments["path"] == "Module1.bas"), Arg.Any<CancellationToken>());
        await tool.Received(1).CallAsync(Arg.Is<JsonObject>(arguments => arguments.Count == 0), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task AToolThatRefusesTheCall_IsAnsweredWithARefusal_ThatSaysWhy()
    {
        var tool = Tool("apply_fix");
        tool.CallAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).ThrowsAsync(new AssistantToolRefusedException("the document changed"));
        var catalog = new AssistantToolCatalog([tool]);

        var result = await catalog.CallAsync(new ToolCallParams { Name = "apply_fix" }, CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.IsNull(result.StructuredContent);
        Assert.AreEqual("the document changed", result.Content.Single().Text);
    }

    [TestMethod]
    public async Task AServiceThatRefusesWithAJsonRpcError_IsAnsweredWithARefusal()
    {
        var tool = Tool("rename");
        tool.CallAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).ThrowsAsync(new RpcErrorException(-32803, null!, "'x' is not a name"));
        var catalog = new AssistantToolCatalog([tool]);

        var result = await catalog.CallAsync(new ToolCallParams { Name = "rename" }, CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual("'x' is not a name", result.Content.Single().Text);
    }

    [TestMethod]
    public async Task ACancelledCall_IsCancelled_NotRefused()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var tool = Tool("get_diagnostics");
        tool.CallAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException(cancelled.Token));
        var catalog = new AssistantToolCatalog([tool]);

        await Assert.ThrowsAsync<OperationCanceledException>(() => catalog.CallAsync(new ToolCallParams { Name = "get_diagnostics" }, cancelled.Token));
    }

    [TestMethod]
    public async Task TheTextOfAResult_IsCutAtItsCap_AndTheStructuredResultIsKept()
    {
        var text = string.Join("\n", Enumerable.Range(1, 5_000).Select(line => $"Module1.bas:{line}:1 RDC01011 Warning line {line}"));
        var structured = new JsonObject { ["total"] = 5_000 };
        var catalog = new AssistantToolCatalog([Tool("get_diagnostics", ToolCallResult.Of(text, structured))]);

        var result = await catalog.CallAsync(new ToolCallParams { Name = "get_diagnostics" }, CancellationToken.None);

        var lines = result.Content.Single().Text.Split('\n');
        var whole = text.Split('\n').ToHashSet();
        Assert.IsLessThanOrEqualTo(ToolResultLimits.TextLength, lines[..^1].Sum(line => line.Length + 1));
        Assert.IsTrue(lines[..^1].All(whole.Contains), "cut at the end of a whole line");
        Assert.DoesNotContain(lines[^1], whole, "the last line says where the text is cut");
        Assert.AreSame(structured, result.StructuredContent);
    }

    [TestMethod]
    public void Cap_KeepsTheFirstItems_AndCountsThemAll()
    {
        var capped = ToolResultLimits.Cap(Enumerable.Range(1, 7), 5);

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, capped.Items.ToArray());
        Assert.AreEqual(7, capped.Total);
        Assert.IsTrue(capped.Truncated);
        Assert.IsFalse(ToolResultLimits.Cap(Enumerable.Range(1, 5), 5).Truncated);
    }

    [TestMethod]
    public void TheLineOfACutList_SaysHowManyAreShown_InTheClientsLanguage()
    {
        var capped = ToolResultLimits.Cap(Enumerable.Range(1, 7), 5);

        Assert.AreEqual("Showing 5 of 7. Narrow the call (a path, a query) to see the others.", ToolResultLimits.TruncatedLine(capped, English));
        Assert.StartsWith("5 sur 7 sont affichés.", ToolResultLimits.TruncatedLine(capped, French)!);
        Assert.IsNull(ToolResultLimits.TruncatedLine(ToolResultLimits.Cap(Enumerable.Range(1, 5), 5), English));
    }

    [TestMethod]
    public void AShortText_IsNotCut()
        => Assert.AreEqual("Module1.bas:1:1 RDC01011", ToolResultLimits.CapText("Module1.bas:1:1 RDC01011", English));

    [TestMethod]
    public void AText_CutAtItsCap_SaysWhereItIsCut()
    {
        var text = new string('x', ToolResultLimits.TextLength + 10);

        var cut = ToolResultLimits.CapText(text, English);

        Assert.StartsWith(new string('x', ToolResultLimits.TextLength), cut);
        Assert.EndsWith($"The text is cut at {ToolResultLimits.TextLength} of {text.Length} characters. Narrow the call (a path, a query) to see the rest.", cut);
    }

    [TestMethod]
    public void AServerWithTools_DeclaresThemAsHexIDEsChatReadsThem_WithTheirMethodsAndNoContext()
    {
        var capabilities = new ServerCapabilities();

        var declared = AssistantToolsCapability.Declare(capabilities, new AssistantToolCatalog([Tool("references")]));

        Assert.IsTrue(declared);
        var written = JsonNode.Parse(new LspSerializer().SerializeObject(new InitializeResult { Capabilities = capabilities }))!["capabilities"]!["experimental"]!;
        Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse("""{"hexide.chatTools":{"list":"rdcore/tools/list","call":"rdcore/tools/call"}}"""), written), written.ToJsonString());
    }

    [TestMethod]
    public void AServerWithNoTools_DeclaresNone()
    {
        foreach (var catalog in new[] { null, new AssistantToolCatalog([]) })
        {
            var capabilities = new ServerCapabilities();

            Assert.IsFalse(AssistantToolsCapability.Declare(capabilities, catalog));
            Assert.IsFalse(capabilities.Experimental?.ContainsKey(RDCore.SDK.Client.RDCorePlatformProtocol.ChatToolsCapability) == true);
        }
    }
}
