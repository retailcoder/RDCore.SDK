using RDCore.SDK.Server.Configuration;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// Which documents may have statements outside their procedures: every document of a workspace that allows them, every document of a language whose files
/// always have them, and a script (<c>.vbs</c>) in any workspace. VBA and VB6 modules have none otherwise.
/// </summary>
[TestClass]
public sealed class FileLevelStatementsTests
{
    [TestMethod]
    [DataRow("vba", "file:///c:/ws/Module1.bas", false, DisplayName = "a VBA module")]
    [DataRow("vb6", "file:///c:/ws/Form1.frm", false, DisplayName = "a VB6 form")]
    [DataRow("vba", "file:///c:/ws/Scripts/Backup.VBS", true, DisplayName = "a script in a VBA workspace")]
    [DataRow("vbscript", "file:///c:/ws/Backup.vbs", true, DisplayName = "a script")]
    [DataRow("vbscript", "file:///c:/ws/Module1.bas", true, DisplayName = "any module of a VBScript workspace")]
    [DataRow("vbscript", "file:///c:/ws/Backup.vbs/Logger", false, DisplayName = "the module of a block of a script")]
    public void ADocument_HasThemWhenItsLanguageDoes(string language, string document, bool expected)
        => Assert.AreEqual(expected, new SdkWorkspaceOptions { Language = language }.AllowsFileLevelStatements(document));

    [TestMethod]
    [DataRow("vba")]
    [DataRow("vb6")]
    public void AWorkspaceThatAllowsThem_HasThemInEveryDocument(string language)
        => Assert.IsTrue(new SdkWorkspaceOptions { Language = language, FileLevelStatements = true }
            .AllowsFileLevelStatements("file:///c:/ws/Module1.bas"));

    // the setting allows them in a VBA or VB6 module, which is then a script only when it writes one; a document of a language that always has them is one
    // whatever it writes.
    [TestMethod]
    [DataRow("vba", "file:///c:/ws/Module1.bas", false, DisplayName = "a VBA module, the setting on")]
    [DataRow("vba", "file:///c:/ws/Backup.vbs", true, DisplayName = "a script in a VBA workspace")]
    [DataRow("vbscript", "file:///c:/ws/Module1.bas", true, DisplayName = "any module of a VBScript workspace")]
    [DataRow("vbscript", "file:///c:/ws/Backup.vbs/Logger", false, DisplayName = "the module of a block of a script")]
    public void ADocument_AlwaysHasThem_OnlyWhereItsLanguageDoes(string language, string document, bool expected)
        => Assert.AreEqual(expected, new SdkWorkspaceOptions { Language = language, FileLevelStatements = true }.AlwaysHasFileLevelStatements(document));

    [TestMethod]
    public void OnlyVBScript_AlwaysHasThem()
        => CollectionAssert.AreEqual(new[] { SupportedLanguages.VBScript },
            SupportedLanguages.All.Where(language => language.HasFileLevelStatements).ToArray());
}
