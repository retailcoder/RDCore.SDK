using System.IO.Abstractions.TestingHelpers;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Runtime.Libraries;
using RDCore.SDK.Workspace;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.SDK.Tests.Extensibility;

/// <summary>
/// A fix that changes the project: what it says it changes, carried in a diagnostic's <c>data</c>, and the project it makes.
/// </summary>
[TestClass]
public sealed class ProjectFixTests
{
    private const string ExcelGuid = "00020813-0000-0000-c000-000000000046";

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-project-fix");
    private static readonly string Symbols = Path.Combine(Path.GetTempPath(), "rdcore-project-fix-symbols");
    private static readonly Uri Sheet1 = new(Path.Combine(Root, "src", "Sheet1.doccls"));

    private static ILibrarySource Libraries() => new DirectoryLibrarySource(new MockFileSystem(new Dictionary<string, MockFileData>
    {
        [Path.Combine(Symbols, "Excel.json")] = new("""{ "formatVersion": 1, "name": "Excel", "origin": { "guid": "{00020813-0000-0000-C000-000000000046}", "version": "1.9" } }"""),
    }), Symbols);

    private static ProjectFile Project(RDCoreReference[] references, params RDCoreModule[] modules)
        => new(Root, new RDCoreProject { Name = "Book1", References = references, Modules = modules });

    private static readonly RDCoreReference Vba = new() { Name = RDCoreReference.StandardLibraryName, Guid = RDCoreReference.StandardLibraryGuid, IsUnremovable = true };
    private static readonly RDCoreReference Scripting = new() { Name = "Scripting" };

    [TestMethod]
    public void AProjectFix_IsReadBack_WithItsChanges_InTheirOrder()
    {
        var fixes = new DiagnosticFixes
        {
            Project = new ProjectFix
            {
                Title = "Reference the Excel library",
                Changes = [ProjectChange.AddReference("Excel"), ProjectChange.SetDocumentClass(DocClassType.ExcelWorksheet), ProjectChange.RemoveModule()],
            },
        };

        var read = DiagnosticFixes.Of(new Diagnostic { Range = new Range(0, 0, 0, 1), Message = "finding", Data = fixes.ToData() });

        Assert.IsNotNull(read?.Project);
        Assert.AreEqual("Reference the Excel library", read.Project.Title);
        Assert.IsFalse(read.Project.ChangesBehavior);
        CollectionAssert.AreEqual(
            new[] { ProjectChange.AddReference("Excel"), ProjectChange.SetDocumentClass(DocClassType.ExcelWorksheet), ProjectChange.RemoveModule() },
            read.Project.Changes.ToArray());
    }

    [TestMethod]
    public void AProjectFix_WritesItsKindsAndClasses_ByName()
    {
        var data = new DiagnosticFixes { Project = new ProjectFix { Changes = [ProjectChange.SetDocumentClass(DocClassType.ExcelWorkbook)] } }.ToData();

        Assert.AreEqual("SetDocumentClass", (string?)data["project"]?["changes"]?[0]?["kind"]);
        Assert.AreEqual("ExcelWorkbook", (string?)data["project"]?["changes"]?[0]?["documentClass"]);
        Assert.IsNull(data["project"]?["changes"]?[0]?["library"]);
    }

    [TestMethod]
    public void AReferenceThatIsAdded_ComesAfterTheProjectsReferences_AndIsToTheTypeLibraryOfTheDescription()
    {
        var fix = new ProjectFix { Changes = [ProjectChange.AddReference("excel")] };

        var changed = fix.ApplyTo(Project([Vba, Scripting]), Sheet1, Libraries(), out var refused);

        Assert.IsNull(refused);
        Assert.IsNotNull(changed);
        Assert.IsTrue(changed.IsDirty);
        CollectionAssert.AreEqual(new[] { "VBA", "Scripting", "Excel" }, changed.ProjectInfo.References.Select(reference => reference.Name).ToArray());
        var excel = changed.ProjectInfo.References[^1];
        Assert.AreEqual(Guid.Parse(ExcelGuid), excel.Guid);
        Assert.AreEqual(1, excel.Major);
        Assert.AreEqual(9, excel.Minor);
    }

    [TestMethod]
    public void AProjectThatReferencesTheLibraryAlready_IsNotChanged()
    {
        var project = Project([Vba, new RDCoreReference { Name = "EXCEL" }]);

        var changed = new ProjectFix { Changes = [ProjectChange.AddReference("Excel")] }.ApplyTo(project, Sheet1, Libraries(), out var refused);

        Assert.IsNull(refused);
        Assert.AreSame(project, changed);
    }

    [TestMethod]
    public void ALibraryNoDescriptionDescribes_IsNotReferenced_AndNoChangeIsMade()
    {
        var fix = new ProjectFix { Changes = [ProjectChange.AddReference("Excel"), ProjectChange.AddReference("Word")] };

        var changed = fix.ApplyTo(Project([Vba]), Sheet1, Libraries(), out var refused);

        Assert.IsNull(changed);
        Assert.AreEqual(ProjectChange.AddReference("Word"), refused);
    }

    [TestMethod]
    [DataRow(@"src\Sheet1.doccls", DisplayName = "written with backslashes")]
    [DataRow("src/Sheet1.doccls", DisplayName = "written with slashes")]
    [DataRow("./src/Sheet1.doccls", DisplayName = "written from the project's folder")]
    public void TheModuleOfTheFinding_IsTheOneTheProjectListsAtItsPath_AndIsNoLongerListed(string written)
    {
        var module1 = new RDCoreModule { RelativeUri = "src/Module1.bas" };
        var project = Project([Vba], module1, new RDCoreModule { RelativeUri = written, Super = DocClassType.ExcelWorksheet });

        var changed = new ProjectFix { Changes = [ProjectChange.RemoveModule()] }.ApplyTo(project, Sheet1, Libraries(), out var refused);

        Assert.IsNull(refused);
        CollectionAssert.AreEqual(new[] { module1 }, changed!.ProjectInfo.Modules);
    }

    [TestMethod]
    public void TheModuleOfTheFinding_IsMadeTheCodeOfADocumentOfTheClass()
    {
        var module1 = new RDCoreModule { RelativeUri = "src/Module1.bas" };
        var project = Project([Vba], module1, new RDCoreModule { RelativeUri = "src/Sheet1.doccls" });

        var changed = new ProjectFix { Changes = [ProjectChange.SetDocumentClass(DocClassType.ExcelWorksheet)] }.ApplyTo(project, Sheet1, Libraries(), out _);

        Assert.IsNotNull(changed);
        Assert.IsTrue(changed.IsDirty);
        Assert.IsNull(changed.ProjectInfo.Modules[0].Super);
        Assert.AreEqual(DocClassType.ExcelWorksheet, changed.ProjectInfo.Modules[1].Super);
    }

    [TestMethod]
    [DataRow(ProjectChangeKind.RemoveModule)]
    [DataRow(ProjectChangeKind.SetDocumentClass)]
    public void AModuleTheProjectDoesNotList_CannotBeChanged(ProjectChangeKind kind)
    {
        var change = kind is ProjectChangeKind.RemoveModule ? ProjectChange.RemoveModule() : ProjectChange.SetDocumentClass(DocClassType.ExcelWorksheet);
        var project = Project([Vba], new RDCoreModule { RelativeUri = "src/Module1.bas" });

        var changed = new ProjectFix { Changes = [change] }.ApplyTo(project, Sheet1, Libraries(), out var refused);

        Assert.IsNull(changed);
        Assert.AreEqual(change, refused);
    }
}
