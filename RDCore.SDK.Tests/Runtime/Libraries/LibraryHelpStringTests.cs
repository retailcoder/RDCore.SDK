using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.Completion;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Runtime.Libraries;
using RDCore.Tests.Model.Symbols;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// The help strings of a library's description (<c>description</c> in its file): each symbol the library declares carries its own as
/// <see cref="SymbolProperties.DocString"/>, which a completion item shows.
/// </summary>
[TestClass]
public sealed class LibraryHelpStringTests
{
    private static readonly Uri Root = new("file:///c:/ws/");

    // a library of the current format, with a help string on each kind of thing it declares, and a class and a member that have none.
    private static readonly string Library = $$"""
        {
          "formatVersion": {{LibraryJson.CurrentFormatVersion}},
          "name": "Lab",
          "description": "The lab's library.",
          "classes": [
            {
              "name": "Beaker",
              "description": "Holds a liquid.",
              "members": [
                { "name": "Volume", "kind": "propertyGet", "type": "Double", "description": "How much it holds." },
                { "name": "Empty", "kind": "method" }
              ],
              "events": [ { "name": "Overflowed", "description": "Raised when it is full." } ]
            },
            { "name": "Flask" }
          ],
          "enums": [
            { "name": "Shape", "description": "The shape of a vessel.", "members": [ { "name": "labRound", "value": 1, "description": "A round vessel." } ] }
          ],
          "modules": [
            {
              "name": "Measures",
              "description": "Units and conversions.",
              "members": [ { "name": "Litres", "type": "Double", "description": "Litres in a volume." } ],
              "constants": [ { "name": "labMillilitre", "type": "Double", "value": "0.001", "description": "One millilitre, in litres." } ]
            }
          ]
        }
        """;

    private static Symbol[] Symbols() => [.. new LibrarySymbolReader(Root).Read([LibraryJson.Read(Library)])];

    private static string? HelpStringOf(Symbol symbol) => symbol.GetProperty(SymbolProperties.DocString);

    [TestMethod]
    public void TheLibrary_ItsClasses_TheirMembersAndEvents_CarryTheirHelpStrings()
    {
        var symbols = Symbols();
        var beaker = symbols.OfType<VBClassModuleSymbol>().Single(declared => declared.Name == "Beaker");

        Assert.AreEqual("The lab's library.", HelpStringOf(symbols.OfType<VBProjectSymbol>().Single(project => project.Name == "Lab")));
        Assert.AreEqual("Holds a liquid.", HelpStringOf(beaker));
        Assert.AreEqual("How much it holds.", HelpStringOf(beaker.Members.Single(member => member.Name == "Volume")));
        Assert.AreEqual("Raised when it is full.", HelpStringOf(beaker.Members.Single(member => member.Name == "Overflowed")));
    }

    [TestMethod]
    public void TheEnumerations_TheirConstants_TheModules_TheirProceduresAndConstants_CarryTheirHelpStrings()
    {
        var symbols = Symbols();

        Assert.AreEqual("The shape of a vessel.", HelpStringOf(symbols.Single(symbol => symbol.Name == "Shape")));
        Assert.AreEqual("A round vessel.", HelpStringOf(symbols.Single(symbol => symbol.Name == "labRound")));
        Assert.AreEqual("Units and conversions.", HelpStringOf(symbols.OfType<VBStandardModuleSymbol>().Single(module => module.Name == "Measures")));
        Assert.AreEqual("Litres in a volume.", HelpStringOf(symbols.Single(symbol => symbol.Name == "Litres")));
        Assert.AreEqual("One millilitre, in litres.", HelpStringOf(symbols.Single(symbol => symbol.Name == "labMillilitre")));
    }

    [TestMethod]
    public void WhatTheLibraryWritesNoHelpStringFor_CarriesNone()
    {
        var classes = Symbols().OfType<VBClassModuleSymbol>().ToArray();

        Assert.IsNull(HelpStringOf(classes.Single(declared => declared.Name == "Flask")));
        Assert.IsNull(HelpStringOf(classes.Single(declared => declared.Name == "Beaker").Members.Single(member => member.Name == "Empty")));
    }

    [TestMethod]
    public void AClassesCompletionItem_ShowsItsHelpString()
    {
        var symbols = Symbols();
        var tree = ScopeTreeBuilder.Build(symbols, AccessModifierVisibility.Instance);
        var lab = symbols.OfType<VBProjectSymbol>().Single(project => project.Name == "Lab");

        var types = CompletionCandidates.TypesOf(tree, lab, creatable: false);

        Assert.AreEqual("Holds a liquid.", types.Single(candidate => candidate.Label == "Beaker").Documentation);
    }
}
