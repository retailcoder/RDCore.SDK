using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The language a project says it is written in: VB6 for a project imported from a <c>.vbp</c>, VBScript for a project of scripts, and none for any other.
/// </summary>
[TestClass]
public sealed class ProjectLanguageTests
{
    private static RDCoreProject Of(params string[] modules) => new() { Modules = [.. modules.Select(path => new RDCoreModule { RelativeUri = path })] };

    [TestMethod]
    [DataRow("Battleship.vbp", DisplayName = "a .vbp")]
    [DataRow("Projects/Battleship.VBP", DisplayName = "a .vbp in a folder, in capitals")]
    public void AProjectImportedFromAVisualBasic6Project_IsWrittenInVB6(string source)
        => Assert.AreSame(SupportedLanguages.VB6, SupportedLanguages.OfProject(new RDCoreProject { SourceProject = source }));

    [TestMethod]
    public void AProjectThatWasNotImported_SaysNoLanguage()
        => Assert.IsNull(SupportedLanguages.OfProject(new RDCoreProject()));

    [TestMethod]
    public void AProjectWhoseSourceIsNoVisualBasic6Project_SaysNoLanguage()
        => Assert.IsNull(SupportedLanguages.OfProject(new RDCoreProject { SourceProject = "Battleship.vbg" }));

    [TestMethod]
    [DataRow(new[] { "Backup.vbs" }, DisplayName = "a script")]
    [DataRow(new[] { "Scripts/Backup.VBS", "Scripts/Restore.vbs" }, DisplayName = "two scripts in a folder, one in capitals")]
    public void AProjectOfScripts_IsWrittenInVBScript(string[] modules)
        => Assert.AreSame(SupportedLanguages.VBScript, SupportedLanguages.OfProject(Of(modules)));

    [TestMethod]
    public void AProjectWithAModuleThatIsNoScript_SaysNoLanguage()
        => Assert.IsNull(SupportedLanguages.OfProject(Of("Backup.vbs", "Module1.bas")));
}
