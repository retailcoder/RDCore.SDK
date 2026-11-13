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
    // and the format 1.0 came before 1.1, which adds the build section.
    [TestMethod]
    [DataRow(null, ProjectFileSchemaMatch.Unstamped)]
    [DataRow("", ProjectFileSchemaMatch.Unstamped)]
    [DataRow("1.1", ProjectFileSchemaMatch.Same)]
    [DataRow("1.1.0-rc3", ProjectFileSchemaMatch.Same)]
    [DataRow("1.0", ProjectFileSchemaMatch.Older, DisplayName = "1.0, the format before the build section")]
    [DataRow("1.0.0", ProjectFileSchemaMatch.Older, DisplayName = "1.0.0, as the first builds stamped it")]
    [DataRow("0.1.3", ProjectFileSchemaMatch.Older, DisplayName = "0.1.3, as the next builds stamped it")]
    [DataRow("0.87.2-rc3", ProjectFileSchemaMatch.Older)]
    [DataRow("1.2", ProjectFileSchemaMatch.NewerMinor)]
    [DataRow("1.2.0+abc", ProjectFileSchemaMatch.NewerMinor)]
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
              "Version": "1.2",
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

        Assert.AreEqual("1.2", written["Version"]!.GetValue<string>(), "the newer format's stamp is kept, since what it added is");
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

    // a file of the format 1.0 that has a build section - a member it did not know - reads it, and is stamped 1.1 when it is written.
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
    [DataRow("1.0", "1.1")]
    [DataRow("1.0.0", "1.1")]
    [DataRow("0.1.3", "1.1")]
    [DataRow("v1", "1.1")]
    [DataRow("", "1.1")]
    [DataRow("1.4", "1.4")]
    [DataRow("3.0", "3.0")]
    public void AWrittenFile_IsStampedWithThisPlatformsFormat_UnlessItIsInANewerOne(string version, string stamped)
        => Assert.AreEqual(stamped, new ProjectFile().WithVersion(version).WithSchemaVersion().Version);

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
