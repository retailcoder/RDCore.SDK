using Microsoft.Extensions.Configuration;
using RDCore.SDK.Runtime;
using RDCore.SDK.Server.Auditing;
using RDCore.SDK.Server.Configuration;
using System.IO.Abstractions.TestingHelpers;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// The execution policy (<c>Environment:ExecutionPolicy</c>) and the audit log (<c>Audit</c>) are the deployment's settings: bound from its configuration, and
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
    public void APolicyOrAuditSetting_IsTheDeployments_AndAUserSetsNoneOfThem(string key)
    {
        Assert.IsTrue(SettingsSchema.Platform.TryFind(key, out var setting));
        Assert.AreEqual(SettingReach.Deployment, setting.Reach);
        Assert.IsFalse(setting.IsSettable);

        var user = new UserSettingsStore(new MockFileSystem(), Path.Combine(Folder, "user.json"), SettingsSchema.Platform);
        Assert.AreEqual(UserSettingProblem.NotSettable, user.Set(key, setting.Choices.FirstOrDefault() ?? "1").Problem);
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
