using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The files written in a language: those whose extension is one of its file types, whatever its case and wherever the file is.
/// </summary>
[TestClass]
public sealed class SupportedLanguageSourceFileTests
{
    [TestMethod]
    [DataRow("vba", "Module1.bas", true, DisplayName = "a VBA standard module")]
    [DataRow("vba", "Sources/Class1.CLS", true, DisplayName = "a VBA class module in a folder, in capitals")]
    [DataRow("vba", "UserForm1.frm", true, DisplayName = "a VBA form")]
    [DataRow("vba", "ThisWorkbook.doccls", true, DisplayName = "a VBA document module")]
    [DataRow("vba", "UserForm1.frx", false, DisplayName = "a VBA form's binary file")]
    [DataRow("vba", "UserControl1.ctl", false, DisplayName = "a VB6 UserControl, in VBA")]
    [DataRow("vb6", "UserControl1.ctl", true, DisplayName = "a VB6 UserControl")]
    [DataRow("vb6", "ThisWorkbook.doccls", false, DisplayName = "a VBA document module, in VB6")]
    [DataRow("basic", "Program.rdc", true, DisplayName = "a BASIC program")]
    [DataRow("basic", "Module1.bas", false, DisplayName = "a .bas, in BASIC")]
    [DataRow("vbscript", "Scripts/Backup.VBS", true, DisplayName = "a VBScript script in a folder, in capitals")]
    [DataRow("vbscript", "Module1.bas", false, DisplayName = "a .bas, in VBScript")]
    [DataRow("vba", "Backup.vbs", false, DisplayName = "a script, in VBA")]
    [DataRow("vba", "bas", false, DisplayName = "a file named as an extension, with none")]
    [DataRow("vba", "Module1.bas.bak", false, DisplayName = "a copy of a module")]
    public void AFile_IsWrittenInALanguage_WhenItsExtensionIsOneOfTheLanguages(string language, string path, bool expected)
        => Assert.AreEqual(expected, SupportedLanguages.Get(language).IsSourceFile(path));

    // a BASIC's modules are RD-VBA's, without the code of an RDForms form; any other language's are its own files.
    [TestMethod]
    [DataRow("basic", "Module1.bas", true, DisplayName = "a standard module, in BASIC")]
    [DataRow("basic", "Class1.cls", true, DisplayName = "a class module, in BASIC")]
    [DataRow("basic", "UserForm1.frm", true, DisplayName = "a UserForm, in BASIC")]
    [DataRow("basic", "Sheet1.doccls", true, DisplayName = "a document module, in BASIC")]
    [DataRow("basic", "Program.rdc", false, DisplayName = "a BASIC program")]
    [DataRow("basic", "Form1.rdform", false, DisplayName = "an RDForms form, in BASIC")]
    [DataRow("vba", "Form1.rdform", true, DisplayName = "an RDForms form, in VBA")]
    [DataRow("vb6", "UserControl1.ctl", true, DisplayName = "a VB6 UserControl")]
    [DataRow("vb6", "ThisWorkbook.doccls", false, DisplayName = "a VBA document module, in VB6")]
    [DataRow("vbscript", "Backup.vbs", true, DisplayName = "a script, in VBScript")]
    public void AFile_IsAModuleOfAWorkspace_WhenItsExtensionIsOneOfTheLanguagesModules(string language, string path, bool expected)
        => Assert.AreEqual(expected, SupportedLanguages.Get(language).IsModuleFile(path));
}
