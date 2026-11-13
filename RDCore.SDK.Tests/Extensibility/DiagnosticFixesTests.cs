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
}
