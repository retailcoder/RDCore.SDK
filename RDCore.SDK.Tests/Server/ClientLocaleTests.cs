using Newtonsoft.Json.Linq;
using RDCore.SDK.Server;
using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharpLanguageServer = OmniSharp.Extensions.LanguageServer.Server.LanguageServer;

namespace RDCore.Tests.Server;

/// <summary>
/// A server speaks the language of the user interface its client says it shows (<c>InitializeParams.locale</c>): on the thread that is told, and on every
/// thread it begins work on later.
/// </summary>
/// <remarks>
/// These change the process's default culture, which every test that runs at the same time would read: they run on their own.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class ClientLocaleTests
{
    private CultureInfo? _defaultUICulture;
    private CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private CultureInfo _culture = CultureInfo.CurrentCulture;

    [TestInitialize]
    public void Remember()
    {
        _defaultUICulture = CultureInfo.DefaultThreadCurrentUICulture;
        _uiCulture = CultureInfo.CurrentUICulture;
        _culture = CultureInfo.CurrentCulture;
    }

    [TestCleanup]
    public void Restore()
    {
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUICulture;
        CultureInfo.CurrentUICulture = _uiCulture;
        CultureInfo.CurrentCulture = _culture;
    }

    [TestMethod]
    public void TheClientsLocale_IsTheLanguageOfTheUserInterface_AndOnlyThat()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var applied = ClientLocale.Apply("fr-CA");

        Assert.AreEqual("fr-CA", applied?.Name);
        Assert.AreEqual("fr-CA", CultureInfo.CurrentUICulture.Name);
        Assert.AreEqual("fr-CA", CultureInfo.DefaultThreadCurrentUICulture?.Name);
        Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture, "how numbers and dates are written is not the client's");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("not-a-culture-at-all-xx")]
    public void NoLocale_OrOneThePlatformDoesNotKnow_LeavesTheLanguageAsItIs(string? locale)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        Assert.IsNull(ClientLocale.Apply(locale));
        Assert.AreEqual("en-US", CultureInfo.CurrentUICulture.Name);
    }

    // ---- what a client of the protocol says ----

    // OmniSharp 0.19.9 reads an initialize request into a type of its own that has no locale, and makes the request its handlers are given of that one: what the
    // client said is kept from the message as it came.
    [TestMethod]
    [DataRow("fr-CA", "fr-CA", DisplayName = "a locale")]
    [DataRow(null, null, DisplayName = "none")]
    public async Task TheLocaleOfTheInitializeRequest_IsTheOneTheServerIsToldTheClientSaid(string? sent, string? said)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var toServer = new Pipe();
        var toClient = new Pipe();
        var told = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serving = OmniSharpLanguageServer.From(options => options
            .WithInput(toServer.Reader)
            .WithOutput(toClient.Writer)
            .WithLifecycleReceiver()
            .OnInitialize((server, request, token) =>
            {
                _ = told.TrySetResult(ClientLocale.Said(server, request));
                return Task.CompletedTask;
            }), timeout.Token);

        var parameters = new JObject { ["processId"] = null, ["rootUri"] = null, ["capabilities"] = new JObject() };
        if (sent is not null)
        {
            parameters["locale"] = sent;
        }

        await SendAsync(toServer.Writer, new JObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize", ["params"] = parameters }, timeout.Token);
        Assert.AreEqual(said, await told.Task.WaitAsync(timeout.Token));

        await SendAsync(toServer.Writer, new JObject { ["jsonrpc"] = "2.0", ["method"] = "initialized", ["params"] = new JObject() }, timeout.Token);
        using var server = await serving.WaitAsync(timeout.Token);
    }

    // a message as LSP frames it, written as a client that is not OmniSharp's writes it.
    private static async Task SendAsync(PipeWriter writer, JObject message, CancellationToken token)
    {
        var body = Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None));
        _ = await writer.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"), token);
        _ = await writer.WriteAsync(body, token);
    }

    // ---- what a platform's client says ----

    [TestMethod]
    public void APlatformsClient_SaysTheLanguageOfItsUserInterface_ByItsTag_AndTheInvariantCultureSaysNone()
    {
        Assert.AreEqual("fr-CA", ClientLocale.TagOf(CultureInfo.GetCultureInfo("fr-CA")));
        Assert.IsNull(ClientLocale.TagOf(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TheRequestsOwnLocale_IsReadFirst_AndThePlatformsInitializationOptionsOtherwise()
    {
        var options = new RDCore.SDK.Platform.Protocol.RDCoreInitializationOptions { Locale = "fr-CA" };

        Assert.AreEqual("de-CH", ClientLocale.Of("de-CH", options), "a client of the protocol says it in the request");
        Assert.AreEqual("fr-CA", ClientLocale.Of(null, options), "a platform's client, whose language client cannot say it there");
        Assert.AreEqual("fr-CA", ClientLocale.Of("  ", options));
        Assert.IsNull(ClientLocale.Of(null, null));
    }

    // a request's locale that is no culture the platform knows does not hide the one a platform's client says; when neither is known, the request's stands.
    [TestMethod]
    public void ARequestsLocaleThePlatformDoesNotKnow_GivesWayToTheInitializationOptions()
    {
        var options = new RDCore.SDK.Platform.Protocol.RDCoreInitializationOptions { Locale = "fr-CA" };

        Assert.AreEqual("fr-CA", ClientLocale.Of("xx-YY", options));
        Assert.AreEqual("xx-YY", ClientLocale.Of("xx-YY", new RDCore.SDK.Platform.Protocol.RDCoreInitializationOptions { Locale = "zz-QQ" }));
        Assert.AreEqual("xx-YY", ClientLocale.Of("xx-YY", null));
    }

    // the initializationOptions as they travel, read by the server as it reads them.
    [TestMethod]
    public void ThePlatformsInitializationOptions_AreReadAsTheyTravel()
    {
        var sent = JToken.FromObject(new RDCore.SDK.Platform.Protocol.RDCoreInitializationOptions { Locale = ClientLocale.TagOf(CultureInfo.GetCultureInfo("fr-CA")) });

        Assert.AreEqual("fr-CA", ClientLocale.Of(null, RDCoreServerApp.ReadInitializationOptions(sent)));
    }

    // a platform's client in French, and a server on an English machine: the server applies what the client says, and a thread it begins later speaks it too.
    [TestMethod]
    public async Task WhatAPlatformsClientInFrenchSays_IsTheServersLanguage_OnAThreadItBeginsLater()
    {
        var said = RDCore.SDK.Client.RDCoreClientApp.InitializationOptionsOf(null, CultureInfo.GetCultureInfo("fr-CA"), new());
        var sent = JToken.FromObject(said);
        CultureInfo.DefaultThreadCurrentUICulture = null;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        Assert.IsNotNull(ClientLocale.Apply(ClientLocale.Of(null, RDCoreServerApp.ReadInitializationOptions(sent))));
        var later = await Task.Factory.StartNew(() => CultureInfo.CurrentUICulture.Name, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        Assert.AreEqual("fr-CA", said.Locale);
        Assert.AreEqual("fr-CA", later);
    }
}
