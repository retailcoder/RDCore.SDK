using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Semantics.Refactoring;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.SDK.Tests.Extensibility;

/// <summary>
/// The fixes a diagnostic carries in its <c>data</c>: written by the analyzer, and read back from the diagnostic a client sends with a code action request.
/// </summary>
[TestClass]
public sealed class DiagnosticFixesTests
{
    private static Diagnostic Carrying(JToken data) => new() { Range = new Range(0, 0, 0, 1), Message = "finding", Data = data };

    [TestMethod]
    public void EditFixes_AreReadBack_InTheirOrder_WithWhetherTheyChangeBehavior_AndTheirVersion()
    {
        var fixes = new DiagnosticFixes
        {
            Edits =
            [
                new EditFix { Title = "Make a local copy", Edits = [new TextEdit { Range = new Range(1, 0, 1, 0), NewText = "Dim n2 As Long\r\n" }], Version = 7 },
                new EditFix { Title = "Pass it ByRef (the caller's variable changes)", Edits = [new TextEdit { Range = new Range(0, 8, 0, 13), NewText = "ByRef" }],
                    ChangesBehavior = true, Version = 7 },
            ],
        };

        var read = DiagnosticFixes.Of(Carrying(fixes.ToData()));

        Assert.IsNotNull(read);
        CollectionAssert.AreEqual(new[] { "Make a local copy", "Pass it ByRef (the caller's variable changes)" }, read.Edits.Select(fix => fix.Title).ToArray());
        CollectionAssert.AreEqual(new[] { false, true }, read.Edits.Select(fix => fix.ChangesBehavior).ToArray());
        CollectionAssert.AreEqual(new int?[] { 7, 7 }, read.Edits.Select(fix => fix.Version).ToArray());
        Assert.AreEqual("ByRef", read.Edits[1].Edits.Single().NewText);
    }

    [TestMethod]
    public void WhatAFixDoesNotSay_IsNotWritten()
    {
        var data = (JObject)new DiagnosticFixes { Rename = new RenameFix { Suggestions = ["Name"] } }.ToData();

        CollectionAssert.AreEqual(new[] { "rename" }, data.Properties().Select(property => property.Name).ToArray(), data.ToString());

        var edit = (JObject)new DiagnosticFixes { Edits = [new EditFix { Title = "Fix" }] }.ToData();
        var written = ((JObject)edit["edits"]![0]!).Properties().Select(property => property.Name).ToArray();
        CollectionAssert.AreEqual(new[] { "title", "edits" }, written, "a fix that changes nothing the code does, of a version that is not known");
    }

    [TestMethod]
    public void TheVersionOfTheDocument_IsStampedOnTheEditFixesThatDoNotStateOne()
    {
        var fixes = new DiagnosticFixes { Edits = [new EditFix { Title = "Unstated" }, new EditFix { Title = "Stated", Version = 3 }] };

        var stamped = fixes.AtVersion(9);

        CollectionAssert.AreEqual(new int?[] { 9, 3 }, stamped.Edits.Select(fix => fix.Version).ToArray());
        Assert.AreSame(fixes, fixes.AtVersion(null), "a version that is not known stamps nothing");
    }

    [TestMethod]
    public void ARefactoringFix_IsReadBack_ByItsKind()
    {
        var data = new DiagnosticFixes { Refactoring = new RefactoringFix { Kind = RefactoringKinds.ExtractMethod } }.ToData();

        Assert.AreEqual(RefactoringKinds.ExtractMethod, DiagnosticFixes.Of(Carrying(data))?.Refactoring?.Kind);
    }

    [TestMethod]
    public void DataThatHoldsNoFix_IsNoFixes()
    {
        Assert.IsNull(DiagnosticFixes.Of(Carrying(new JObject { ["errorNumber"] = 13 })));
        Assert.IsNull(DiagnosticFixes.Of(Carrying(new JObject { ["edits"] = "not a list" })));
    }

    [TestMethod]
    public void AnItemToAddToAList_TravelsWithTheDiagnostic()
    {
        var fixes = new DiagnosticFixes
        {
            Rename = new RenameFix { Suggestions = ["Index"] },
            Setting = new SettingFix { Title = "Allow the name 'i'", Key = "Diagnostics.MeaningfulNames.AllowedNames", Value = "i" },
        };

        // as the client sends it back: data as JSON it read and wrote.
        var read = DiagnosticFixes.Of(Carrying(JToken.Parse(fixes.ToData().ToString())));

        Assert.AreEqual("Diagnostics.MeaningfulNames.AllowedNames", read?.Setting?.Key);
        Assert.AreEqual("i", read?.Setting?.Value);
        Assert.AreEqual("Allow the name 'i'", read?.Setting?.Title);
        CollectionAssert.AreEqual(new[] { "Index" }, read?.Rename?.Suggestions.ToArray());
        Assert.AreEqual(0, read?.Edits.Count);
    }

    [TestMethod]
    public void AnItemToAddToAList_IsAFixOfItsOwn()
    {
        var read = DiagnosticFixes.Of(Carrying(JObject.Parse("""{ "setting": { "title": "Allow", "key": "K", "value": "v" } }""")));

        Assert.AreEqual("v", read?.Setting?.Value);
        Assert.IsNull(read?.Rename);
    }

    private static readonly RDCoreDiagnosticCode Fixed = new("RDC", 1025);

    // a diagnostic of a code on a line, whose first fix inserts a text at a column of that line
    private static Diagnostic Found(RDCoreDiagnosticCode code, int line, string text, int column = 0, int version = 3, bool changesBehavior = false)
        => new()
        {
            Code = code.ToString(),
            Range = new Range(line, 0, line, 1),
            Message = text,
            Data = new DiagnosticFixes
            {
                Edits = [new EditFix { Title = text, Edits = [new TextEdit { Range = new Range(line, column, line, column), NewText = text }],
                    ChangesBehavior = changesBehavior, Version = version }],
            }.ToData(),
        };

    [TestMethod]
    public void EveryDiagnosticOfACode_IsFixed_InTheOrderOfTheirRanges()
    {
        var (fixedOnes, edits) = DiagnosticFixes.OfEvery([Found(Fixed, 4, "b"), Found(new("RDC", 1011), 3, "other"), Found(Fixed, 1, "a")], Fixed, version: 3);

        CollectionAssert.AreEqual(new[] { "a", "b" }, fixedOnes.Select(diagnostic => diagnostic.Message).ToArray());
        CollectionAssert.AreEqual(new[] { "a", "b" }, edits.Select(edit => edit.NewText).ToArray());
    }

    [TestMethod]
    [DataRow(2, false, DisplayName = "a fix of another version")]
    [DataRow(3, true, DisplayName = "a fix that changes what the code does")]
    public void AFixOfAnotherVersion_OrThatChangesWhatTheCodeDoes_IsNotTaken(int version, bool changesBehavior)
    {
        var (fixedOnes, _) = DiagnosticFixes.OfEvery(
            [Found(Fixed, 1, "a"), Found(Fixed, 2, "b", version: version, changesBehavior: changesBehavior)], Fixed, version: 3);

        CollectionAssert.AreEqual(new[] { "a" }, fixedOnes.Select(diagnostic => diagnostic.Message).ToArray());
    }

    [TestMethod]
    [DataRow(0, DisplayName = "an edit that starts where one taken does: not taken")]
    [DataRow(2, DisplayName = "an edit after the one taken: taken")]
    public void AFixIsTaken_UnlessItOverlapsOneTaken(int column)
    {
        var first = Found(Fixed, 1, "a");
        var replacing = new Diagnostic
        {
            Code = Fixed.ToString(),
            Range = new Range(1, 0, 1, 1),
            Message = "b",
            Data = new DiagnosticFixes
            {
                Edits = [new EditFix { Title = "b", Edits = [new TextEdit { Range = new Range(1, column, 1, 3), NewText = "b" }], Version = 3 }],
            }.ToData(),
        };

        var (fixedOnes, _) = DiagnosticFixes.OfEvery([first, replacing], Fixed, version: 3);

        CollectionAssert.AreEqual(column == 0 ? new[] { "a" } : new[] { "a", "b" }, fixedOnes.Select(diagnostic => diagnostic.Message).ToArray());
    }
}
