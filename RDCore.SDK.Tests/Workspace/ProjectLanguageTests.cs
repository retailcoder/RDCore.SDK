using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The language a project's file says the project is written in: VB6 for a project imported from a <c>.vbp</c>, and none for any other.
/// </summary>
[TestClass]
public sealed class ProjectLanguageTests
{
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
}
