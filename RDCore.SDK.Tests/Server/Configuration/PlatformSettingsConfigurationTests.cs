using Microsoft.Extensions.Configuration;
using NSubstitute;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Platform;
using RDCore.SDK.Server.Configuration;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// The platform's settings file (<c>platform-settings.json</c>) is read twice: below every other source, as the defaults every component shares, and above
/// every other source for the settings only the platform sets (<see cref="SettingReach.Platform"/>), which nothing else changes.
/// </summary>
[TestClass]
public sealed class PlatformSettingsConfigurationTests
{
    // the settings of an application, as its settings type declares them: two only the platform sets, and two any source sets.
    public sealed class LockOptions
    {
        [Setting(SettingReach.Platform)]
        public string Mode { get; set; } = "Allow";

        [Setting(SettingReach.Platform)]
        public DayOfWeek Day { get; set; } = DayOfWeek.Friday;

        [Setting(SettingReach.Platform)]
        public List<string> Codes { get; set; } = [];

        public string Plain { get; set; } = "default";

        public List<string> Allowed { get; set; } = [];
    }

    private static readonly SettingsSchema Schema = SettingsSchema.For<LockOptions>("Lock");

    private string _folder = null!;
    private string _environmentPrefix = null!;

    private string PlatformFile => Path.Combine(_folder, PlatformEnvironment.SettingsFileName);
    private string AppFile => Path.Combine(_folder, "app", "appsettings.json");
    private string UserFile => Path.Combine(_folder, "user", UserSettingsStore.FileName);

    [TestInitialize]
    public void CreateFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "RDCore", $"platform-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_folder, "app"));
        Directory.CreateDirectory(Path.Combine(_folder, "user"));
        // each test its own variables: the tests run side by side, in one process.
        _environmentPrefix = $"RDCTEST{Guid.NewGuid():N}_";
    }

    [TestCleanup]
    public void DeleteFolder()
    {
        var variables = Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(name => name.StartsWith(_environmentPrefix, StringComparison.Ordinal));
        foreach (var variable in variables.ToList())
        {
            Environment.SetEnvironmentVariable(variable, null);
        }

        Directory.Delete(_folder, recursive: true);
    }

    private static void Write(string path, string lockSection) => File.WriteAllText(path, $$"""{ "Configuration": { "Lock": { {{lockSection}} } } }""");

    private void SetEnvironment(string key, string value) => Environment.SetEnvironmentVariable($"{_environmentPrefix}Configuration__Lock__{key}", value);

    // the sources in the order an application's host has them: its own appsettings.json, the environment and the command line, then the platform's file and
    // the user's settings, which each go where they belong.
    private IConfigurationRoot Configuration(params string[] commandLine)
        => new ConfigurationBuilder()
            .AddJsonFile(AppFile, optional: true)
            .AddEnvironmentVariables(_environmentPrefix)
            .AddCommandLine(commandLine)
            .AddPlatformSettings(PlatformFile, Schema)
            .AddUserSettings(UserFile, Schema)
            .Build();

    private LockOptions Bound(params string[] commandLine)
        => Configuration(commandLine).GetSection("Configuration:Lock").Get<LockOptions>() ?? new LockOptions();

    [TestMethod]
    [DataRow("app", DisplayName = "the application's appsettings.json")]
    [DataRow("environment", DisplayName = "a Configuration__ environment variable")]
    [DataRow("command line", DisplayName = "a command-line switch")]
    public void ASettingOnlyThePlatformSets_IsThePlatformFiles_WhateverAnotherSourceSays(string other)
    {
        Write(PlatformFile, "\"Mode\": \"Block\"");
        string[] commandLine = [];
        switch (other)
        {
            case "app": Write(AppFile, "\"Mode\": \"Allow\""); break;
            case "environment": SetEnvironment("Mode", "Allow"); break;
            default: commandLine = ["--Configuration:Lock:Mode=Allow"]; break;
        }

        Assert.AreEqual("Block", Bound(commandLine).Mode);
    }

    [TestMethod]
    public void ASettingOnlyThePlatformSets_ThatThePlatformFileDoesNotSay_IsItsDefault()
    {
        Write(PlatformFile, "\"Plain\": \"platform\"");
        Write(AppFile, "\"Mode\": \"Halt\", \"Day\": \"Monday\"");
        SetEnvironment("Mode", "Halt");

        var bound = Bound("--Configuration:Lock:Mode=Halt");

        Assert.AreEqual("Allow", bound.Mode);
        Assert.AreEqual(DayOfWeek.Friday, bound.Day);
    }

    [TestMethod]
    public void AListOnlyThePlatformSets_HasThePlatformFilesItems_AndNoOtherSources()
    {
        Write(PlatformFile, "\"Codes\": [ \"A\" ]");
        Write(AppFile, "\"Codes\": [ \"B\", \"C\" ]");
        Write(UserFile, "\"Codes\": [ \"F\" ]");
        SetEnvironment("Codes__2", "D");

        CollectionAssert.AreEqual(new[] { "A" }, Bound("--Configuration:Lock:Codes:3=E").Codes);
    }

    [TestMethod]
    public void AListOnlyThePlatformSets_ThatThePlatformFileDoesNotSay_IsEmpty()
    {
        Write(AppFile, "\"Codes\": [ \"B\" ]");
        SetEnvironment("Codes__0", "D");

        Assert.IsEmpty(Bound().Codes);
    }

    [TestMethod]
    public void AnotherSetting_ThatOnlyThePlatformFileSays_IsThePlatformFiles()
    {
        Write(PlatformFile, "\"Plain\": \"platform\", \"Allowed\": [ \"RDCore.Diagnostics\", \"RDCore.Web\" ]");

        var bound = Bound();

        Assert.AreEqual("platform", bound.Plain);
        CollectionAssert.AreEqual(new[] { "RDCore.Diagnostics", "RDCore.Web" }, bound.Allowed);
    }

    // the platform's file is below the application's own: the user's settings still go above the application's file, and the environment above them.
    [TestMethod]
    [DataRow(false, false, "app", DisplayName = "the application's file over the platform's")]
    [DataRow(true, false, "user", DisplayName = "the user's settings over the application's file")]
    [DataRow(true, true, "environment", DisplayName = "the environment over the user's settings")]
    public void AnotherSetting_IsTheOneOfTheSourceAboveTheOthers(bool user, bool environment, string expected)
    {
        Write(PlatformFile, "\"Plain\": \"platform\"");
        Write(AppFile, "\"Plain\": \"app\"");
        if (user)
        {
            Write(UserFile, "\"Plain\": \"user\"");
        }

        if (environment)
        {
            SetEnvironment("Plain", "environment");
        }

        Assert.AreEqual(expected, Bound().Plain);
    }

    // a setting both files say: the user's settings are above the platform's file, unless it is one only the platform sets; then nothing is above it.
    [TestMethod]
    public void ASettingInBothFiles_IsTheUsers_UnlessOnlyThePlatformSetsIt()
    {
        Write(PlatformFile, "\"Mode\": \"Block\", \"Plain\": \"platform\", \"Allowed\": [ \"RDCore.Diagnostics\", \"RDCore.UnitTesting\" ]");
        Write(UserFile, "\"Mode\": \"Allow\", \"Plain\": \"user\", \"Allowed\": [ \"RDCore.Web\" ]");
        SetEnvironment("Mode", "Halt");

        var bound = Bound("--Configuration:Lock:Mode=Allow");

        Assert.AreEqual("Block", bound.Mode);
        Assert.AreEqual("user", bound.Plain);
        CollectionAssert.AreEqual(new[] { "RDCore.Diagnostics", "RDCore.UnitTesting", "RDCore.Web" }, bound.Allowed);
    }

    [TestMethod]
    public void AListTheUserSets_AddsTheUsersItems_ThatTheLayersBelowDoNotHave()
    {
        Write(PlatformFile, "\"Allowed\": [ \"A\", \"B\" ]");
        Write(AppFile, "\"Allowed\": [ \"A\", \"B\", \"C\" ]");
        Write(UserFile, "\"Allowed\": [ \"b\", \"D\" ]");

        CollectionAssert.AreEqual(new[] { "A", "B", "C", "D" }, Bound().Allowed);
    }

    [TestMethod]
    public void AnItemOnlyALayerBelowHolds_IsNotTheUsersToRemove()
    {
        Write(PlatformFile, "\"Allowed\": [ \"A\", \"B\" ]");
        Write(UserFile, "\"Allowed\": [ \"C\" ]");
        var written = File.ReadAllText(UserFile);
        var store = new UserSettingsStore(new System.IO.Abstractions.FileSystem(), UserFile, Schema, Configuration());

        var change = store.Remove("Lock.Allowed", "A");

        Assert.AreEqual(UserSettingProblem.NotTheUsers, change.Problem);
        Assert.AreEqual(written, File.ReadAllText(UserFile));
    }

    [TestMethod]
    public void AnItemTheUserAdded_IsTheUsersToRemove_ThoughALayerBelowHoldsOthers()
    {
        Write(PlatformFile, "\"Allowed\": [ \"A\", \"B\" ]");
        Write(UserFile, "\"Allowed\": [ \"C\" ]");
        var store = new UserSettingsStore(new System.IO.Abstractions.FileSystem(), UserFile, Schema, Configuration());

        Assert.IsTrue(store.Remove("Lock.Allowed", "C").IsSet);
        CollectionAssert.AreEqual(new[] { "A", "B" }, Bound().Allowed);
    }

    // an extension the platform allows, and the user blocks: it is blocked, whatever else the platform's file allows.
    [TestMethod]
    public void AnExtensionThePlatformAllows_ThatTheUserBlocks_IsBlocked()
    {
        File.WriteAllText(PlatformFile,
            """{ "Configuration": { "Platform": { "Extensions": { "Allowed": [ "RDCore.Diagnostics", "RDCore.UnitTesting" ] } } } }""");
        File.WriteAllText(UserFile, """{ "Configuration": { "Platform": { "Extensions": { "Blocked": [ { "Title": "RDCore.UnitTesting" } ] } } } }""");
        var options = new ConfigurationBuilder()
            .AddPlatformSettings(PlatformFile, SettingsSchema.Platform)
            .AddUserSettings(UserFile, SettingsSchema.Platform)
            .Build()
            .GetSection(SettingsSchema.Root).Get<SdkAppOptions>()!;
        var validation = new ExtensionManifestValidationService(
            Microsoft.Extensions.Options.Options.Create(options), Substitute.For<System.IO.Abstractions.IFileSystem>(),
            Substitute.For<IPlatformEnvironment>());

        CollectionAssert.Contains(options.Platform.Extensions.Allowed, "RDCore.UnitTesting");
        Assert.AreEqual(ExtensionValidationFlags.Blocked, validation.Validate(Manifest("RDCore.UnitTesting")));
    }

    private static ExtensionInfo Manifest(string title) => new()
    {
        Name = $"{title}.exe",
        Title = title,
        Version = new Version(1, 0, 0),
        // built with this SDK, so Blocked is the only reason it does not load.
        SdkVersion = ExtensionManifestValidationService.SdkVersion,
        Publisher = "Publisher",
        PublisherWebUrl = "https://example.com",
        Description = "An extension.",
        FileHash = string.Empty,
        Capabilities = [],
    };

    [TestMethod]
    public void NoPlatformFile_LeavesTheOtherSources_AndTheDefaults()
    {
        Write(AppFile, "\"Plain\": \"app\", \"Mode\": \"Halt\"");

        var bound = Bound();

        Assert.AreEqual("app", bound.Plain);
        Assert.AreEqual("Allow", bound.Mode);
    }

    [TestMethod]
    public void APlatformFileThatCannotBeRead_IsNoSetting()
    {
        File.WriteAllText(PlatformFile, "{ not json");
        Write(AppFile, "\"Plain\": \"app\"");

        var bound = Bound();

        Assert.AreEqual("app", bound.Plain);
        Assert.AreEqual("Allow", bound.Mode);
    }

    [TestMethod]
    public void ASettingOnlyThePlatformSets_IsNotSettable()
    {
        Assert.IsTrue(Schema.TryFind("Lock.Mode", out var mode));

        Assert.IsFalse(mode.IsSettable);
        Assert.IsFalse(WorkspaceSettingsStore.CanSet(mode));
    }

    [TestMethod]
    public void ThePlatformFile_IsAtThePlatformRoot()
    {
        var environment = new PlatformEnvironment(new System.IO.Abstractions.FileSystem());

        Assert.AreEqual(Path.Combine(environment.Root, "platform-settings.json"), ((IPlatformEnvironment)environment).SettingsPath);
    }
}
