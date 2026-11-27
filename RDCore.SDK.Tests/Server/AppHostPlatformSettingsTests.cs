using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RDCore.SDK;
using RDCore.SDK.Client;
using RDCore.SDK.Platform;
using RDCore.SDK.Server;
using RDCore.SDK.Server.Configuration;

namespace RDCore.Tests.Server;

/// <summary>
/// Every component of a platform starts with the platform's settings file under its own settings: an extension, which ships no list of extensions of its own,
/// has the platform's list of the extensions that load, as the language server does.
/// </summary>
/// <remarks>
/// These write the platform's settings file of the test process and the folder of its user, which every component started at the same time would read: they
/// run on their own.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class AppHostPlatformSettingsTests
{
    private string _user = null!;
    private string? _userData;
    private bool _platformFileWasThere;

    private static string PlatformFile => PlatformEnvironment.Default.SettingsPath;

    [TestInitialize]
    public void Remember()
    {
        _userData = Environment.GetEnvironmentVariable(PlatformEnvironment.UserDataEnvironmentVariable);
        _user = Path.Combine(Path.GetTempPath(), "RDCore", "platform-settings-host", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_user);
        Environment.SetEnvironmentVariable(PlatformEnvironment.UserDataEnvironmentVariable, _user);
        _platformFileWasThere = File.Exists(PlatformFile);
        Assert.IsFalse(_platformFileWasThere, $"a test leaves the platform's settings file of the test process alone: {PlatformFile}");
        RecordingApp.Allowed = null;
    }

    [TestCleanup]
    public void Restore()
    {
        Environment.SetEnvironmentVariable(PlatformEnvironment.UserDataEnvironmentVariable, _userData);
        if (!_platformFileWasThere)
        {
            File.Delete(PlatformFile);
        }

        Directory.Delete(_user, recursive: true);
    }

    [TestMethod]
    public async Task AComponent_HasThePlatformsListOfExtensions_AndTheUsersAddedToIt()
    {
        File.WriteAllText(PlatformFile, """{ "Configuration": { "Platform": { "Extensions": { "Allowed": [ "RDCore.Diagnostics", "RDCore.Web" ] } } } }""");
        File.WriteAllText(UserSettingsStore.PathIn(PlatformEnvironment.Default),
            """{ "Configuration": { "Platform": { "Extensions": { "Allowed": [ "RDCore.UnitTesting" ] } } } }""");

        Assert.AreEqual(0, await new ExtensionTestAppHost().RunAsync([]));

        CollectionAssert.IsSubsetOf(new[] { "RDCore.Diagnostics", "RDCore.Web", "RDCore.UnitTesting" }, RecordingApp.Allowed);
    }

    private sealed class ExtensionTestAppHost : AppHost<RecordingApp>
    {
        protected override void Configure(IConfigurationBuilder configuration, IServiceCollection services, string[] args) { }
    }

    internal sealed class RecordingApp(IOptions<SdkAppOptions> options) : IRDCoreApp
    {
        public static List<string>? Allowed { get; set; }

        public CoreServerComponent PlatformComponent => CoreServerComponent.LanguageServer;

        public Task RunAsync(IServiceProvider provider, string[] args)
        {
            Allowed = [.. options.Value.Platform.Extensions.Allowed];
            return Task.CompletedTask;
        }

        public void LogIfEnabled(LogLevel logLevel, string message) { }

        public void Dispose() { }
    }
}
