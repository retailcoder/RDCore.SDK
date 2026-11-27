using Microsoft.Extensions.Configuration;
using RDCore.SDK.Platform;
using RDCore.SDK.Runtime;
using RDCore.SDK.Server.Auditing;
using RDCore.SDK.Server.Configuration;
using System.IO.Abstractions.TestingHelpers;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// The execution policy (<c>Environment:ExecutionPolicy</c>) and the audit log (<c>Audit</c>) are the platform's settings: bound from its settings file, and
/// set by neither a user nor a workspace.
/// </summary>
[TestClass]
public sealed class ExecutionPolicySettingsTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "rdcore-execution-policy-settings");

    private static SdkAppOptions Bound(string json)
    {
        var bound = new SdkAppOptions();
        new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build()
            .GetSection(SettingsSchema.Root).Bind(bound);
        return bound;
    }

    // every setting of the two sections the schema holds, by its key.
    public static IEnumerable<object[]> PolicyAndAuditSettings => SettingsSchema.Platform.Settings
        .Where(setting => setting.Key.StartsWith("Environment.ExecutionPolicy.", StringComparison.Ordinal)
            || setting.Key.StartsWith("Audit.", StringComparison.Ordinal))
        .Select(setting => new object[] { setting.Key });

    // the description of each of those settings, by its key, in a language.
    private static Dictionary<string, string?> DescriptionsIn(string language)
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(language);
            var keys = PolicyAndAuditSettings.Select(row => (string)row[0]).ToHashSet();
            return SettingsSchema.Platform.Settings.Where(setting => keys.Contains(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Description);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = previous;
        }
    }

    // a client lists them too, as settings it cannot change, so each says what it does in each language the platform describes its settings in.
    [TestMethod]
    public void EveryPolicyAndAuditSetting_IsDescribedInEachLanguage_AndTranslated()
    {
        var first = DescriptionsIn(SettingsSchema.Languages[0]);
        Assert.IsNotEmpty(first, "the schema holds the policy's and the audit log's settings");

        foreach (var language in SettingsSchema.Languages)
        {
            var descriptions = DescriptionsIn(language);
            var undescribed = descriptions.Where(each => string.IsNullOrWhiteSpace(each.Value)).Select(each => each.Key).ToArray();
            CollectionAssert.AreEqual(Array.Empty<string>(), undescribed, $"not described in {language}: {string.Join(", ", undescribed)}");
            if (language != SettingsSchema.Languages[0])
            {
                var untranslated = first.Keys.Where(key => first[key] == descriptions[key]).ToArray();
                CollectionAssert.AreEqual(Array.Empty<string>(), untranslated, $"not translated to {language}: {string.Join(", ", untranslated)}");
            }
        }
    }

    [TestMethod]
    public void ThePolicy_IsBoundFromTheEnvironmentSection()
    {
        var bound = Bound("""
            { "Configuration": { "Environment": { "ExecutionPolicy": {
                "ProcessLaunch": { "Mode": "Halt", "Audit": "All" },
                "FileIO": { "Mode": "Block" }
            } } } }
            """);

        var policy = bound.Environment.ExecutionPolicy.ToPolicy();

        Assert.AreEqual(new ExecutionRule(ExecutionPolicyMode.Halt, ExecutionAuditLevel.All), policy.ProcessLaunch);
        Assert.AreEqual(new ExecutionRule(ExecutionPolicyMode.Block, ExecutionAuditLevel.None), policy.FileIO);
        Assert.AreEqual(ExecutionRule.PassThrough, policy.LibraryImports);
    }

    [TestMethod]
    public void TheAuditLog_IsBoundFromTheAuditSection()
    {
        var bound = Bound("""
            { "Configuration": { "Audit": {
                "Capacity": 16, "ShutdownTimeoutSeconds": 2, "Arguments": "Values",
                "File": { "Enabled": false, "Directory": "audits" },
                "EventLog": { "Enabled": true, "LogName": "RDCore", "Source": "RD-VBA" }
            } } }
            """);

        Assert.AreEqual(16, bound.Audit.Capacity);
        Assert.AreEqual(2, bound.Audit.ShutdownTimeoutSeconds);
        Assert.AreEqual(AuditArguments.Values, bound.Audit.Arguments);
        Assert.AreEqual(new SdkAuditFileOptions { Enabled = false, Directory = "audits" }, bound.Audit.File);
        Assert.AreEqual(new SdkAuditEventLogOptions { Enabled = true, LogName = "RDCore", Source = "RD-VBA" }, bound.Audit.EventLog);
    }

    [TestMethod]
    public void TheSchema_HoldsARuleForEachCategory_AndFileIO()
    {
        var keys = SettingsSchema.Platform.Settings.Select(setting => setting.Key).ToHashSet();

        var categories = Enum.GetNames<ExecutionCategory>().Where(name => name != nameof(ExecutionCategory.Workspace)).Append(nameof(ExecutionPolicy.FileIO));
        foreach (var category in categories)
        {
            Assert.Contains($"Environment.ExecutionPolicy.{category}.Mode", keys);
            Assert.Contains($"Environment.ExecutionPolicy.{category}.Audit", keys);
        }

        Assert.IsFalse(keys.Any(key => key.StartsWith($"Environment.ExecutionPolicy.{nameof(ExecutionCategory.Workspace)}.", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DynamicData(nameof(PolicyAndAuditSettings))]
    public void APolicyOrAuditSetting_IsThePlatforms_AndAUserSetsNoneOfThem(string key)
    {
        Assert.IsTrue(SettingsSchema.Platform.TryFind(key, out var setting));
        Assert.AreEqual(SettingReach.Platform, setting.Reach);
        Assert.IsFalse(setting.IsSettable);

        var user = new UserSettingsStore(new MockFileSystem(), Path.Combine(Folder, "user.json"), SettingsSchema.Platform);
        Assert.AreEqual(UserSettingProblem.NotSettable, user.Set(key, setting.Choices.FirstOrDefault() ?? "1").Problem);
    }

    // the sandbox an administrator sets: what the platform's file says, or the default where it says nothing, whatever a component's own file, an
    // environment variable or the command line says.
    [TestMethod]
    public void ThePlatformsFile_SetsThePolicyAndTheAuditLog_AndNothingElseChangesThem()
    {
        var folder = Path.Combine(Path.GetTempPath(), "RDCore", $"execution-policy-settings-{Guid.NewGuid():N}");
        var prefix = $"RDCTEST{Guid.NewGuid():N}_";
        var platformFile = Path.Combine(folder, PlatformEnvironment.SettingsFileName);
        var appFile = Path.Combine(folder, "app", "appsettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(appFile)!);
        try
        {
            File.WriteAllText(platformFile, """
                { "Configuration": {
                    "Environment": { "ExecutionPolicy": { "LibraryImports": { "Mode": "Halt", "Audit": "All" } } },
                    "Audit": { "Capacity": 16 }
                } }
                """);
            File.WriteAllText(appFile, """
                { "Configuration": {
                    "Environment": { "ExecutionPolicy": { "Automation": { "Mode": "Block" } } },
                    "Audit": { "File": { "Enabled": false } }
                } }
                """);
            Environment.SetEnvironmentVariable($"{prefix}Configuration__Environment__ExecutionPolicy__LibraryImports__Mode", "Allow");
            Environment.SetEnvironmentVariable($"{prefix}Configuration__Audit__Capacity", "1");

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(appFile, optional: false)
                .AddEnvironmentVariables(prefix)
                .AddCommandLine(["--Configuration:Environment:ExecutionPolicy:LibraryImports:Audit=None", "--Configuration:Audit:Arguments=Values"])
                .AddPlatformSettings(platformFile, SettingsSchema.Platform)
                .Build();
            var bound = new SdkAppOptions();
            configuration.GetSection(SettingsSchema.Root).Bind(bound);

            Assert.AreEqual(new ExecutionRule(ExecutionPolicyMode.Halt, ExecutionAuditLevel.All), bound.Environment.ExecutionPolicy.ToPolicy().LibraryImports);
            Assert.AreEqual(ExecutionRule.PassThrough, bound.Environment.ExecutionPolicy.ToPolicy().Automation, "the component's own file sets no rule");
            Assert.AreEqual(16, bound.Audit.Capacity);
            Assert.AreEqual(AuditArguments.Types, bound.Audit.Arguments, "the command line sets nothing of the audit log");
            Assert.IsTrue(bound.Audit.File.Enabled, "the component's own file sets nothing of the audit log");
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}Configuration__Environment__ExecutionPolicy__LibraryImports__Mode", null);
            Environment.SetEnvironmentVariable($"{prefix}Configuration__Audit__Capacity", null);
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void AWorkspaceThatSetsThePolicyOrTheAuditLog_IsRefused()
    {
        var fileSystem = new MockFileSystem();
        var workspace = Path.Combine(Folder, "workspace");
        fileSystem.AddFile(WorkspaceSettingsStore.PathIn(workspace), new MockFileData("""
            { "Configuration": {
                "Environment": { "ExecutionPolicy": { "LibraryImports": { "Mode": "Allow", "Audit": "All" } } },
                "Audit": { "File": { "Enabled": false } },
                "Analysis": { "DisabledDiagnostics": [ "RDC01001" ] }
            } }
            """));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([
                new("Configuration:Environment:ExecutionPolicy:LibraryImports:Mode", "Halt"),
                new("Configuration:Audit:File:Enabled", "True"),
            ])
            .AddWorkspaceSettings(workspace, SettingsSchema.Platform, fileSystem)
            .Build();

        // the workspace's file is read: what a workspace may set, it sets.
        Assert.AreEqual("RDC01001", configuration["Configuration:Analysis:DisabledDiagnostics:0"]);
        Assert.AreEqual("Halt", configuration["Configuration:Environment:ExecutionPolicy:LibraryImports:Mode"]);
        Assert.IsNull(configuration["Configuration:Environment:ExecutionPolicy:LibraryImports:Audit"]);
        Assert.AreEqual("True", configuration["Configuration:Audit:File:Enabled"]);
    }
}
