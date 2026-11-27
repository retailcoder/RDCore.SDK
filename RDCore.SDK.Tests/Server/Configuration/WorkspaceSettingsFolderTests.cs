using Microsoft.Extensions.Configuration;
using RDCore.SDK.Server.Configuration;
using System.IO.Abstractions.TestingHelpers;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// A component started on one workspace that learns its workspace is another folder (the one its client opened) takes that folder's settings: its
/// configuration says what that folder sets and not what the first one set, and so do its settings objects, live or not. What its client said stays
/// said, under the other folder's settings.
/// </summary>
[TestClass]
public sealed class WorkspaceSettingsFolderTests
{
    private static readonly string Parent = Path.Combine(Path.GetTempPath(), "rdcore-root-split");
    private static readonly string Project = Path.Combine(Parent, "Project1");

    private readonly MockFileSystem _fileSystem = new();
    private readonly SdkAppOptions _options = new();
    private ConfigurationManager _configuration = default!;
    private LiveSettings _live = default!;

    // the parent sets a live setting, the project one that is read when a component starts.
    [TestInitialize]
    public void Initialize()
    {
        Assert.IsTrue(SettingsSchema.Platform.TryFind("Formatting.IndentCase", out var indentCase) && indentCase.Reach == SettingReach.Live);
        Assert.IsTrue(SettingsSchema.Platform.TryFind("Workspace.FileLevelStatements", out var statements) && statements.Reach == SettingReach.NextStart);
        Assert.IsTrue(WorkspaceSettingsStore.CanSet(indentCase) && WorkspaceSettingsStore.CanSet(statements), "a workspace is to set both");
        Assert.IsFalse(
            new SdkAppOptions().Formatting.IndentCase || new SdkAppOptions().Workspace.FileLevelStatements,
            "each folder is to set its own away from its default");

        _fileSystem.AddFile(WorkspaceSettingsStore.PathIn(Parent), new MockFileData("""{ "Configuration": { "Formatting": { "IndentCase": true } } }"""));
        _fileSystem.AddFile(
            WorkspaceSettingsStore.PathIn(Project), new MockFileData("""{ "Configuration": { "Workspace": { "FileLevelStatements": true } } }"""));
    }

    // a component as AppHost starts one on a folder: its configuration has that folder's layer, its objects are bound from it, and its live settings know
    // what the folder sets.
    private WorkspaceSettingsFolder StartedOn(string? folder)
    {
        _configuration = new ConfigurationManager();
        var store = new WorkspaceSettingsStore(_fileSystem, SettingsSchema.Platform);
        if (folder is not null)
        {
            _configuration.AddWorkspaceSettings(folder, SettingsSchema.Platform, _fileSystem);
        }

        _configuration.GetSection(SettingsSchema.Root).Bind(_options);
        _live = new LiveSettings([new LiveSettingsTarget(SettingsSchema.Platform, string.Empty, _options)], SettingsSchema.Platform,
            Path.Combine(Path.GetTempPath(), "rdcore-nowhere", UserSettingsStore.FileName))
        {
            Workspace = folder is null ? new Dictionary<string, string>() : store.Values(folder),
        };
        return new WorkspaceSettingsFolder(_configuration, SettingsSchema.Platform, folder, _live, store, _fileSystem);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "started on another folder")]
    [DataRow(false, DisplayName = "started on no folder")]
    public void MovedToAnotherFolder_TheComponentHasThatFoldersSettings_AndNoLongerTheFirstOnes(bool startedOnAFolder)
    {
        var folder = StartedOn(startedOnAFolder ? Parent : null);
        Assert.AreEqual(startedOnAFolder, _options.Formatting.IndentCase, "what the folder it started on sets");

        Assert.IsTrue(folder.MoveTo(Project));

        Assert.AreEqual("True", _configuration["Configuration:Workspace:FileLevelStatements"]);
        Assert.IsNull(_configuration["Configuration:Formatting:IndentCase"]);
        Assert.IsTrue(_options.Workspace.FileLevelStatements, "a setting read when it starts");
        Assert.IsFalse(_options.Formatting.IndentCase, "a live one");
        Assert.AreEqual(Project, folder.Folder);
    }

    [TestMethod]
    public void MovedToTheFolderItStartedOn_WrittenAnotherWay_NothingIsReadAgain()
    {
        var folder = StartedOn(Parent);
        _fileSystem.AddFile(WorkspaceSettingsStore.PathIn(Parent), new MockFileData("""{ "Configuration": { "Formatting": { "IndentCase": false } } }"""));

        Assert.IsFalse(folder.MoveTo(Parent + Path.DirectorySeparatorChar));

        Assert.IsTrue(_options.Formatting.IndentCase, "the folder's settings as they were read when it started");
        Assert.AreEqual("True", _configuration["Configuration:Formatting:IndentCase"]);
    }

    [TestMethod]
    public void ALiveSettingTheClientSaysNothingOf_TakesBackWhatTheOtherFolderGivesIt()
    {
        var folder = StartedOn(Parent);
        _ = folder.MoveTo(Project);

        _live.Apply(new Dictionary<string, string>());

        Assert.IsFalse(_options.Formatting.IndentCase, "what the component would have started with on the other folder, and not on the first");
    }

    [TestMethod]
    public void WhatTheClientSaid_IsSetAgain_UnderTheOtherFoldersSettings()
    {
        var folder = StartedOn(Parent);
        _live.Apply(new Dictionary<string, string> { ["Formatting:IndentCase"] = "false" });
        Assert.IsTrue(_options.Formatting.IndentCase, "the first folder's setting is above what the client says");

        _ = folder.MoveTo(Project);

        Assert.IsFalse(_options.Formatting.IndentCase, "what the client said, with nothing above it");
        Assert.AreEqual("false", _live.Said["Formatting:IndentCase"]);
    }
}
