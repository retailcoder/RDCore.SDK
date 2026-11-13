using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Server;
using RDCore.SDK.Client;
using RDCore.SDK.Client.Connection;
using RDCore.SDK.Platform;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Runtime.Abstract.Execution;
using RDCore.SDK.Semantics.Refactoring;
using RDCore.SDK.Server.Serialization;
using System.IO.Pipelines;
using System.Text.Json;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace RDCore.Tests.Client;

/// <summary>
/// The client logic of the Protocol package (<see cref="RDCorePlatformClient"/>) over OmniSharp's language client (<see cref="OmniSharpPlatformChannel"/>), against a
/// server that serves the platform's methods as the language server does: with OmniSharp, its Newtonsoft serializer, and the SDK's request wrappers.
/// </summary>
[TestClass]
public sealed class RDCorePlatformClientWireTests
{
    // strings Newtonsoft would read as dates and write back otherwise: in the offset of the machine, and without the fraction.
    private const string WithAnOffset = "2026-10-30T12:34:56+02:00";

    private const string WithAFraction = "2026-10-30T12:34:56.000Z";

    private sealed record class Connected(OmniSharpLanguageServer Server, LanguageClient Client, RDCorePlatformClient Platform) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
        }
    }

    private static async Task<Connected> ConnectAsync(Action<OmniSharp.Extensions.LanguageServer.Server.LanguageServerOptions>? serve = null)
    {
        var toServer = new Pipe();
        var toClient = new Pipe();
        // each end with the serializer the platform gives it.
        var serving = OmniSharpLanguageServer.From(server =>
        {
            server.WithInput(toServer.Reader).WithOutput(toClient.Writer).WithSerializer(new PullDiagnosticsLspSerializer());
            serve?.Invoke(server);
        });
        var connecting = LanguageClient.From(client => client.WithInput(toClient.Reader).WithOutput(toServer.Writer));

        await Task.WhenAll(serving, connecting);
        var languageClient = await connecting;
        return new Connected(await serving, languageClient, new RDCorePlatformClient(new OmniSharpPlatformChannel(languageClient)));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TheHandshake_IsAnsweredByTheServersHandler()
    {
        JObject? received = null;
        using var connected = await ConnectAsync(server => server.OnJsonRequest(RDCorePlatformProtocol.PlatformInitialize, (JToken request, CancellationToken _) =>
        {
            received = (JObject)request;
            return Task.FromResult<JToken>(JObject.Parse("""{ "component": 1, "provided": ["SessionExecute"] }"""));
        }));

        var result = await connected.Platform.InitializeAsync(
            CoreServerComponent.LanguageServer, new CorePlatformClientCapabilities { LanguageServer = new() { SessionExecute = new SessionExecute(true) } }, CancellationToken.None);

        Assert.AreEqual(CoreServerComponent.LanguageServer, result.Component);
        CollectionAssert.AreEqual(new[] { nameof(SessionExecute) }, result.Provided.ToArray());
        Assert.IsNotNull(received);
        Assert.AreEqual((int)CoreServerComponent.LanguageServer, received["expectedComponent"]!.Value<int>());
        Assert.IsTrue(received["expected"]!["languageServer"]!["sessionExecute"]!["isSupported"]!.Value<bool>());
        var expected = (JObject)received["expected"]!;
        Assert.IsFalse(expected.ContainsKey("parsing"), "a group the client expects nothing of is not sent, not even as null");
        Assert.IsFalse(expected.ContainsKey("environmentHost"));
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ASessionRequest_ReachesTheServer_AndItsResultComesBackAsTheServerWroteIt()
    {
        string? source = null;
        using var connected = await ConnectAsync(server => server.OnRequest<SessionExecuteRequest, ExecuteResult>(RDCorePlatformProtocol.SessionExecute, (request, _) =>
        {
            source = request.Source;
            return Task.FromResult(new ExecuteResult
            {
                Outcome = ExecutionOutcome.Suspended,
                Output = ["1", "2026-10-30T12:34:56"],
                StackTrace = [new ExecuteStackFrame(request.EntryPoint, 3, 4, [7])],
            });
        }));

        var result = await connected.Platform.ExecuteAsync(new SessionExecuteParams { Source = "Debug.Print 1", ModuleName = "Module1", EntryPoint = "Main", Debug = true }, CancellationToken.None);

        Assert.AreEqual("Debug.Print 1", source);
        Assert.AreEqual(ExecutionOutcome.Suspended, result.Outcome);
        CollectionAssert.AreEqual(new[] { "1", "2026-10-30T12:34:56" }, result.Output.ToArray(), "a date as VBA's Format writes one is read as it was written");
        Assert.AreEqual("Main", result.StackTrace.Single().Procedure);
        CollectionAssert.AreEqual(new[] { 7 }, result.StackTrace.Single().ReturnLines.ToArray());
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ADebugRequest_ReachesTheServer_WithItsEnumeration()
    {
        StepKind? received = null;
        using var connected = await ConnectAsync(server => server.OnRequest<SessionDebugResumeRequest, ExecuteResult>(RDCorePlatformProtocol.SessionDebugResume, (request, _) =>
        {
            received = request.Step;
            return Task.FromResult(new ExecuteResult { Outcome = ExecutionOutcome.Completed });
        }));

        var result = await connected.Platform.DebugResumeAsync(new SessionDebugResumeParams { Step = StepKind.Out }, CancellationToken.None);

        Assert.AreEqual(StepKind.Out, received);
        Assert.AreEqual(ExecutionOutcome.Completed, result.Outcome);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TheServersMessageBox_IsShownByTheClientsHandler_UntilItIsDisposed()
    {
        using var connected = await ConnectAsync();
        SessionMessageBoxParams? shown = null;
        var registration = connected.Platform.OnMessageBox((request, _) =>
        {
            shown = request;
            return Task.FromResult(new MessageBoxResult { Handled = true, Result = 6 });
        });

        var answer = await connected.Server.SendRequest(new SessionMessageBoxRequest { Prompt = "Continue?", Title = "VBAProject", Style = 4, Output = ["before"] }, CancellationToken.None);

        Assert.IsTrue(answer.Handled);
        Assert.AreEqual(6, answer.Result);
        Assert.AreEqual("Continue?", shown?.Prompt);
        Assert.AreEqual(4, shown?.Style);
        CollectionAssert.AreEqual(new[] { "before" }, shown?.Output.ToArray());

        registration.Dispose();
        await Assert.ThrowsAsync<Exception>(
            () => connected.Server.SendRequest(new SessionMessageBoxRequest { Prompt = "Again?" }, CancellationToken.None),
            "a client that stopped answering is not asked any more");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TheServersInputBox_IsAnsweredByTheClientsHandler()
    {
        using var connected = await ConnectAsync();
        using var registration = connected.Platform.OnInputBox((request, _) => Task.FromResult(new InputBoxResult { Handled = true, Value = request.Default + "!" }));

        var answer = await connected.Server.SendRequest(new SessionInputBoxRequest { Prompt = "Name?", Default = "Rick", XPos = 10 }, CancellationToken.None);

        Assert.IsTrue(answer.Handled);
        Assert.IsFalse(answer.Cancelled);
        Assert.AreEqual("Rick!", answer.Value);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TheServersStop_ReachesTheClientsHandler_AsTheServerWroteIt()
    {
        using var connected = await ConnectAsync();
        var told = new TaskCompletionSource<SessionDebugStoppedParams>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = connected.Platform.OnDebugStopped((stop, _) =>
        {
            told.TrySetResult(stop);
            return Task.CompletedTask;
        });

        connected.Server.SendNotification(new SessionDebugStoppedNotification
        {
            Stop = new ExecuteResult { Outcome = ExecutionOutcome.Suspended, Output = ["clicked"], StackTrace = [new ExecuteStackFrame("Command1_Click", 4, 4) { Module = "Form1" }] },
        });
        var stop = (await told.Task.WaitAsync(TimeSpan.FromSeconds(30))).Stop;

        Assert.AreEqual(ExecutionOutcome.Suspended, stop.Outcome);
        CollectionAssert.AreEqual(new[] { "clicked" }, stop.Output.ToArray());
        Assert.AreEqual(("Command1_Click", "Form1", 4), (stop.StackTrace.Single().Procedure, stop.StackTrace.Single().Module, stop.StackTrace.Single().Line));
    }

    // what C:\Dev\Claude\ticket-omnisharp-input-dates.md is for: the strings are written as they are, and read otherwise.
    private const string InputDates = "OmniSharp's InputHandler parses ISO date-times with its own JsonTextReader, before any serializer";

    [TestMethod]
    [Ignore(InputDates)]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TheLanguageServer_ReadsAStringThatLooksLikeADate_AsTheClientWroteIt()
    {
        var sources = new List<string>();
        using var connected = await ConnectAsync(server => server.OnRequest<SessionExecuteRequest, ExecuteResult>(RDCorePlatformProtocol.SessionExecute, (request, _) =>
        {
            sources.Add(request.Source);
            return Task.FromResult(new ExecuteResult());
        }));

        await connected.Platform.ExecuteAsync(new SessionExecuteParams { Source = WithAnOffset }, CancellationToken.None);
        await connected.Platform.ExecuteAsync(new SessionExecuteParams { Source = WithAFraction }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { WithAnOffset, WithAFraction }, sources, "an offset and a fraction of a second are read as written");
    }

    [TestMethod]
    [Ignore(InputDates)]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TheClient_ReadsAStringThatLooksLikeADate_AsTheLanguageServerWroteIt()
    {
        string? prompt = null;
        using var connected = await ConnectAsync(server => server.OnRequest<SessionExecuteRequest, ExecuteResult>(RDCorePlatformProtocol.SessionExecute, (_, _) =>
            Task.FromResult(new ExecuteResult { Output = [WithAnOffset, WithAFraction] })));
        using var registration = connected.Platform.OnInputBox((request, _) =>
        {
            prompt = request.Prompt;
            return Task.FromResult(InputBoxResult.NotHandled);
        });

        var result = await connected.Platform.ExecuteAsync(new SessionExecuteParams(), CancellationToken.None);
        await connected.Server.SendRequest(new SessionInputBoxRequest { Prompt = WithAnOffset }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { WithAnOffset, WithAFraction }, result.Output.ToArray(), "a result is read as written");
        Assert.AreEqual(WithAnOffset, prompt, "and a request the server sends");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ASecondHandlerOfAMethod_IsRefused_UntilTheFirstIsDisposed()
    {
        using var connected = await ConnectAsync();
        var first = connected.Platform.OnMessageBox((_, _) => Task.FromResult(new MessageBoxResult { Handled = true, Result = 1 }));

        var refused = Assert.ThrowsExactly<InvalidOperationException>(() => connected.Platform.OnMessageBox((_, _) => Task.FromResult(MessageBoxResult.NotHandled)));
        StringAssert.Contains(refused.Message, RDCorePlatformProtocol.SessionMessageBox);
        Assert.AreEqual(1, (await connected.Server.SendRequest(new SessionMessageBoxRequest(), CancellationToken.None)).Result, "the first still answers");

        first.Dispose();
        first.Dispose();
        using var second = connected.Platform.OnMessageBox((_, _) => Task.FromResult(new MessageBoxResult { Handled = true, Result = 2 }));
        Assert.AreEqual(2, (await connected.Server.SendRequest(new SessionMessageBoxRequest(), CancellationToken.None)).Result, "the second answers once the first is gone");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ARequestWhoseParamsAreNotWhatTheMethodTakes_IsAnsweredInvalidParams()
    {
        using var connected = await ConnectAsync();
        var asked = false;
        using var registration = connected.Platform.OnMessageBox((_, _) =>
        {
            asked = true;
            return Task.FromResult(MessageBoxResult.NotHandled);
        });

        // OmniSharp tells InvalidParams (-32602) by an exception of its own.
        await Assert.ThrowsExactlyAsync<OmniSharp.Extensions.JsonRpc.Server.InvalidParametersException>(
            () => connected.Server.SendRequest(RDCorePlatformProtocol.SessionMessageBox, JObject.Parse("""{ "style": "not a number" }""")).Returning<JToken>(CancellationToken.None));

        Assert.IsFalse(asked, "the handler is not given what it cannot read");
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task TheIndenterSettingsRequests_AreAnsweredByTheClientsHandlers_AsTheLanguageServerReadsThem()
    {
        using var connected = await ConnectAsync();
        RubberduckIndenterSettingsApplyParams? applied = null;
        using var reading = connected.Platform.OnRubberduckIndenterSettings((_, _) => Task.FromResult(new RubberduckIndenterSettingsResult
        {
            Source = RubberduckIndenterSettingsSource.SmartIndenterRegistry,
            Values = new Dictionary<string, JsonElement> { ["IndentProc"] = JsonSerializer.SerializeToElement("Y"), ["AlignDimCol"] = JsonSerializer.SerializeToElement(15) },
            TabWidth = 4,
        }));
        using var applying = connected.Platform.OnRubberduckIndenterSettingsApply((request, _) =>
        {
            applied = request;
            return Task.FromResult(new RubberduckIndenterSettingsApplyResult { Applied = request.Settings.Count });
        });

        var read = await connected.Server.SendRequest(new RubberduckIndenterSettingsRequest(), CancellationToken.None);
        var answer = await connected.Server.SendRequest(
            new RubberduckIndenterSettingsApplyRequest { Source = RubberduckIndenterSettingsSource.SmartIndenterRegistry, Settings = new() { ["Formatting.IndentSpaces"] = "4" } },
            CancellationToken.None);

        Assert.AreEqual(RubberduckIndenterSettingsSource.SmartIndenterRegistry, read.Source);
        Assert.AreEqual("Y", read.Values["IndentProc"].GetString());
        Assert.AreEqual(15, read.Values["AlignDimCol"].GetInt32());
        Assert.AreEqual(4, read.TabWidth);
        Assert.AreEqual(RubberduckIndenterSettingsSource.SmartIndenterRegistry, applied?.Source);
        Assert.AreEqual("4", applied?.Settings["Formatting.IndentSpaces"]);
        Assert.AreEqual(1, answer.Applied);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task AListSettingValueRequest_IsAnsweredByTheClientsHandler_AsTheLanguageServerReadsIt()
    {
        using var connected = await ConnectAsync();
        ListSettingValueParams? asked = null;
        using var changing = connected.Platform.OnListSettingValue((request, _) =>
        {
            asked = request;
            return Task.FromResult(new ListSettingValueResult { Applied = true });
        });

        var answer = await connected.Server.SendRequest(
            new ListSettingValueRequest { Key = "Analysis.DisabledDiagnostics", Value = "RDC01001", Scope = SettingScope.Workspace, Document = new Uri("file:///c:/ws/Module1.bas") },
            CancellationToken.None);

        Assert.IsTrue(answer.Applied);
        Assert.AreEqual("Analysis.DisabledDiagnostics", asked?.Key);
        Assert.AreEqual("RDC01001", asked?.Value);
        Assert.AreEqual(SettingScope.Workspace, asked?.Scope);
        Assert.IsFalse(asked!.Remove);
        Assert.AreEqual(new Uri("file:///c:/ws/Module1.bas"), asked.Document);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ARefactorPrepare_ReachesTheServersHandler_AndItsChoicesNoticesAndRangeComeBackAsTheServerWroteThem()
    {
        var module = new Uri("file:///c:/workspace/Module1.bas");
        RefactorPrepareRequest? received = null;
        using var connected = await ConnectAsync(server => server.OnRequest<RefactorPrepareRequest, RefactorPrepareResult>(RDCorePlatformProtocol.RefactorPrepare, (request, _) =>
        {
            received = request;
            return Task.FromResult(new RefactorPrepareResult
            {
                Choices =
                [
                    new RefactorChoice { Id = "name", Kind = RefactorChoiceKind.Name, Label = "Name", Default = "Total" },
                    new RefactorChoice
                    {
                        Id = "parameters", Kind = RefactorChoiceKind.List, Label = "Parameters", CanReorder = true,
                        Items = [new RefactorItem { Id = "x", Label = "ByRef x As Long", Reason = "written here", Choices = [new RefactorChoice { Id = "byRef", Kind = RefactorChoiceKind.Flag, Default = "true" }] }],
                    },
                ],
                Notices =
                [
                    new RefactorNotice { Code = "onErrorGoTo", Kind = RefactorNoticeKind.Change, Message = "m", Location = new SourceLocation(request.Uri!, request.Range) },
                    new RefactorNotice { Code = "erl", Kind = RefactorNoticeKind.Note, Message = "n" },
                ],
                Range = new SourceRange(1, 0, 3, 12),
            });
        }));

        var result = await connected.Platform.RefactorPrepareAsync(
            new RefactorPrepareParams { Kind = RefactoringKinds.ExtractMethod, Uri = module, Range = new SourceRange(1, 0, 3, 7) }, CancellationToken.None);

        Assert.AreEqual(RefactoringKinds.ExtractMethod, received?.Kind);
        Assert.AreEqual(module, received?.Uri);
        Assert.AreEqual(new SourceRange(1, 0, 3, 7), received?.Range);
        Assert.AreEqual(new SourceRange(1, 0, 3, 12), result.Range);
        Assert.AreEqual("Total", result.Choices[0].Default);
        Assert.AreEqual(RefactorChoiceKind.List, result.Choices[1].Kind);
        Assert.IsTrue(result.Choices[1].CanReorder);
        Assert.AreEqual("written here", result.Choices[1].Items.Single().Reason);
        Assert.AreEqual(RefactorChoiceKind.Flag, result.Choices[1].Items.Single().Choices.Single().Kind);
        Assert.AreEqual(RefactorNoticeKind.Change, result.Notices[0].Kind);
        Assert.AreEqual(new SourceLocation(module, new SourceRange(1, 0, 3, 7)), result.Notices[0].Location);
        Assert.AreEqual(RefactorNoticeKind.Note, result.Notices[1].Kind);
        Assert.IsNull(result.Notices[1].Location);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ARefactorApply_ReachesTheServersHandler_WithTheAnswers_AndTheEditComesBackAsTheServerWroteIt()
    {
        var module = new Uri("file:///c:/workspace/Module1.bas");
        RefactorApplyRequest? received = null;
        using var connected = await ConnectAsync(server => server.OnRequest<RefactorApplyRequest, RefactorWorkspaceEdit>(RDCorePlatformProtocol.RefactorApply, (request, _) =>
        {
            received = request;
            return Task.FromResult(new RefactorWorkspaceEdit
            {
                Documents = [new RefactorDocumentEdit { Uri = request.Uri, Version = 2, Edits = [new RefactorTextEdit { Range = request.Range, NewText = "Total x" }] }],
            });
        }));

        var edit = await connected.Platform.RefactorApplyAsync(new RefactorApplyParams
        {
            Kind = RefactoringKinds.ExtractMethod, Uri = module, Range = new SourceRange(1, 0, 3, 7),
            Answers = [new RefactorAnswer { Id = "parameters", Items = [new RefactorItemAnswer { Id = "x", Answers = [new RefactorAnswer { Id = "byRef", Value = "false" }] }] }],
            Accepted = ["onErrorGoTo"],
        }, CancellationToken.None);

        Assert.AreEqual("false", received?.Answers.Single().Items.Single().Answers.Single().Value);
        CollectionAssert.AreEqual(new[] { "onErrorGoTo" }, received?.Accepted.ToArray());
        var document = edit.Documents.Single();
        Assert.AreEqual(module, document.Uri);
        Assert.AreEqual(2, document.Version);
        Assert.AreEqual(new SourceRange(1, 0, 3, 7), document.Edits.Single().Range);
        Assert.AreEqual("Total x", document.Edits.Single().NewText);
    }

    [TestMethod]
    [Timeout(60_000, CooperativeCancellation = true)]
    public async Task ARefactorIdentifierCheck_ReachesTheServersHandler_WithTheAnswers_AndTheNoticesComeBackWithTheChoiceEachAnswers()
    {
        var module = new Uri("file:///c:/workspace/Module1.bas");
        RefactorCheckIdentifierRequest? received = null;
        using var connected = await ConnectAsync(server => server.OnRequest<RefactorCheckIdentifierRequest, RefactorIdentifierCheckResult>(
            RDCorePlatformProtocol.RefactorCheckIdentifier, (request, _) =>
        {
            received = request;
            return Task.FromResult(new RefactorIdentifierCheckResult
            {
                Notices =
                [
                    new RefactorNotice { Code = "shadows", Kind = RefactorNoticeKind.Note, Message = "n", Related = new SourceLocation(request.Uri!, request.Range), Choice = "name" },
                    new RefactorNotice { Code = "reserved", Kind = RefactorNoticeKind.Refusal, Message = "r", Choice = "name", Item = "x" },
                ],
            });
        }));

        var result = await connected.Platform.RefactorCheckIdentifierAsync(new RefactorIdentifierCheckParams
        {
            Kind = RefactoringKinds.ExtractMethod, Uri = module, Range = new SourceRange(1, 0, 3, 7),
            Answers = [new RefactorAnswer { Id = "name", Value = "Sum" }],
        }, CancellationToken.None);

        Assert.AreEqual(RefactoringKinds.ExtractMethod, received?.Kind);
        Assert.AreEqual(new SourceRange(1, 0, 3, 7), received?.Range);
        Assert.AreEqual("Sum", received?.Answers.Single().Value);
        Assert.AreEqual(RefactorNoticeKind.Note, result.Notices[0].Kind);
        Assert.AreEqual("name", result.Notices[0].Choice);
        Assert.IsNull(result.Notices[0].Item);
        Assert.AreEqual(new SourceLocation(module, new SourceRange(1, 0, 3, 7)), result.Notices[0].Related);
        Assert.AreEqual(RefactorNoticeKind.Refusal, result.Notices[1].Kind);
        Assert.AreEqual("x", result.Notices[1].Item);
    }
}
