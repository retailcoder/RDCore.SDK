using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Server.Configuration;
using RDCore.SDK.Workspace;
using System.Text.Json;

namespace RDCore.Tests.Workspace;

/// <summary>
/// What kind a module of a project is: the project's word for a document module (<c>Super</c>), and the module's own text otherwise - the same for the
/// environment host and the language server.
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.2.3.3 RDCoreModule")]
public sealed class ModuleKindsTests
{
    private const string ClassModule = "VERSION 1.0 CLASS\r\nBEGIN\r\n  MultiUse = -1  'True\r\nEND\r\nAttribute VB_Name = \"C1\"\r\n";

    // what legacy Rubberduck exported of a worksheet's module: the text the code pane shows, with no header and no attribute.
    private const string LegacyDocumentExport = "'@Folder(\"Battleship.View\")\r\nOption Explicit\r\nPrivate Sub Worksheet_Activate()\r\n    Me.Range(\"A1\").Select\r\nEnd Sub\r\n";

    [TestMethod]
    [DataRow(DocClassType.ExcelWorksheet)]
    [DataRow(DocClassType.ExcelWorkbook)]
    [DataRow(DocClassType.WordDocument)]
    [DataRow(DocClassType.AccessForm)]
    [DataRow(DocClassType.Unknown)]
    public void AModuleTheProjectGivesASuper_IsADocumentModule_WhateverItsTextSays(DocClassType super)
    {
        Assert.AreEqual(ModuleType.DocumentModule, ModuleKinds.Of(new RDCoreModule { RelativeUri = "Sheet1.doccls", Super = super }, LegacyDocumentExport));
        Assert.AreEqual(ModuleType.DocumentModule, ModuleKinds.Of(new RDCoreModule { RelativeUri = "Sheet1.cls", Super = super }, ClassModule));
    }

    [TestMethod]
    public void AModuleWithNoSuper_IsTheKindItsTextSays_AndNoNameOrHandlerMakesItADocumentModule()
    {
        Assert.AreEqual(ModuleType.ClassModule, ModuleKinds.Of(new RDCoreModule { RelativeUri = "C1.cls" }, ClassModule));
        Assert.AreEqual(ModuleType.StdModule, ModuleKinds.Of(new RDCoreModule { RelativeUri = "ThisWorkbook.doccls" }, LegacyDocumentExport));
        Assert.AreEqual(ModuleType.StdModule, ModuleKinds.Of((RDCoreModule?)null, LegacyDocumentExport));
    }

    [TestMethod]
    public void ADocumentModule_IsAClassModule()
    {
        Assert.IsTrue(ModuleType.DocumentModule.IsClass());
        Assert.IsTrue(ModuleType.ClassModule.IsClass());
        Assert.IsFalse(ModuleType.StdModule.IsClass());
    }

    [TestMethod]
    public void ADocumentModule_IsNotCreatable_IsExtensible_AndHasNoDefaultInstance_WhateverItsTextSays()
    {
        var declared = new ModuleDirectives(Explicit: true, PredeclaredId: true, Creatable: true, Extensible: false);

        var directives = ModuleKinds.DirectivesOf(ModuleType.DocumentModule, declared);

        Assert.AreEqual(new ModuleDirectives(Explicit: true, PredeclaredId: false, Creatable: false, Extensible: true), directives);
        Assert.AreEqual(declared, ModuleKinds.DirectivesOf(ModuleType.ClassModule, declared), "a class module's attributes are its text's");
    }

    [TestMethod]
    public void EachKnownHostDocument_IsAnObjectOfAClassOfItsLibrary_AndAnUnknownOneOfNone()
    {
        Assert.AreEqual(new DocumentHostClass("Excel", "Worksheet"), ModuleKinds.HostClassOf(DocClassType.ExcelWorksheet));
        Assert.AreEqual(new DocumentHostClass("Excel", "Workbook"), ModuleKinds.HostClassOf(DocClassType.ExcelWorkbook));
        Assert.AreEqual(new DocumentHostClass("Word", "Document"), ModuleKinds.HostClassOf(DocClassType.WordDocument));
        Assert.AreEqual(new DocumentHostClass("Access", "Form"), ModuleKinds.HostClassOf(DocClassType.AccessForm));
        Assert.AreEqual(new DocumentHostClass("Access", "Report"), ModuleKinds.HostClassOf(DocClassType.AccessReport));
        Assert.IsNull(ModuleKinds.HostClassOf(DocClassType.Unknown));
    }

    // ---- the project file ----

    private static DocClassType? SuperRead(string super)
        => JsonSerializer.Deserialize<RDCoreModule>($$"""{ "RelativeUri": "Sheet1.doccls", "Super": {{super}} }""")!.Super;

    [TestMethod]
    [DataRow("2", DocClassType.ExcelWorksheet)]
    [DataRow("\"ExcelWorksheet\"", DocClassType.ExcelWorksheet)]
    [DataRow("\"excelworkbook\"", DocClassType.ExcelWorkbook)]
    [DataRow("5", DocClassType.WordDocument)]
    public void Super_IsReadByItsNameOrByItsNumber(string written, DocClassType expected)
        => Assert.AreEqual(expected, SuperRead(written));

    [TestMethod]
    [DataRow("\"PowerPointSlide\"")]
    [DataRow("\"2\"")]
    [DataRow("\"ExcelWorkbook, ExcelWorksheet\"")]
    [DataRow("\" ExcelWorksheet\"")]
    [DataRow("\"\"")]
    [DataRow("42")]
    public void ASuperRDVBADoesNotKnow_IsReadAsUnknown_AndTheProjectStillLoads(string written)
        => Assert.AreEqual(DocClassType.Unknown, SuperRead(written));

    [TestMethod]
    public void Super_IsWrittenByItsName_AndAModuleWithNoneIsItsPath()
    {
        StringAssert.Contains(JsonSerializer.Serialize(new RDCoreModule { RelativeUri = "Sheet1.doccls", Super = DocClassType.ExcelWorksheet }), "\"Super\":\"ExcelWorksheet\"");
        Assert.AreEqual("\"Module1.bas\"", JsonSerializer.Serialize(new RDCoreModule { RelativeUri = "Module1.bas" }));
        Assert.IsNull(SuperRead("null"));
    }

    [TestMethod]
    public void AFile_DeclaresItsOwnModuleFirst_ThenOneForEachBlock_NamedAndKindedAsItsHeaderSays()
    {
        const string source = "Attribute VB_Name = \"Backup\"\r\nClass Account\r\n    Attribute VB_Name = \"Ledger\"\r\nEnd Class\r\n"
            + "Module Logger\r\nEnd Module\r\n";

        var modules = ModuleKinds.ModulesOf(new RDCoreModule { RelativeUri = "Backup.vbs" }, source);

        CollectionAssert.AreEqual(new[]
            {
                new DeclaredModule("Backup", ModuleType.StdModule, IsBlock: false),
                new DeclaredModule("Account", ModuleType.ClassModule, IsBlock: true),
                new DeclaredModule("Logger", ModuleType.StdModule, IsBlock: true),
            },
            modules.ToArray());
    }

    // a block's declared module is partial, creatable, and has a visibility, as its header writes; the file's own module is the plain part, has no
    // header, and has none of them.
    [TestMethod]
    public void ABlocksModifiers_AreItsDeclaredModules_AndTheFilesOwnHasNone()
    {
        const string source = "Attribute VB_Name = \"Backup\"\r\nPublic Partial Class Account\r\nEnd Class\r\nCreatable Class Shape\r\nEnd Class\r\n"
            + "Private Module Logger\r\nEnd Module\r\nModule Plain\r\nEnd Module\r\n";

        var modules = ModuleKinds.ModulesOf(new RDCoreModule { RelativeUri = "Backup.vbs" }, source);

        CollectionAssert.AreEqual(new[]
            {
                new DeclaredModule("Backup", ModuleType.StdModule, IsBlock: false),
                new DeclaredModule("Account", ModuleType.ClassModule, IsBlock: true) { IsPartial = true, Visibility = AccessModifier.Public },
                new DeclaredModule("Shape", ModuleType.ClassModule, IsBlock: true) { IsCreatable = true },
                new DeclaredModule("Logger", ModuleType.StdModule, IsBlock: true) { Visibility = AccessModifier.Private },
                new DeclaredModule("Plain", ModuleType.StdModule, IsBlock: true),
            },
            modules.ToArray());
    }

    [TestMethod]
    public void AFileOfOneModule_DeclaresIt_AndAnUnreadOneIsNamedForItsFile()
    {
        var sheet = new RDCoreModule { RelativeUri = "Sheet1.cls", Super = DocClassType.ExcelWorksheet };

        CollectionAssert.AreEqual(new[] { new DeclaredModule("C1", ModuleType.ClassModule, IsBlock: false) },
            ModuleKinds.ModulesOf(new RDCoreModule { RelativeUri = "Class1.cls" }, ClassModule).ToArray());
        CollectionAssert.AreEqual(new[] { new DeclaredModule("Sheet1", ModuleType.DocumentModule, IsBlock: false) },
            ModuleKinds.ModulesOf(sheet, null).ToArray());
    }

    // a file declares modules in blocks only where it may have statements outside its procedures: a script always may, a VBA module only with the setting.
    [TestMethod]
    [DataRow("vba", "Backup.vbs", false, true, DisplayName = "a script, in a VBA workspace, the setting off")]
    [DataRow("vbscript", "Backup.vbs", false, true, DisplayName = "a script, in a VBScript workspace")]
    [DataRow("vba", "Backup.bas", false, false, DisplayName = "a VBA module, the setting off")]
    [DataRow("vba", "Backup.bas", true, true, DisplayName = "a VBA module, the setting on")]
    public void AFile_DeclaresBlocks_OnlyWhereItMayHaveStatementsOutsideItsProcedures(string language, string file, bool setting, bool declaresBlocks)
    {
        var workspace = new SdkWorkspaceOptions { Language = language, FileLevelStatements = setting };

        var modules = ModuleKinds.ModulesOf(new RDCoreModule { RelativeUri = file }, BackupWithBlocks, workspace);

        CollectionAssert.AreEqual(declaresBlocks ? new[] { "Backup", "Account", "Logger" } : new[] { "Backup" },
            modules.Select(module => module.Name).ToArray());
    }

    // with no options, the workspace is the default one: VBA, the setting off.
    [TestMethod]
    [DataRow("Backup.vbs", true, DisplayName = "a script")]
    [DataRow("Backup.bas", false, DisplayName = "a VBA module")]
    public void AFile_DeclaresBlocksWithNoOptions_AsInTheDefaultWorkspace(string file, bool declaresBlocks)
        => CollectionAssert.AreEqual(declaresBlocks ? new[] { "Backup", "Account", "Logger" } : new[] { "Backup" },
            ModuleKinds.ModulesOf(new RDCoreModule { RelativeUri = file }, BackupWithBlocks).Select(module => module.Name).ToArray());

    // a file's blocks by its path, as a parser has it: the address of a block's own module has none.
    [TestMethod]
    [DataRow("/c:/ws/Backup.vbs", false, true, DisplayName = "a script, the setting off")]
    [DataRow("/c:/ws/Backup.bas", false, false, DisplayName = "a VBA module, the setting off")]
    [DataRow("/c:/ws/Backup.bas", true, true, DisplayName = "a VBA module, the setting on")]
    [DataRow("/c:/ws/Backup.vbs/Logger", true, false, DisplayName = "the module of a block, the setting on")]
    public void AFilesBlocks_AreFound_OnlyWhereItMayHaveStatementsOutsideItsProcedures(string document, bool setting, bool hasBlocks)
        => CollectionAssert.AreEqual(hasBlocks ? new[] { "Account", "Logger" } : Array.Empty<string>(),
            ModuleKinds.BlocksOf(document, BackupWithBlocks, new SdkWorkspaceOptions { FileLevelStatements = setting }).Select(block => block.Name).ToArray());

    private const string BackupWithBlocks = "Attribute VB_Name = \"Backup\"\r\nClass Account\r\nEnd Class\r\nModule Logger\r\nEnd Module\r\n";

    [TestMethod]
    public void TheKindOfABlock_IsAStandardModuleForModule_AndAClassModuleForClass()
        => Assert.AreEqual((ModuleType.StdModule, ModuleType.ClassModule), (ModuleKinds.Of(FileBlockKind.Module), ModuleKinds.Of(FileBlockKind.Class)));
}
