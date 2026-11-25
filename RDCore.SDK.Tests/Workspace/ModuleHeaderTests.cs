using RDCore.SDK.Model.Source;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

[TestClass]
public sealed class ModuleHeaderTests
{
    private const string ClassModule =
        "VERSION 1.0 CLASS\r\nBEGIN\r\n  MultiUse = -1  'True\r\nEND\r\nAttribute VB_Name = \"C1\"\r\n";

    private const string DesignerModule =
        "VERSION 5.00\r\nBegin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} Form1 \r\nEnd\r\nAttribute VB_Name = \"Form1\"\r\n";

    private const string StandardModule =
        "Attribute VB_Name = \"M1\"\r\nOption Explicit\r\nPublic X As Long\r\n";

    [TestMethod]
    [DataRow(ClassModule)]
    [DataRow(DesignerModule)]
    [DataRow("version 1.0 class\r\n")]          // keywords are case-insensitive
    [DataRow("  VERSION 1.0 CLASS  \r\n")]      // leading/trailing whitespace
    [DataRow("\r\nVERSION 1.0 CLASS\r\n")]      // a leading blank line
    public void IsClassModule_True_WhenSourceHasAVersionHeader(string source)
        => Assert.AreEqual(true, ModuleHeader.IsClassModule(source));

    [TestMethod]
    [DataRow(StandardModule)]
    [DataRow("Option Explicit\r\n' VERSION 1.0 CLASS was here once\r\n")] // not on its own line
    [DataRow("Attribute VB_Name = \"NotAClass\"\r\n")]
    [DataRow("Option Explicit\r\nClass Logger\r\nVERSION 1.0 CLASS\r\nEnd Class\r\n")] // inside a block: the block's code
    public void IsClassModule_False_WhenSourceHasNoVersionHeader(string source)
        => Assert.AreEqual(false, ModuleHeader.IsClassModule(source));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void IsClassModule_Null_WhenThereIsNoSource(string? source)
        => Assert.IsNull(ModuleHeader.IsClassModule(source));

    private static string Lines(params string[] lines) => string.Join("\r\n", lines) + "\r\n";

    private const string Form = "VERSION 5.00\r\nObject = \"{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0\"; \"MSCOMCTL.OCX\"\r\n" +
        "Begin VB.Form Picker \r\n   Caption = \"Pick\"\r\n   Begin VB.CommandButton Ok \r\n      Caption = \"OK\"\r\n" +
        "      BeginProperty Font \r\n         Name = \"Tahoma\"\r\n      EndProperty\r\n   End\r\nEnd\r\n";

    [TestMethod]
    [DataRow(StandardModule, 0, 21, 23, DisplayName = "a standard module")]
    [DataRow(ClassModule, 4, 21, 23, DisplayName = "a class module, after its header")]
    [DataRow(DesignerModule, 3, 21, 26, DisplayName = "a designer module")]
    [DataRow("Option Explicit\r\n  attribute  vb_name=\"Late\"\r\n", 1, 22, 26, DisplayName = "any case and spacing, on a later line")]
    [DataRow("Attribute VB_Name = \"\"\r\n", 0, 21, 21, DisplayName = "an empty name")]
    [DataRow("Class Logger\r\nAttribute VB_Name = \"Other\"\r\nEnd Class\r\nAttribute VB_Name = \"Backup\"\r\n", 3, 21, 27,
        DisplayName = "the file's own, after a block that writes one")]
    public void NameRangeOf_IsBetweenTheQuotesOfTheFirstVBNameLine(string source, int line, int start, int end)
        => Assert.AreEqual(new SourceRange(line, start, line, end), ModuleHeader.NameRangeOf(source));

    [TestMethod]
    [DataRow(null, DisplayName = "no source")]
    [DataRow("Option Explicit\r\nPublic X As Long\r\n", DisplayName = "no VB_Name line")]
    [DataRow("' Attribute VB_Name = \"Commented\"\r\n", DisplayName = "a comment that writes one")]
    [DataRow("Attribute VB_Description = \"VB_Name\"\r\n", DisplayName = "another attribute")]
    public void NameRangeOf_IsNull_WithoutAVBNameLine(string? source)
        => Assert.IsNull(ModuleHeader.NameRangeOf(source));

    [TestMethod]
    [DataRow("Option Explicit\r\nPublic Sub Go()\r\nEnd Sub\r\n", 0, DisplayName = "no header: its first line")]
    [DataRow(null, 0, DisplayName = "no source")]
    [DataRow("VERSION 1.0 CLASS\r\nBEGIN\r\n  MultiUse = -1  'True\r\nEND\r\nOption Explicit\r\n", 4, DisplayName = "a class: after its BEGIN block")]
    [DataRow(Form + "Option Explicit\r\n", 11, DisplayName = "a form: after its Object lines and its block, the blocks of its controls included")]
    [DataRow("VERSION 5.00\r\nObject = \"{X}#1.0#0\"; \"A.OCX\"\r\nObject = \"{Y}#1.0#0\"; \"B.OCX\"\r\nOption Explicit\r\n", 3,
        DisplayName = "Object lines and no block: after them")]
    [DataRow("VERSION 5.00\r\nBegin VB.Form Picker \r\n   Begin VB.Label L \r\n   End\r\n", 4, DisplayName = "a block never ended: its last line")]
    public void AttributesLineOf_IsTheLineAfterTheHeader(string? source, int line)
        => Assert.AreEqual(line, ModuleHeader.AttributesLineOf(source));

    [TestMethod]
    public void AttributesLineOf_AModuleThatHasAVBName_IsTheLineOfIt()
    {
        var source = Form + "Attribute VB_Name = \"Picker\"\r\nOption Explicit\r\n";

        Assert.AreEqual(ModuleHeader.NameRangeOf(source)?.Start.Line, ModuleHeader.AttributesLineOf(source));
    }
}
