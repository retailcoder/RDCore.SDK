using System.IO.Abstractions.TestingHelpers;
using System.Text;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Workspace;
using RDCore.SDK.Workspace.Forms;

namespace RDCore.Tests.Workspace.Forms;

/// <summary>
/// An RDForms form: its code is a <c>.rdform</c> class module with no <c>VERSION</c> header and no designer block, and its controls are in the <c>.xaml</c>
/// of the same base name beside it, which the reader registered for it reads.
/// </summary>
[TestClass]
public sealed class RDFormsModuleTests
{
    private const string Code = "Attribute VB_Name = \"Form1\"\r\nAttribute VB_PredeclaredId = True\r\nOption Explicit\r\n";

    // a document of lines: the form's name, then one control a line, its class and its name; "!" alone holds no form.
    private sealed class LinesReader : IFormDocumentReader
    {
        public string DocumentExtension => FormDesigner.RDFormsDocumentExtension;

        public FormDescriptor Read(ReadOnlyMemory<byte> document)
        {
            var text = Encoding.UTF8.GetString(document.Span);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines is ["!"])
            {
                throw new FormDesignerFormatException(FormDesignerFormatProblem.InvalidDocument);
            }

            var controls = lines.Skip(1).Select(line => line.Split(' ')).Select(parts => new FormControlDescriptor(parts[1], new FormControlClass("RDForms", parts[0]), null, null, []));
            return new FormDescriptor(lines[0], FormDesignerKind.RDForms, new FormControlClass("RDForms", "Form"), new Dictionary<string, string>(), [.. controls])
            {
                DesignerText = text,
            };
        }
    }

    private static readonly LinesReader Reader = new();

    private static (MockFileSystem FileSystem, string ModulePath) Workspace(string? document)
    {
        var modulePath = MockUnixSupport.Path(@"C:\work\forms\Form1.rdform");
        var files = new Dictionary<string, MockFileData> { [modulePath] = new(Code) };
        if (document is not null)
        {
            files[MockUnixSupport.Path(@"C:\work\forms\Form1.xaml")] = new(document);
        }

        return (new MockFileSystem(files), modulePath);
    }

    [TestMethod]
    [DataRow("Form1.rdform")]
    [DataRow("forms/Form1.RDFORM")]
    public void AnRDFormsModule_IsAClassModule_ByItsFile_ThoughItsTextHasNoVersionHeader(string relativeUri)
    {
        Assert.AreEqual(ModuleType.ClassModule, ModuleKinds.Of(new RDCoreModule { RelativeUri = relativeUri }, Code));
        Assert.AreEqual(ProjectModuleKind.Class, ProjectModuleKinds.Of(new RDCoreModule { RelativeUri = relativeUri }, Code));
        Assert.AreEqual(ModuleType.ClassModule, ModuleKinds.Of(super: null, Code, relativeUri));
        Assert.AreEqual(ModuleType.StdModule, ModuleKinds.Of(new RDCoreModule { RelativeUri = "Module1.bas" }, Code), "a .bas with the same text");
    }

    [TestMethod]
    public void AnRDFormsModule_WithNoFileToGoBy_IsWhatItsTextSays_AndAProjectsSuperStillWins()
    {
        Assert.AreEqual(ModuleType.StdModule, ModuleKinds.Of(super: null, Code));
        Assert.AreEqual(ModuleType.StdModule, ModuleKinds.Of(super: null, Code, path: null));
        Assert.AreEqual(ModuleType.DocumentModule, ModuleKinds.Of(new RDCoreModule { RelativeUri = "Form1.rdform", Super = DocClassType.Unknown }, Code));
        Assert.IsFalse(ProjectModuleKinds.IsRDFormsModule(null));
        Assert.IsFalse(ProjectModuleKinds.IsRDFormsModule("Form1.xaml"));
    }

    [TestMethod]
    public void AnRDFormsModule_IsAFileOfVBAAndOfVB6_AndItsDocumentIsAFileOfNoLanguage()
    {
        CollectionAssert.Contains(SupportedLanguages.RDVBA.FileTypes, "*.rdform");
        CollectionAssert.Contains(SupportedLanguages.VB6.FileTypes, "*.rdform");
        Assert.IsFalse(SupportedLanguages.All.SelectMany(language => language.FileTypes).Contains("*.xaml"));
        Assert.IsTrue(SupportedLanguageSelectors.DocumentSelector.Any(filter => filter.Pattern == "**/*.rdform"));
    }

    [TestMethod]
    [DataRow("forms/Form1.rdform", "forms/Form1.xaml")]
    [DataRow("Form1.RDFORM", "Form1.xaml")]
    [DataRow("my.forms/Main.Window.rdform", "my.forms/Main.Window.xaml")]
    public void AnRDFormsModulesDocument_IsTheXamlOfItsBaseNameBesideIt(string modulePath, string documentPath)
        => Assert.AreEqual(documentPath, FormDesigner.RDFormsDocumentOf(modulePath));

    [TestMethod]
    public void ReadCompanion_OfAnRDFormsModule_IsItsDocument()
    {
        var (fileSystem, modulePath) = Workspace("Form1\nTextBox Text1\n");

        var companion = FormDesigner.ReadCompanion(fileSystem, modulePath, Code, out var problem);

        Assert.IsNull(problem);
        Assert.AreEqual("Form1\nTextBox Text1\n", Encoding.UTF8.GetString(companion.Span));
    }

    [TestMethod]
    public void ReadCompanion_OfAnRDFormsModuleWithNoDocument_IsEmpty_AndSaysSo()
    {
        var (fileSystem, modulePath) = Workspace(document: null);

        var companion = FormDesigner.ReadCompanion(fileSystem, modulePath, Code, out var problem);

        Assert.IsTrue(companion.IsEmpty);
        Assert.AreEqual(FormDesignerFormatProblem.MissingDocument, (problem as FormDesignerFormatException)?.Problem);
    }

    [TestMethod]
    public void AnRDFormsModule_DeclaresWhatItsDocumentDoes_AsTheRegisteredReaderReadsIt()
    {
        FormDesigner.Register(Reader);
        var (fileSystem, modulePath) = Workspace("Form1\nTextBox Text1\nCommandButton Command1\n");
        var companion = FormDesigner.ReadCompanion(fileSystem, modulePath, Code, out _);

        var form = FormDesigner.Read(modulePath, Code, companion, out var problem);

        Assert.IsNull(problem);
        Assert.AreEqual(FormDesignerKind.RDForms, form!.Kind);
        CollectionAssert.AreEqual(new[] { "RDForms.TextBox Text1", "RDForms.CommandButton Command1" },
            form.DeclaredControls().Select(control => $"{control.Class} {control.Name}").ToArray());
        Assert.AreEqual("Form1\nTextBox Text1\nCommandButton Command1\n", form.DesignerText);
    }

    [TestMethod]
    public void AnRDFormsModule_WhoseDocumentIsMissingOrHoldsNoForm_DeclaresNothing_AndSaysWhy()
    {
        FormDesigner.Register(Reader);

        Assert.IsNull(FormDesigner.Read("Form1.rdform", Code, ReadOnlyMemory<byte>.Empty, out var missing));
        Assert.AreEqual(FormDesignerFormatProblem.MissingDocument, missing?.Problem);
        Assert.IsNull(FormDesigner.Read("Form1.rdform", Code, Encoding.UTF8.GetBytes("!"), out var invalid));
        Assert.AreEqual(FormDesignerFormatProblem.InvalidDocument, invalid?.Problem);
        Assert.IsFalse(string.IsNullOrWhiteSpace(missing!.Message));
        Assert.AreNotEqual(missing.Message, invalid!.Message);
        Assert.AreNotEqual(missing.Message, new FormDesignerFormatException(FormDesignerFormatProblem.NoDocumentReader).Message);
    }

    [TestMethod]
    public void AModuleThatIsNoRDFormsModule_IsReadByItsText_WhateverItsPath()
    {
        const string vb6 = "VERSION 5.00\r\nBegin VB.Form Form1\r\n   Caption = \"Form1\"\r\n   Begin VB.TextBox Text1\r\n   End\r\nEnd\r\nAttribute VB_Name = \"Form1\"\r\n";

        var form = FormDesigner.Read("Form1.frm", vb6, ReadOnlyMemory<byte>.Empty, out var problem);

        Assert.IsNull(problem);
        Assert.AreEqual(FormDesignerKind.VisualBasic6, form!.Kind);
        Assert.AreEqual("Text1", form.DeclaredControls().Single().Name);
        Assert.IsNull(FormDesigner.Read("Module1.bas", Code, ReadOnlyMemory<byte>.Empty, out _));
    }

    [TestMethod]
    [DataRow("Form1.rdform")]
    [DataRow("FORM1.RDFORM")]
    public void AnRDFormsModulesCompanion_IsItsDocument_AsItsExtensionSays_ThoughItsTextNamesNone(string fileName)
    {
        var modulePath = Path.Combine(Path.GetTempPath(), "rdcore-forms", fileName);

        Assert.AreEqual(".xaml", FormDesigner.CompanionExtensions[Path.GetExtension(fileName)]);
        Assert.AreEqual(Path.ChangeExtension(modulePath, ".xaml"), FormDesigner.CompanionPathOf(modulePath, Code));
        Assert.AreEqual(Path.ChangeExtension(modulePath, ".xaml"), FormDesigner.CompanionPathOf(modulePath, null), "with no text");
    }

    [TestMethod]
    [DataRow(FormDesignerKind.RDForms, true, DisplayName = "an RDForms form")]
    [DataRow(FormDesignerKind.VisualBasic6, true, DisplayName = "a Visual Basic 6.0 form")]
    [DataRow(FormDesignerKind.MSForms, false, DisplayName = "a UserForm")]
    [DataRow(FormDesignerKind.Other, false, DisplayName = "another designer's form")]
    public void AnRDFormsForm_KeepsVisualBasic6sFormModel_AsAVB6FormDoes(FormDesignerKind kind, bool model)
        => Assert.AreEqual(model, new FormDescriptor("Form1", kind, new FormControlClass(null, "Form"), new Dictionary<string, string>(), []).HasVisualBasic6FormModel);

    [TestMethod]
    public void Register_TakesAReader() => Assert.ThrowsExactly<ArgumentNullException>(() => FormDesigner.Register(null!));

    [TestMethod]
    [DataRow(FormDesignerKind.RDForms, "100", 75d, DisplayName = "an RDForms document, in DIP")]
    [DataRow(FormDesignerKind.VisualBasic6, "1500", 75d, DisplayName = "a designer block, in twips")]
    [DataRow(FormDesignerKind.MSForms, "1500", 75d, DisplayName = "a UserForm's block, in twips")]
    public void AFormsLengths_AreInPoints_FromTheUnitItsDesignerWritesThemIn(FormDesignerKind kind, string written, double points)
    {
        var properties = new Dictionary<string, string> { ["ClientLeft"] = written, ["ClientTop"] = written, ["ClientWidth"] = written, ["ClientHeight"] = written };

        var form = new FormDescriptor("Form1", kind, new FormControlClass("RDForms", "Form"), properties, []);

        CollectionAssert.AreEqual(new double?[] { points, points, points, points }, new[] { form.ClientLeft, form.ClientTop, form.ClientWidth, form.ClientHeight });
    }
}
