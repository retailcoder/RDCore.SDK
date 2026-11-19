using System.IO.Abstractions.TestingHelpers;
using NSubstitute;
using RDCore.SDK.Runtime.Libraries;
using RDCore.SDK.Workspace;
using RDCore.SDK.Workspace.Forms;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The designed modules of a VB6 project besides its forms: a UserControl (<c>.ctl</c>), a PropertyPage (<c>.pag</c>) and a UserDocument (<c>.dob</c>) are
/// source files of the language and modules of the project, whose code is built on the class their designer block names. A Designer (<c>.dsr</c>) is not.
/// </summary>
[TestClass]
public sealed class VisualBasic6DesignedModuleTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-vb6-designed-ws");

    // a designed module as VB6 writes it: the VERSION header, the designer block of its root, and its code.
    private static string Designed(string rootClass, string name)
        => $"VERSION 5.00\r\nBegin {rootClass} {name} \r\n   ClientHeight    =   3600\r\n   ClientWidth     =   4800\r\nEnd\r\n"
            + $"Attribute VB_Name = \"{name}\"\r\nOption Explicit\r\n\r\nPrivate Sub UserControl_Resize()\r\n    BackColor = vbRed\r\nEnd Sub\r\n";

    [TestMethod]
    [DataRow("*.ctl")]
    [DataRow("*.pag")]
    [DataRow("*.dob")]
    public void ADesignedModule_IsASourceFileOfVB6(string fileType)
        => CollectionAssert.Contains(SupportedLanguages.VB6.FileTypes.ToArray(), fileType);

    [TestMethod]
    public void ADesigner_IsNoSourceFileOfVB6()
        => CollectionAssert.DoesNotContain(SupportedLanguages.VB6.FileTypes.ToArray(), "*.dsr");

    [TestMethod]
    [DataRow("VB.UserControl", "Gauge")]
    [DataRow("VB.PropertyPage", "GaugePage")]
    [DataRow("VB.UserDocument", "GaugeDocument")]
    public void ADesignedModule_IsAClassModule_BuiltOnTheClassItsDesignerBlockNames(string rootClass, string name)
    {
        var source = Designed(rootClass, name);

        Assert.AreEqual(ProjectModuleKind.Class, ProjectModuleKinds.Of(super: null, source));
        Assert.AreEqual(name, ModuleName.Resolve(source, $"{name}.ctl"));
        Assert.AreEqual(rootClass, FormDesigner.Read(source)!.BaseClass?.ToString());
    }

    [TestMethod]
    public async Task AVB6Folder_HasItsUserControlsPropertyPagesAndUserDocuments_AsModules_AndNoDesigner()
    {
        var fs = new MockFileSystem();
        fs.AddDirectory(Root);
        fs.AddFile(Path.Combine(Root, "Gauge.ctl"), new MockFileData(Designed("VB.UserControl", "Gauge")));
        fs.AddFile(Path.Combine(Root, "GaugeDocument.dob"), new MockFileData(Designed("VB.UserDocument", "GaugeDocument")));
        fs.AddFile(Path.Combine(Root, "GaugePage.pag"), new MockFileData(Designed("VB.PropertyPage", "GaugePage")));
        fs.AddFile(Path.Combine(Root, "Report.dsr"), new MockFileData("VERSION 5.00\r\nBegin {78E93846-85FD-11D0-8487-00A0C90DC8A9} Report \r\nEnd\r\n"));

        var project = await new ProjectFileLoader(fs).LoadOrSynthesizeAsync(Root, SupportedLanguages.VB6);

        CollectionAssert.AreEqual(new[] { "Gauge.ctl", "GaugeDocument.dob", "GaugePage.pag" },
            project.ProjectInfo.Modules.Select(module => module.RelativeUri).ToArray());
    }

    [TestMethod]
    public void AVbp_ListsItsUserControlsPropertyPagesAndUserDocuments_AsModules_AndADesigner_AsAFileOfTheProject()
    {
        const string vbp = "Type=Control\r\nUserControl=Gauge.ctl\r\nPropertyPage=GaugePage.pag\r\nUserDocument=GaugeDocument.dob\r\n"
            + "Designer=Report.dsr\r\nName=\"Gauges\"\r\n";

        var import = VbpProjectImporter.Import(vbp, "Gauges.vbp", Substitute.For<ILibrarySource>());

        CollectionAssert.AreEqual(new[] { "Gauge.ctl", "GaugePage.pag", "GaugeDocument.dob" },
            import.Project.Modules.Select(module => module.RelativeUri).ToArray());
        CollectionAssert.AreEqual(new[] { "Report.dsr" }, import.Project.OtherFiles.Select(file => file.RelativeUri).ToArray());
        CollectionAssert.Contains(import.Notes.ToArray(), new VbpImportNote(VbpImportNoteKind.NotAModule, "Report.dsr"));
    }
}
