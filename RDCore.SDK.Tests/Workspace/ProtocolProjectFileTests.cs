using RDCore.SDK.Workspace;
using System.Text.Json;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The project file model is in <c>RDCore.SDK.Protocol</c>, which a client of the platform references alone: an IDE's add-in reads a <c>.rdproj</c>
/// with it, and with nothing of the platform.
/// </summary>
[TestClass]
public sealed class ProtocolProjectFileTests
{
    [TestMethod]
    public void TheProjectFileModel_IsInAnAssemblyThatReferencesTheBaseLibraryAlone()
    {
        var assembly = typeof(ProjectFile).Assembly;

        Assert.AreEqual("RDCore.SDK.Protocol", assembly.GetName().Name);
        var others = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name != "System" && !name.StartsWith("System.", StringComparison.Ordinal) && name != "netstandard" && name != "mscorlib")
            .ToArray();
        Assert.IsEmpty(others, string.Join(", ", others));
    }

    [TestMethod]
    public void AProjectFile_ReadsWithTheSerializerAlone_InEitherFormOfItsFiles()
    {
        const string json = """
            {
              "Version": "0.1.0",
              "ProjectInfo": {
                "Name": "Battleship",
                "Modules": [ "src/Game.bas", { "RelativeUri": "Sheet1.doccls", "Super": "ExcelWorksheet" }, { "RelativeUri": "src/Ship.cls" } ],
                "OtherFiles": [ "README.md" ]
              }
            }
            """;

        var project = JsonSerializer.Deserialize<ProjectFile>(json)!;

        Assert.AreEqual("0.1.0", project.Version);
        Assert.AreEqual("Battleship", project.ProjectInfo.Name);
        CollectionAssert.AreEqual(new[] { "src/Game.bas", "Sheet1.doccls", "src/Ship.cls" }, project.ProjectInfo.Modules.Select(module => module.RelativeUri).ToArray());
        CollectionAssert.AreEqual(new DocClassType?[] { null, DocClassType.ExcelWorksheet, null }, project.ProjectInfo.Modules.Select(module => module.Super).ToArray());
        Assert.AreEqual("README.md", project.ProjectInfo.OtherFiles.Single().RelativeUri);
    }

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-protocol-read");

    private static Task<ProjectFile> ReadAsync(string json, bool byteOrderMark = false)
        => ProjectFile.ReadAsync(new MemoryStream([.. byteOrderMark ? new byte[] { 0xEF, 0xBB, 0xBF } : [], .. System.Text.Encoding.UTF8.GetBytes(json)]), Root);

    [TestMethod]
    [DataRow("""{ "ProjectInfo": null }""", new string[0], DisplayName = "a null project")]
    [DataRow("""{ "ProjectInfo": { "Name": null, "Modules": null, "OtherFiles": null, "Folders": null, "References": null, "PrecompilerConstants": null } }""", new string[0], DisplayName = "null lists")]
    [DataRow("""{ "ProjectInfo": { "Modules": [ null, "Module1.bas" ], "OtherFiles": [ null ], "Folders": [ null ], "References": [ null ] } }""", new[] { "Module1.bas" }, DisplayName = "null items")]
    [DataRow("""{ }""", new string[0], DisplayName = "nothing at all")]
    public async Task WhatAFileLeavesOut_ReadsAsEmpty_NeverNull(string json, string[] modules)
    {
        var project = await ReadAsync(json);

        Assert.AreEqual(Root, project.Uri);
        Assert.AreEqual(string.Empty, project.Version);
        Assert.IsNotNull(project.Configuration);
        var info = project.ProjectInfo;
        Assert.AreEqual(string.Empty, info.Name);
        CollectionAssert.AreEqual(modules, info.Modules.Select(module => module.RelativeUri).ToArray());
        Assert.IsEmpty(info.OtherFiles);
        Assert.IsEmpty(info.Folders);
        Assert.IsEmpty(info.References);
        Assert.IsNotNull(info.PrecompilerConstants);
    }

    [TestMethod]
    public async Task TheStandardLibrary_IsNoReferenceOfAProjectAsItIsRead_AndAMarkedFileReadsTheSame()
    {
        const string json = """
            { "ProjectInfo": { "References": [ { "Name": "VBA", "Guid": "000204ef-0000-0000-c000-000000000046" }, { "Name": "Scripting" } ] } }
            """;

        foreach (var marked in new[] { false, true })
        {
            var project = await ReadAsync(json, marked);

            Assert.AreEqual("Scripting", project.ProjectInfo.References.Single().Name);
        }
    }

    [TestMethod]
    public async Task AFileThatIsNoJsonObject_ThrowsTheJsonException()
    {
        await Assert.ThrowsAsync<JsonException>(() => ReadAsync("null"));
        await Assert.ThrowsAsync<JsonException>(() => ReadAsync("{ \"ProjectInfo\": { ]"));
    }

    // a project file's version is the version of its format, major.minor; what follows them is not compared. The first builds stamped 1.0.0, the next 0.1.x,
    // and the format 1.0 came before 1.1, which adds the build section, and 1.2, which adds KeepSystemTheme and the Make section.
    [TestMethod]
    [DataRow(null, ProjectFileSchemaMatch.Unstamped)]
    [DataRow("", ProjectFileSchemaMatch.Unstamped)]
    [DataRow("1.2", ProjectFileSchemaMatch.Same)]
    [DataRow("1.2.0-rc3", ProjectFileSchemaMatch.Same)]
    [DataRow("1.1", ProjectFileSchemaMatch.Older, DisplayName = "1.1, the format before KeepSystemTheme and the Make section")]
    [DataRow("1.0", ProjectFileSchemaMatch.Older, DisplayName = "1.0, the format before the build section")]
    [DataRow("1.0.0", ProjectFileSchemaMatch.Older, DisplayName = "1.0.0, as the first builds stamped it")]
    [DataRow("0.1.3", ProjectFileSchemaMatch.Older, DisplayName = "0.1.3, as the next builds stamped it")]
    [DataRow("0.87.2-rc3", ProjectFileSchemaMatch.Older)]
    [DataRow("1.3", ProjectFileSchemaMatch.NewerMinor)]
    [DataRow("1.3.0+abc", ProjectFileSchemaMatch.NewerMinor)]
    [DataRow("2.0", ProjectFileSchemaMatch.NewerMajor)]
    [DataRow("2", ProjectFileSchemaMatch.NewerMajor, DisplayName = "a lone number is a major")]
    [DataRow("v1.0", ProjectFileSchemaMatch.Unparseable)]
    [DataRow("1.x", ProjectFileSchemaMatch.Unparseable)]
    [DataRow("99999999999.0", ProjectFileSchemaMatch.Unparseable, DisplayName = "a major no int holds")]
    public void AVersion_IsComparedWithThisPlatformsFormat_ByItsMajorAndMinor(string? version, ProjectFileSchemaMatch expected)
        => Assert.AreEqual(expected, ProjectFile.SchemaOf(version));

    // what a file says its version is is read whatever it is: it is never why a project file can't be read.
    [TestMethod]
    [DataRow("\"1.1\"", "1.1")]
    [DataRow("2", "2")]
    [DataRow("1.5", "1.5", DisplayName = "the number 1.5")]
    [DataRow("true", "true")]
    [DataRow("{ \"Major\": 2 }", "{ \"Major\": 2 }")]
    [DataRow("null", "")]
    public async Task AVersion_IsReadWhateverItIs(string written, string read)
        => Assert.AreEqual(read, (await ReadAsync($$"""{ "Version": {{written}}, "ProjectInfo": { "Name": "P" } }""")).Version);

    // a file a newer platform wrote opens in an older one, and keeps what the newer one added when the older one writes it: the members this model does not know,
    // at every level, are written back as they were read, and so is the newer format's stamp.
    [TestMethod]
    public async Task AFileOfANewerMinor_KeepsWhatThisPlatformDoesNotKnow_AndItsStamp_WhenItIsWritten()
    {
        const string json = """
            {
              "Version": "1.3",
              "Theme": { "Accent": "teal" },
              "ProjectInfo": {
                "Name": "Newer",
                "Kind": "Library",
                "References": [ { "Name": "Scripting", "Pinned": true } ],
                "Modules": [ "src/Plain.bas", { "RelativeUri": "src/Tagged.bas", "Tags": [ "core" ] }, { "RelativeUri": "Sheet1.doccls", "Super": "ExcelWorksheet", "Sheet": 1 } ],
                "OtherFiles": [ { "RelativeUri": "README.md", "Preview": true }, { "RelativeUri": "Book.xlsx", "Super": "ExcelWorkbook" } ]
              }
            }
            """;

        var project = await ReadAsync(json);
        var written = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(project.WithSchemaVersion()))!;

        Assert.AreEqual("1.3", written["Version"]!.GetValue<string>(), "the newer format's stamp is kept, since what it added is");
        Assert.AreEqual("""{"Accent":"teal"}""", written["Theme"]!.ToJsonString());
        var info = written["ProjectInfo"]!;
        Assert.AreEqual("Library", info["Kind"]!.GetValue<string>());
        Assert.IsTrue(info["References"]![0]!["Pinned"]!.GetValue<bool>());
        Assert.AreEqual("""["src/Plain.bas",{"RelativeUri":"src/Tagged.bas","Tags":["core"]},{"RelativeUri":"Sheet1.doccls","Super":"ExcelWorksheet","Sheet":1}]""",
            info["Modules"]!.ToJsonString());
        Assert.AreEqual("""[{"RelativeUri":"README.md","Preview":true},{"RelativeUri":"Book.xlsx","Super":"ExcelWorkbook"}]""", info["OtherFiles"]!.ToJsonString(),
            "a file that is no module has no document: what it says of one is kept as any member this model does not know");
    }

    // the build section, as files have it: what the project builds into, and the procedure its program starts at.
    [TestMethod]
    [DataRow("""{ "outputType": "Library" }""", ProjectOutputType.Library, null)]
    [DataRow("""{ "outputType": "library" }""", ProjectOutputType.Library, null, DisplayName = "Library, whatever its case")]
    [DataRow("""{ "outputType": "Application", "entryPoint": "Module1.Main" }""", ProjectOutputType.Application, "Module1.Main")]
    [DataRow("""{ "outputType": "ConsoleApplication", "entryPoint": "Game.Main" }""", ProjectOutputType.ConsoleApplication, "Game.Main")]
    [DataRow("""{ "outputType": "consoleapplication" }""", ProjectOutputType.ConsoleApplication, null, DisplayName = "ConsoleApplication, whatever its case")]
    [DataRow("""{ "outputType": "Console" }""", ProjectOutputType.Application, null, DisplayName = "Console alone, a value it does not know, is a program")]
    [DataRow("""{ "entryPoint": "Start" }""", ProjectOutputType.Application, "Start", DisplayName = "a program unless it says Library")]
    [DataRow("""{ "outputType": "Exe" }""", ProjectOutputType.Application, null, DisplayName = "an output type it does not know is a program")]
    [DataRow("""{ "outputType": 1, "entryPoint": 2 }""", ProjectOutputType.Application, null, DisplayName = "what is no string says nothing")]
    [DataRow("""{ "entryPoint": "  " }""", ProjectOutputType.Application, null, DisplayName = "a blank entry point is none")]
    [DataRow("""{ }""", ProjectOutputType.Application, null)]
    public async Task TheBuildSection_IsReadAsFilesHaveIt(string build, ProjectOutputType outputType, string? entryPoint)
    {
        var project = await ReadAsync($$"""{ "ProjectInfo": { "Name": "P", "build": {{build}} } }""");

        Assert.IsNotNull(project.ProjectInfo.Build);
        Assert.AreEqual(outputType, project.ProjectInfo.Build.OutputType);
        Assert.AreEqual(outputType is ProjectOutputType.Library, project.ProjectInfo.Build.IsLibrary);
        Assert.AreEqual(outputType is ProjectOutputType.ConsoleApplication, project.ProjectInfo.Build.IsConsoleApplication);
        Assert.AreEqual(entryPoint, project.ProjectInfo.Build.EntryPoint);
        Assert.IsFalse(project.ProjectInfo.ExtensionData?.ContainsKey("build") ?? false, "a member this model knows");
    }

    // a project file with no build section, or one that is no object, reads as a program that starts at its Sub Main, and is never refused for it.
    [TestMethod]
    [DataRow("""{ "ProjectInfo": { "Name": "P" } }""", DisplayName = "no section")]
    [DataRow("""{ "ProjectInfo": { "Name": "P", "build": null } }""", DisplayName = "a null section")]
    [DataRow("""{ "ProjectInfo": { "Name": "P", "build": "Library" } }""", DisplayName = "a section that is no object")]
    [DataRow("""{ "ProjectInfo": { "Name": "P", "build": [ { "outputType": "Library" } ] } }""", DisplayName = "an array")]
    public async Task AProjectWithNoBuildSection_HasNone(string json)
    {
        var project = await ReadAsync(json);

        Assert.IsNull(project.ProjectInfo.Build);
        Assert.AreEqual("P", project.ProjectInfo.Name);
    }

    // the section is written with the names files have it with, and only what it says: a project file without one is written without one, and what a newer
    // format adds to it is kept.
    [TestMethod]
    public async Task TheBuildSection_IsWrittenAsFilesHaveIt_AndWhatItDoesNotKnowIsKept()
    {
        var project = await ReadAsync("""{ "ProjectInfo": { "Name": "P", "build": { "outputType": "Library", "icon": "duck.ico" } } }""");

        var written = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(project.WithSchemaVersion()))!;

        Assert.AreEqual("""{"outputType":"Library","icon":"duck.ico"}""", written["ProjectInfo"]!["build"]!.ToJsonString());
        var program = new RDCoreProject { Name = "P", Build = new ProjectBuild { EntryPoint = "Module1.Main" } };
        Assert.AreEqual("""{"entryPoint":"Module1.Main"}""", System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(program))!["build"]!.ToJsonString(),
            "a program's output type is left out, as files leave it out");
        Assert.IsNull(System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new RDCoreProject { Name = "P" }))!["build"]);
    }

    // a console program's output type is written as ConsoleApplication, whatever case it was read in, and reads back as one.
    [TestMethod]
    [DataRow("ConsoleApplication")]
    [DataRow("CONSOLEAPPLICATION", DisplayName = "read in another case")]
    public async Task AConsoleProgramsOutputType_IsWritten_AndReadsBack(string read)
    {
        var project = await ReadAsync($$"""{ "Version": "1.2", "ProjectInfo": { "Name": "P", "build": { "outputType": "{{read}}" } } }""");

        var json = JsonSerializer.Serialize(project.WithSchemaVersion());
        var again = await ReadAsync(json);

        Assert.AreEqual("""{"outputType":"ConsoleApplication"}""", System.Text.Json.Nodes.JsonNode.Parse(json)!["ProjectInfo"]!["build"]!.ToJsonString());
        Assert.AreEqual(ProjectOutputType.ConsoleApplication, again.ProjectInfo.Build?.OutputType);
        Assert.IsTrue(again.ProjectInfo.Build!.IsConsoleApplication);
        Assert.IsFalse(again.ProjectInfo.Build.IsLibrary, "a console program is run");
    }

    // a file of the format 1.0 that has a build section, a member that format did not know, reads it, and is stamped with this format when it is written.
    [TestMethod]
    public async Task AFileOfTheFormatBeforeTheBuildSection_ReadsItsSection_AndIsWrittenInThisFormat()
    {
        var project = await ReadAsync("""{ "Version": "1.0", "ProjectInfo": { "Name": "P", "build": { "entryPoint": "Two.Main" } } }""");

        Assert.AreEqual(ProjectFileSchemaMatch.Older, ProjectFile.SchemaOf(project.Version));
        Assert.AreEqual("Two.Main", project.ProjectInfo.Build?.EntryPoint);
        Assert.AreEqual(ProjectFile.SchemaVersion, project.WithSchemaVersion().Version);
    }

    // a file in an older format, or no format, is in this one once it is written: only a newer one keeps its stamp.
    [TestMethod]
    [DataRow("1.1", "1.2")]
    [DataRow("1.0", "1.2")]
    [DataRow("1.0.0", "1.2")]
    [DataRow("0.1.3", "1.2")]
    [DataRow("v1", "1.2")]
    [DataRow("", "1.2")]
    [DataRow("1.4", "1.4")]
    [DataRow("3.0", "3.0")]
    public void AWrittenFile_IsStampedWithThisPlatformsFormat_UnlessItIsInANewerOne(string version, string stamped)
        => Assert.AreEqual(stamped, new ProjectFile().WithVersion(version).WithSchemaVersion().Version);

    // KeepSystemTheme, as files have it: true is set, and anything else, or nothing, is not; a project file is never refused for it.
    [TestMethod]
    [DataRow("""{ "Name": "P", "KeepSystemTheme": true }""", true)]
    [DataRow("""{ "Name": "P", "KeepSystemTheme": false }""", false)]
    [DataRow("""{ "Name": "P" }""", false, DisplayName = "a project that does not have it")]
    [DataRow("""{ "Name": "P", "KeepSystemTheme": null }""", false, DisplayName = "null")]
    [DataRow("""{ "Name": "P", "KeepSystemTheme": "true" }""", false, DisplayName = "a string")]
    [DataRow("""{ "Name": "P", "KeepSystemTheme": 1 }""", false, DisplayName = "a number")]
    [DataRow("""{ "Name": "P", "KeepSystemTheme": { "Value": true } }""", false, DisplayName = "an object")]
    public async Task KeepSystemTheme_IsReadAsFilesHaveIt(string info, bool keepSystemTheme)
    {
        var project = await ReadAsync($$"""{ "Version": "1.2", "ProjectInfo": {{info}} }""");

        Assert.AreEqual(keepSystemTheme, project.ProjectInfo.KeepSystemTheme);
        Assert.AreEqual("P", project.ProjectInfo.Name);
        Assert.IsFalse(project.ProjectInfo.ExtensionData?.ContainsKey(nameof(RDCoreProject.KeepSystemTheme)) ?? false, "a member this model knows");
    }

    // it is written only when it is set, and reads back as it was written.
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task KeepSystemTheme_IsWrittenOnlyWhenItIsSet_AndReadsBack(bool keepSystemTheme)
    {
        var project = new ProjectFile(Root, new RDCoreProject { Name = "P" }).WithKeepSystemTheme(keepSystemTheme).WithSchemaVersion();

        var json = JsonSerializer.Serialize(project);
        var info = System.Text.Json.Nodes.JsonNode.Parse(json)!["ProjectInfo"]!.AsObject();
        var read = await ReadAsync(json);

        Assert.AreEqual(keepSystemTheme, info.ContainsKey(nameof(RDCoreProject.KeepSystemTheme)), json);
        Assert.AreEqual(keepSystemTheme ? "true" : null, info[nameof(RDCoreProject.KeepSystemTheme)]?.ToJsonString());
        Assert.AreEqual(keepSystemTheme, read.ProjectInfo.KeepSystemTheme);
        Assert.AreEqual(ProjectFile.SchemaVersion, read.Version);
    }

    // a file of the format 1.1, which has no KeepSystemTheme, loads with the light colors, and is written in this format without the member.
    [TestMethod]
    public async Task AFileOfTheFormatBeforeKeepSystemTheme_ReadsItAsNotSet_AndIsWrittenInThisFormat()
    {
        var project = await ReadAsync("""{ "Version": "1.1", "ProjectInfo": { "Name": "P", "Modules": [ "Module1.bas" ], "build": { "entryPoint": "Module1.Main" } } }""");

        Assert.AreEqual(ProjectFileSchemaMatch.Older, ProjectFile.SchemaOf(project.Version));
        Assert.IsFalse(project.ProjectInfo.KeepSystemTheme);
        Assert.AreEqual("Module1.Main", project.ProjectInfo.Build?.EntryPoint);
        var written = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(project.WithSchemaVersion()))!;
        Assert.AreEqual("1.2", written["Version"]!.GetValue<string>());
        Assert.IsNull(written["ProjectInfo"]!["KeepSystemTheme"]);
    }

    // the Make section, as files have it: each member that says something is read, and the others are none.
    [TestMethod]
    public async Task TheMakeSection_IsReadAsFilesHaveIt()
    {
        var project = await ReadAsync("""
            {
              "Version": "1.2",
              "ProjectInfo": {
                "Name": "P",
                "Make": {
                  "Title": "Naval", "Major": 2, "Minor": 10, "Revision": 9999,
                  "Comments": "c", "CompanyName": "Contoso", "FileDescription": "d",
                  "LegalCopyright": "(c)", "LegalTrademarks": "tm", "ProductName": "Naval Game"
                }
              }
            }
            """);

        var make = project.ProjectInfo.Make;
        Assert.IsNotNull(make);
        Assert.AreEqual(("Naval", (short)2, (short)10, (short)9999), (make.Title, make.Major, make.Minor, make.Revision),
            "9999, the highest the Make tab takes");
        CollectionAssert.AreEqual(new[] { "c", "Contoso", "d", "(c)", "tm", "Naval Game" },
            new[] { make.Comments, make.CompanyName, make.FileDescription, make.LegalCopyright, make.LegalTrademarks, make.ProductName });
        Assert.IsNull(make.ExtensionData);
        Assert.IsFalse(project.ProjectInfo.ExtensionData?.ContainsKey(nameof(RDCoreProject.Make)) ?? false, "a member this model knows");
    }

    // what is not what a member is says nothing, and a project file is never refused for it.
    [TestMethod]
    [DataRow("""{ "Major": "1" }""", DisplayName = "a version number that is a string")]
    [DataRow("""{ "Major": 1.5 }""", DisplayName = "a version number with a fraction")]
    [DataRow("""{ "Major": -1 }""", DisplayName = "a negative version number")]
    [DataRow("""{ "Major": 10000 }""", DisplayName = "a major version above 9999")]
    [DataRow("""{ "Minor": 10000 }""", DisplayName = "a minor version above 9999")]
    [DataRow("""{ "Revision": 32767 }""", DisplayName = "a revision above 9999")]
    [DataRow("""{ "Major": 32768 }""", DisplayName = "a version number no Integer holds")]
    [DataRow("""{ "Major": null }""", DisplayName = "a null version number")]
    [DataRow("""{ "Title": 1 }""", DisplayName = "a title that is a number")]
    [DataRow("""{ "CompanyName": true }""", DisplayName = "a version string that is a Boolean")]
    [DataRow("""{ "ProductName": [ "P" ] }""", DisplayName = "a version string that is an array")]
    [DataRow("""{ }""", DisplayName = "an empty section")]
    public async Task AMemberOfTheMakeSection_ThatIsNotWhatItIs_IsNone(string make)
    {
        var project = await ReadAsync($$"""{ "Version": "1.2", "ProjectInfo": { "Name": "P", "Make": {{make}} } }""");

        Assert.IsNotNull(project.ProjectInfo.Make);
        Assert.IsNull(project.ProjectInfo.Make.Major);
        Assert.IsNull(project.ProjectInfo.Make.Minor);
        Assert.IsNull(project.ProjectInfo.Make.Revision);
        Assert.IsNull(project.ProjectInfo.Make.Title);
        Assert.IsNull(project.ProjectInfo.Make.CompanyName);
        Assert.IsNull(project.ProjectInfo.Make.ProductName);
        Assert.IsNull(project.ProjectInfo.Make.ExtensionData, "a member this model knows");
    }

    // a project file with no Make section, or one that is not an object, has none.
    [TestMethod]
    [DataRow("""{ "Name": "P" }""", DisplayName = "no section")]
    [DataRow("""{ "Name": "P", "Make": null }""", DisplayName = "a null section")]
    [DataRow("""{ "Name": "P", "Make": "Naval" }""", DisplayName = "a section that is a string")]
    [DataRow("""{ "Name": "P", "Make": [ { "Title": "Naval" } ] }""", DisplayName = "an array")]
    public async Task AProjectWithNoMakeSection_HasNone(string info)
    {
        var project = await ReadAsync($$"""{ "Version": "1.2", "ProjectInfo": {{info}} }""");

        Assert.IsNull(project.ProjectInfo.Make);
        Assert.AreEqual("P", project.ProjectInfo.Name);
    }

    // the section is written only when the project has one, with only the members it says, a string kept as it is, empty or not; it reads back as it was
    // written, and what a newer format adds to it is kept.
    [TestMethod]
    public async Task TheMakeSection_IsWrittenOnlyWhenItIsSet_WithWhatItSays_AndReadsBack()
    {
        var make = new ProjectMake { Title = "Naval", Major = 1, Minor = 0, CompanyName = "" };
        var project = new ProjectFile(Root, new RDCoreProject { Name = "P" }).WithMake(make).WithSchemaVersion();

        var json = JsonSerializer.Serialize(project);
        var read = await ReadAsync(json);
        var none = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new RDCoreProject { Name = "P" }))!.AsObject();
        var newer = await ReadAsync("""{ "Version": "1.3", "ProjectInfo": { "Name": "P", "Make": { "Title": "Naval", "Icon": "duck.ico" } } }""");

        Assert.AreEqual("""{"Title":"Naval","Major":1,"Minor":0,"CompanyName":""}""",
            System.Text.Json.Nodes.JsonNode.Parse(json)!["ProjectInfo"]!["Make"]!.ToJsonString());
        Assert.AreEqual(make, read.ProjectInfo.Make with { ExtensionData = null });
        Assert.AreEqual(ProjectFile.SchemaVersion, read.Version);
        Assert.IsFalse(none.ContainsKey(nameof(RDCoreProject.Make)), none.ToJsonString());
        Assert.AreEqual("""{"Title":"Naval","Icon":"duck.ico"}""",
            System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(newer.WithSchemaVersion()))!["ProjectInfo"]!["Make"]!.ToJsonString());
    }

    // a file of the format 1.1, which has no Make section, has none, and is written in this format without one.
    [TestMethod]
    public async Task AFileOfTheFormatBeforeTheMakeSection_HasNone_AndIsWrittenInThisFormat()
    {
        var project = await ReadAsync("""{ "Version": "1.1", "ProjectInfo": { "Name": "P", "Modules": [ "Module1.bas" ] } }""");

        Assert.AreEqual(ProjectFileSchemaMatch.Older, ProjectFile.SchemaOf(project.Version));
        Assert.IsNull(project.ProjectInfo.Make);
        var written = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(project.WithSchemaVersion()))!;
        Assert.AreEqual("1.2", written["Version"]!.GetValue<string>());
        Assert.IsNull(written["ProjectInfo"]!["Make"]);
    }

    // a platform of the format 1.1, whose model has no Make, keeps the section as a member it does not know, and writes it back as it was read.
    [TestMethod]
    public void TheMakeSection_IsKeptAsRead_ByAModelThatDoesNotKnowIt()
    {
        const string json = """{"Name":"P","Make":{"Title":"Naval","Major":3,"Extra":[1]}}""";

        var older = JsonSerializer.Deserialize<ProjectOfTheFormat11>(json)!;

        Assert.IsTrue(older.ExtensionData!.ContainsKey(nameof(RDCoreProject.Make)));
        Assert.AreEqual(json, JsonSerializer.Serialize(older));
    }

    // what a platform of the format 1.1 has of a project, for what it does not know: its name, and the members it keeps as they were read.
    private sealed record class ProjectOfTheFormat11
    {
        public string Name { get; init; } = string.Empty;

        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; init; }
    }

    [TestMethod]
    [DataRow(null, "Attribute VB_Name = \"Module1\"\r\n", ProjectModuleKind.Standard)]
    [DataRow(null, "VERSION 1.0 CLASS\r\nBEGIN\r\nEND\r\nAttribute VB_Name = \"Class1\"\r\n", ProjectModuleKind.Class)]
    [DataRow(DocClassType.ExcelWorksheet, "Attribute VB_Name = \"Sheet1\"\r\n", ProjectModuleKind.Document)]
    [DataRow(DocClassType.Unknown, "VERSION 1.0 CLASS\r\n", ProjectModuleKind.Document)]
    [DataRow(null, null, ProjectModuleKind.Standard)]
    public void AModulesKind_IsTheProjectsWordForItsDocument_ElseWhatItsTextSays(DocClassType? super, string? source, ProjectModuleKind expected)
    {
        Assert.AreEqual(expected, ProjectModuleKinds.Of(super, source));
        Assert.AreEqual(expected, ProjectModuleKinds.Of(new RDCoreModule { RelativeUri = "M.cls", Super = super }, source));
    }

    [TestMethod]
    [DataRow("utf-8", true)]
    [DataRow("utf-8", false)]
    [DataRow("utf-16", true)]
    [DataRow("utf-16BE", true)]
    public void AModulesText_IsReadAsThePlatformReadsAFile_AndAClassModuleBehindAMarkIsOne(string encodingName, bool byteOrderMark)
    {
        const string text = "VERSION 1.0 CLASS\r\nBEGIN\r\nEND\r\nAttribute VB_Name = \"Modèle\"\r\n";
        var encoding = System.Text.Encoding.GetEncoding(encodingName);
        byte[] bytes = [.. byteOrderMark ? encoding.GetPreamble() : [], .. encoding.GetBytes(text)];
        var path = Path.Combine(Path.GetTempPath(), $"rdcore-module-text-{Guid.NewGuid():N}.cls");
        try
        {
            File.WriteAllBytes(path, bytes);

            var read = ProjectModuleKinds.ReadText(bytes);

            Assert.AreEqual(File.ReadAllText(path), read);
            Assert.AreEqual(text, read);
            Assert.AreEqual(ProjectModuleKind.Class, ProjectModuleKinds.Of((DocClassType?)null, read));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
