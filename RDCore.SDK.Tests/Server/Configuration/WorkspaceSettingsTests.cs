using Microsoft.Extensions.Configuration;
using RDCore.SDK.Server.Configuration;
using System.IO.Abstractions.TestingHelpers;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// What a workspace sets for itself, in the <c>Configuration</c> section of its <c>appsettings.json</c>, comes after what its user set and before what a
/// process is told when it starts. A workspace grants itself no rights: it sets no setting of the deployment and no trust-sensitive one. A list it sets adds
/// its items to the list below it.
/// </summary>
[TestClass]
public sealed class WorkspaceSettingsConfigurationTests
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rdcore-workspace-settings-" + Guid.NewGuid().ToString("N"));

    private string Workspace => Path.Combine(_folder, "workspace");

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string Write(string folder, string name, string json)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, json);
        return path;
    }

    private IConfigurationRoot Configuration(string userSettings, string workspaceSettings, params (string Key, string Value)[] startedWith)
    {
        Write(_folder, "appsettings.json", """
            { "Configuration": {
                "Server": { "Verbose": true, "TraceLevel": "Trace", "ConnectTimeoutSeconds": 30 },
                "Environment": { "AllowDllImports": true, "AllowAutomation": true, "LegacyRegistrySettings": false }
            } }
            """);
        Write(Workspace, WorkspaceSettingsStore.FileName, workspaceSettings);
        var builder = new ConfigurationBuilder()
            .SetBasePath(_folder)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddInMemoryCollection(startedWith.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)));

        // as a component adds them: the user's settings, then the workspace's once the command line has named it.
        builder.AddUserSettings(Write(_folder, UserSettingsStore.FileName, userSettings), SettingsSchema.Platform);
        return builder.AddWorkspaceSettings(Workspace, SettingsSchema.Platform).Build();
    }

    private static string?[] Items(IConfiguration configuration, string path)
        => [.. configuration.GetSection(path).GetChildren().OrderBy(item => int.Parse(item.Key)).Select(item => item.Value)];

    [TestMethod]
    public void AWorkspaceThatSetsATrustSensitiveSetting_IsRefused()
    {
        var configuration = Configuration("{ }", """
            { "Configuration": {
                "Environment": { "AllowDllImports": false, "AllowAutomation": false, "LegacyRegistrySettings": true },
                "Platform": { "Extensions": { "Allowed": [ "Anything" ], "Location": "elsewhere" } }
            } }
            """);

        Assert.AreEqual("True", configuration["Configuration:Environment:AllowDllImports"]);
        Assert.AreEqual("True", configuration["Configuration:Environment:AllowAutomation"]);
        Assert.AreEqual("False", configuration["Configuration:Environment:LegacyRegistrySettings"]);
        Assert.IsNull(configuration["Configuration:Platform:Extensions:Allowed:0"]);
        Assert.IsNull(configuration["Configuration:Platform:Extensions:Location"]);
    }

    [TestMethod]
    public void AWorkspaceThatSetsASettingOfTheDeployment_IsRefused()
    {
        Assert.IsTrue(SettingsSchema.Platform.TryFind("Platform.Transport.Type", out var transport));
        Assert.AreEqual(SettingReach.Deployment, transport.Reach);

        var configuration = Configuration("{ }", """
            { "Configuration": {
                "Platform": { "Transport": { "Type": "Sockets", "PipeConfig": { "PipeName": "elsewhere" } } }
            } }
            """);

        Assert.IsNull(configuration["Configuration:Platform:Transport:Type"]);
        Assert.IsNull(configuration["Configuration:Platform:Transport:PipeConfig:PipeName"]);
    }

    [TestMethod]
    public void AListAWorkspaceSets_AddsItsItemsToTheListBelowIt_AndIsNotLaidOverItIndexByIndex()
    {
        var configuration = Configuration(
            """{ "Configuration": { "Analysis": { "DisabledDiagnostics": [ "RDC01001", "RDC01002", "RDC01003" ] } } }""",
            """{ "Configuration": { "Analysis": { "DisabledDiagnostics": [ "XYZ00042", "rdc01002", "XYZ00043" ] } } }""");

        CollectionAssert.AreEqual(new[] { "RDC01001", "RDC01002", "RDC01003", "XYZ00042", "XYZ00043" }, Items(configuration, "Configuration:Analysis:DisabledDiagnostics"));

        var bound = new SdkAppOptions();
        configuration.GetSection(SettingsSchema.Root).Bind(bound);
        CollectionAssert.AreEquivalent(new[] { "RDC01001", "RDC01002", "RDC01003", "XYZ00042", "XYZ00043" }, bound.Analysis.DisabledCodes.Select(code => code.ToString()).ToArray());
    }

    [TestMethod]
    public void AListAWorkspaceSets_IsItsOwn_WhenNothingBelowItSetsOne()
    {
        var configuration = Configuration("{ }", """{ "Configuration": { "Analysis": { "DisabledDiagnostics": [ "XYZ00042", "VBC00001", "xyz00043" ] } } }""");

        CollectionAssert.AreEqual(new[] { "XYZ00042", "XYZ00043" }, Items(configuration, "Configuration:Analysis:DisabledDiagnostics"), "a compile error cannot be disabled");
    }

    [TestMethod]
    [DataRow("XYZ00042, xyz00043", "XYZ00042|XYZ00043", DisplayName = "codes")]
    [DataRow("XYZ00042, VBC00001", "", DisplayName = "a compile error among them")]
    public void AListAWorkspaceWritesAsText_IsReadAsTheSettingReadsIt(string text, string items)
    {
        var configuration = Configuration("{ }", $$"""{ "Configuration": { "Analysis": { "DisabledDiagnostics": "{{text}}" } } }""");

        Assert.AreEqual(items, string.Join('|', Items(configuration, "Configuration:Analysis:DisabledDiagnostics")));
    }

    [TestMethod]
    public void WhatAWorkspaceSets_TakesThePlaceOfWhatTheUserSet_AndWhatAProcessIsStartedWithTakesItsPlace()
    {
        var configuration = Configuration(
            """{ "Configuration": { "Server": { "Verbose": false, "TraceLevel": "Error", "ConnectTimeoutSeconds": 20 } } }""",
            """{ "Configuration": { "Server": { "TraceLevel": "Warning", "ConnectTimeoutSeconds": 10 } } }""",
            ("Configuration:Server:ConnectTimeoutSeconds", "5"));

        Assert.AreEqual("False", configuration["Configuration:Server:Verbose"], "the user's");
        Assert.AreEqual("Warning", configuration["Configuration:Server:TraceLevel"], "the workspace's");
        Assert.AreEqual("5", configuration["Configuration:Server:ConnectTimeoutSeconds"], "the command line's");
    }

    [TestMethod]
    public void WhatIsNotAValueAWorkspaceCanSet_IsNotRead()
    {
        var configuration = Configuration("{ }", """
            {
              // written by hand
              "Configuration": {
                "Server": { "Verbose": "banana", "ConnectTimeoutSeconds": -1 },
                "Diagnostics": { "Anything": "goes" }
              },
              "ProgramSettings": { "MyApp": { "Startup": { "Theme": "Dark" } } },
            }
            """);

        Assert.AreEqual("True", configuration["Configuration:Server:Verbose"], "no boolean");
        Assert.AreEqual("30", configuration["Configuration:Server:ConnectTimeoutSeconds"], "out of range");
        Assert.IsNull(configuration["Configuration:Diagnostics:Anything"], "no setting of the schema");
        Assert.IsNull(configuration["ProgramSettings:MyApp:Startup:Theme"], "the program's own");
    }

    [TestMethod]
    public void AFileThatCannotBeRead_SetsNothing()
        => Assert.AreEqual("Trace", Configuration("{ }", "{ not json")["Configuration:Server:TraceLevel"]);

    [TestMethod]
    public void AFileThatIsNotThere_SetsNothing()
    {
        var builder = new ConfigurationBuilder().AddWorkspaceSettings(Path.Combine(_folder, "nowhere"), SettingsSchema.Platform);

        Assert.IsNull(builder.Build()["Configuration:Server:Verbose"]);
    }

    [TestMethod]
    public void TheFolderOfAWorkspace_IsTheOneItsFileUriNames_AndAUriOfAnotherSchemeNamesNone()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Project1");

        Assert.AreEqual(folder, WorkspaceSettingsStore.FolderOf(new Uri(folder).AbsoluteUri));
        Assert.AreEqual(folder, WorkspaceSettingsStore.FolderOf(folder));
        Assert.IsNull(WorkspaceSettingsStore.FolderOf("vscode-vfs://github/owner/repo"));
    }

    [TestMethod]
    public void TheFolderOfAWorkspace_NamedByARelativePath_IsUnderTheWorkingDirectory()
        => Assert.AreEqual(Path.GetFullPath("Project1"), WorkspaceSettingsStore.FolderOf("Project1"));
}

/// <summary>
/// The lists of a workspace are changed in its file, which keeps what else it holds; a workspace changes no setting it cannot set. While a component runs,
/// what its client says is what the user set, and the workspace's settings stay above it.
/// </summary>
[TestClass]
public sealed class WorkspaceSettingsStoreTests
{
    private const string Key = "Analysis.DisabledDiagnostics";
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "rdcore-workspace");
    private static readonly Uri Root = new(Folder);

    private readonly MockFileSystem _fileSystem = new();

    private WorkspaceSettingsStore Sut => new(_fileSystem, SettingsSchema.Platform);

    private string FilePath => WorkspaceSettingsStore.PathIn(Folder);

    [TestMethod]
    public void ACode_IsAddedToTheWorkspacesList_AndTheProgramsOwnSettingsAreKept()
    {
        _fileSystem.AddFile(FilePath, new MockFileData("""{ "ProgramSettings": { "MyApp": { "Startup": { "Theme": "Dark" } } } }"""));

        Assert.IsTrue(Sut.Add(Root, Key, "RDC01001").IsSet);
        Assert.IsTrue(Sut.Add(Root, Key, "xyz00042").IsSet);
        Assert.IsTrue(Sut.Remove(Root, Key, "RDC01001").IsSet);

        CollectionAssert.AreEqual(new[] { "XYZ00042" }, Sut.Read(Root, Key).ToArray());
        var file = new ConfigurationBuilder().AddJsonStream(_fileSystem.File.OpenRead(FilePath)).Build();
        Assert.AreEqual("Dark", file["ProgramSettings:MyApp:Startup:Theme"]);
    }

    [TestMethod]
    [DataRow("Platform.Extensions.Allowed", "Anything", UserSettingProblem.NotSettable, DisplayName = "trust-sensitive")]
    [DataRow("Platform.Transport.Type", "Sockets", UserSettingProblem.NotSettable, DisplayName = "of the deployment")]
    [DataRow("No.Such.Setting", "Anything", UserSettingProblem.UnknownSetting, DisplayName = "no setting")]
    [DataRow("Server.Verbose", "true", UserSettingProblem.NotAList, DisplayName = "not a list")]
    public void AWorkspace_ChangesNoListItCannotSet(string key, string value, UserSettingProblem problem)
    {
        Assert.AreEqual(problem, Sut.Add(Root, key, value).Problem);
        Assert.IsFalse(_fileSystem.File.Exists(FilePath));
    }

    [TestMethod]
    public void ATrustSensitiveListTheFileHoldsByHand_IsNotRead()
    {
        _fileSystem.AddFile(FilePath, new MockFileData("""{ "Configuration": { "Platform": { "Extensions": { "Allowed": [ "Anything" ] } } } }"""));

        Assert.IsEmpty(Sut.Read(Root, "Platform.Extensions.Allowed"));
        Assert.IsEmpty(Sut.Values(Folder));
    }

    [TestMethod]
    public void WhileAComponentRuns_ThePlaceOfAWorkspacesSettings_IsAboveWhatItsClientSays()
    {
        Assert.IsTrue(SettingsSchema.Platform.TryFind("Formatting.IndentCase", out var indentCase));
        Assert.AreEqual(SettingReach.Live, indentCase.Reach);
        _fileSystem.AddFile(FilePath, new MockFileData("""
            { "Configuration": { "Analysis": { "DisabledDiagnostics": [ "XYZ00042" ] }, "Formatting": { "IndentCase": true } } }
            """));
        var options = new SdkAppOptions();
        var live = new LiveSettings([new LiveSettingsTarget(SettingsSchema.Platform, string.Empty, options)], SettingsSchema.Platform,
            Path.Combine(Path.GetTempPath(), "rdcore-nowhere", UserSettingsStore.FileName))
        {
            Workspace = Sut.Values(Folder),
        };

        live.Apply(new Dictionary<string, string>
        {
            ["Analysis:DisabledDiagnostics:0"] = "RDC01001",
            ["Formatting:IndentCase"] = "false",
        });

        CollectionAssert.AreEqual(new[] { "RDC01001", "XYZ00042" }, options.Analysis.DisabledDiagnostics);
        Assert.IsTrue(options.Formatting.IndentCase);
    }
}
