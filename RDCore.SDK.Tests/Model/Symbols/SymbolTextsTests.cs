using RDCore.SDK.Model.Symbols;
using System.Globalization;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// What the platform says of a symbol where an editor shows it (<see cref="SymbolTexts"/>), in the language of the reader, English where the platform has
/// no texts in the reader's language.
/// </summary>
[TestClass]
public sealed class SymbolTextsTests
{
    [TestMethod]
    [DataRow("en", "Member of VBA.Interaction")]
    [DataRow("fr", "Membre de VBA.Interaction")]
    [DataRow("de", "Member of VBA.Interaction")]
    public void MemberOf_NamesWhatDeclaresTheSymbol_InTheLanguageOfTheReader(string language, string expected)
        => Assert.AreEqual(expected, SymbolTexts.MemberOf("VBA.Interaction", CultureInfo.GetCultureInfo(language)));

    [TestMethod]
    [DataRow("en", "Control array of VB.CommandButton")]
    [DataRow("fr", "Groupe de contrôles de type VB.CommandButton")]
    [DataRow("de", "Control array of VB.CommandButton")]
    public void ControlArrayOf_NamesTheClassOfItsControls_InTheLanguageOfTheReader(string language, string expected)
        => Assert.AreEqual(expected, SymbolTexts.ControlArrayOf("VB.CommandButton", CultureInfo.GetCultureInfo(language)));

    [TestMethod]
    [DataRow("en", "The references of 'Foo' may be incomplete: what 'Foo' refers to at Module1 4:12 is not known.")]
    [DataRow("fr", "Les références de « Foo » peuvent être incomplètes : ce que « Foo » désigne à Module1 4:12 n'est pas connu.")]
    public void IncompleteReferences_NameTheSymbol_AndWhereItsBindingIsNotKnown(string language, string expected)
        => Assert.AreEqual(expected, SymbolTexts.ReferencesIncomplete("Foo", "Module1 4:12", CultureInfo.GetCultureInfo(language)));

    [TestMethod]
    public void TheTexts_AreInTheCurrentUICulture_WhenNoCultureIsGiven()
    {
        var current = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            Assert.AreEqual("Membre de Module1", SymbolTexts.MemberOf("Module1"));
            Assert.AreEqual("Groupe de contrôles de type VB.Label", SymbolTexts.ControlArrayOf("VB.Label"));
            Assert.AreEqual("Ajout de RD-VBA : la bibliothèque de types de VB ne le déclare pas.", SymbolTexts.LibraryAddition("VB"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = current;
        }
    }
}
