using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.Symbols.Completion;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// The named arguments of a call are offered wherever an argument starts, the first one included, and rank where they are most likely to be written: before
/// the other candidates where the argument would be given to an Optional parameter, or follows a named one; after them where a value is what is written.
/// </summary>
[TestClass]
public sealed class NamedArgumentGroupTests
{
    // Send(Text, Title, Optional Copies)
    private static readonly SignatureDescription Send = new("Send(Text, Title, Optional Copies)",
    [
        new SignatureParameter("Text", 5, 9),
        new SignatureParameter("Title", 11, 16),
        new SignatureParameter("Copies", 18, 33, IsOptional: true),
    ]);

    private static CompletionGroup GroupAt(string argumentsBefore, int argumentIndex)
        => CompletionCandidates.NamedArgumentGroup(Send, new SignatureSite("Send", argumentIndex) { ArgumentsBefore = argumentsBefore }, string.Empty, string.Empty);

    private static string[] Labels(CompletionGroup group) => [.. group.Candidates.Select(candidate => candidate.Label)];

    [TestMethod]
    public void TheFirstArgument_OfAParameterThatIsNotOptional_IsOfferedTheNames_AfterTheOthers()
    {
        var group = GroupAt(string.Empty, 0);

        CollectionAssert.AreEqual(new[] { "Text:=", "Title:=", "Copies:=" }, Labels(group));
        Assert.IsTrue(group.RanksAfterTheOthers);
        Assert.IsFalse(group.IsSelectedWhenNothingIsTyped);
    }

    [TestMethod]
    public void AnArgument_AfterAPositionalOne_OfAParameterThatIsNotOptional_IsOfferedTheNamesLeft_AfterTheOthers()
    {
        var group = GroupAt("\"a\", ", 1);

        CollectionAssert.AreEqual(new[] { "Title:=", "Copies:=" }, Labels(group));
        Assert.IsTrue(group.RanksAfterTheOthers);
    }

    [TestMethod]
    public void AnArgument_OfAnOptionalParameter_IsOfferedTheNames_BeforeTheOthers()
    {
        var group = GroupAt("\"a\", \"b\", ", 2);

        CollectionAssert.AreEqual(new[] { "Copies:=" }, Labels(group));
        Assert.IsFalse(group.RanksAfterTheOthers);
    }

    [TestMethod]
    public void AnArgument_AfterANamedOne_IsOfferedTheNamesLeft_BeforeTheOthers()
    {
        var group = GroupAt("Copies:=2, ", 1);

        CollectionAssert.AreEqual(new[] { "Text:=", "Title:=" }, Labels(group));
        Assert.IsFalse(group.RanksAfterTheOthers, "after a named argument, nothing but a name is written");
    }

    [TestMethod]
    public void AGroupThatRanksAfterTheOthers_IsListedAfterThem_AndAnotherIsSelected()
    {
        CompletionGroup names = new([new CompletionCandidate("Text:=", CompletionItemKind.Variable)], IsSelectedWhenNothingIsTyped: false, RanksAfterTheOthers: true);
        CompletionCandidate[] others = [new("Total", CompletionItemKind.Variable), new("Tally", CompletionItemKind.Function)];

        var items = CompletionCandidates.ListOf("T", [names], others).Items.ToList();

        CollectionAssert.AreEqual(new[] { "Tally", "Total", "Text:=" }, items.Select(item => item.Label).ToArray());
        Assert.AreEqual("Tally", items.Single(item => item.Preselect).Label);
    }
}
