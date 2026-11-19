using RDCore.SDK.Model.Symbols.Completion;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// What a completion list offers in a directive: the conditional-compilation constants, in the condition of a <c>#If</c> or a <c>#ElseIf</c> and in the
/// value of a <c>#Const</c>.
/// </summary>
[TestClass]
public sealed class ConditionalConstantCompletionTests
{
    [TestMethod]
    [DataRow("#If ", "", DisplayName = "#If")]
    [DataRow("#If Wi", "Wi", DisplayName = "#If, a name being typed")]
    [DataRow("    #ElseIf Mac Or Is64", "Is64", DisplayName = "#ElseIf, indented, after an operator")]
    [DataRow("#If Not (Lin", "Lin", DisplayName = "#If, in parentheses")]
    [DataRow("#Const DEV = Win", "Win", DisplayName = "the value of a #Const")]
    [DataRow("#const X = ", "", DisplayName = "#const, in lowercase")]
    public void InACondition_OrTheValueOfAConst_AConstantIsWritten(string line, string prefix)
        => Assert.AreEqual(new CompletionSite(CompletionSiteKind.ConditionalConstant, prefix), CompletionSite.Read(line));

    [TestMethod]
    [DataRow("#Const DE", DisplayName = "the name a #Const declares")]
    [DataRow("#If Win64 Then ", DisplayName = "after Then")]
    [DataRow("#If Win64 Then '", DisplayName = "in the comment after Then")]
    [DataRow("#If X = \"Wi", DisplayName = "in a string literal")]
    [DataRow("#If 12", DisplayName = "a number")]
    public void WhereNoConstantIsWritten_NothingIsOffered(string line)
        => Assert.AreEqual(CompletionSiteKind.None, CompletionSite.Read(line).Kind);

    [TestMethod]
    [DataRow("#If", DisplayName = "the keyword being typed")]
    [DataRow("#Else ", DisplayName = "another directive")]
    [DataRow("x = Win", DisplayName = "a statement")]
    public void ElsewhereOnALine_TheSiteIsNoConstant(string line)
        => Assert.AreNotEqual(CompletionSiteKind.ConditionalConstant, CompletionSite.Read(line).Kind);

    [TestMethod]
    public void TheList_HasTheModulesConstants_ThenTheProjects_ThenThePredefinedOnes()
    {
        var lines = new[] { "#Const DEV = 1", "Option Explicit", "  # Const Trace% = DEV", "#If DEV Then", "#End If" };

        var labels = CompletionCandidates.ConditionalConstants(lines, ["Build", "dev"]).Select(candidate => candidate.Label).ToArray();

        CollectionAssert.AreEqual(
            new[] { "DEV", "Trace", "Build", "DEBUG", "Is64BitEnv", "Linux", "Mac", "VBA6", "VBA7", "Win16", "Win32", "Win64" }, labels);
    }
}
