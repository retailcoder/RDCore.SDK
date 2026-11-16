using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Symbols.Completion;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Runtime.Libraries;
using System.Globalization;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// What RD-VBA adds to a library that its type library does not declare (<see cref="MemberDescription.IsAddition"/>): read from the file, written back only
/// where it is true, refused in a format that states none, carried by each symbol as <see cref="SymbolProperties.LibraryAddition"/>, and said by its
/// completion item.
/// </summary>
[TestClass]
public sealed class LibraryAdditionTests
{
    private static readonly Uri Root = new("file:///c:/ws/");

    // VB's Label with one exported member (Caption) and one RD-VBA adds (Opacity), an added event, an added enumeration and an added constant.
    private const string Library = """
        {
          "formatVersion": 5,
          "name": "VB",
          "classes": [
            {
              "name": "Label",
              "members": [
                { "name": "Caption", "kind": "propertyGet", "type": "String" },
                { "name": "Opacity", "kind": "propertyGet", "type": "Single", "isAddition": true }
              ],
              "events": [ { "name": "Click" }, { "name": "Faded", "isAddition": true } ]
            },
            { "name": "Gauge", "isAddition": true }
          ],
          "enums": [
            { "name": "PicturePosition", "isAddition": true, "members": [ { "name": "rdPictureAbove", "value": 7, "isAddition": true } ] },
            { "name": "AlignConstants", "members": [ { "name": "vbAlignNone", "value": 0 } ] }
          ],
          "modules": [
            { "name": "Constants", "constants": [ { "name": "vbOpaque", "type": "Integer", "value": "1" }, { "name": "rdSolid", "type": "Single", "value": "1", "isAddition": true } ] }
          ]
        }
        """;

    [TestMethod]
    public void AnAddition_IsReadFromTheFile_WhereverItCanBe()
    {
        var library = LibraryJson.Read(Library);
        var label = library.Classes.Single(declared => declared.Name == "Label");

        Assert.IsFalse(label.IsAddition);
        Assert.IsTrue(library.Classes.Single(declared => declared.Name == "Gauge").IsAddition);
        CollectionAssert.AreEqual(new[] { false, true }, label.Members.Select(member => member.IsAddition).ToArray());
        CollectionAssert.AreEqual(new[] { false, true }, label.Events.Select(raised => raised.IsAddition).ToArray());
        CollectionAssert.AreEqual(new[] { true, false }, library.Enums.Select(declared => declared.IsAddition).ToArray());
        Assert.IsTrue(library.Enums[0].Members.Single().IsAddition);
        CollectionAssert.AreEqual(new[] { false, true }, library.Modules.Single().Constants.Select(constant => constant.IsAddition).ToArray());
    }

    [TestMethod]
    public void AnAddition_IsWrittenBack_AndWhatIsNotOneStatesNothing()
    {
        var written = LibraryJson.Write(LibraryJson.Read(Library));

        Assert.AreEqual(6, written.Split("\"isAddition\"").Length - 1);
        Assert.AreEqual(LibraryJson.Read(Library).Classes[0].Members[1], LibraryJson.Read(written).Classes[0].Members[1]);
        Assert.IsFalse(LibraryJson.Write(new LibraryDescription { Name = "L", Classes = [new ClassDescription { Name = "X", Members = [new MemberDescription { Name = "Y" }] }] })
            .Contains("isAddition", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(4)]
    public void AFileOfAnEarlierFormat_ThatStatesAnAddition_IsRefused(int version)
        => Assert.ThrowsExactly<InvalidDataException>(() => LibraryJson.Read(
            $$"""{ "formatVersion": {{version}}, "name": "Old", "classes": [ { "name": "X", "members": [ { "name": "Y", "kind": "method", "isAddition": true } ] } ] }"""));

    [TestMethod]
    public void EachSymbol_CarriesWhetherItIsAnAddition()
    {
        var symbols = new LibrarySymbolReader(Root).Read([LibraryJson.Read(Library)]).ToArray();
        bool Added(Symbol symbol) => symbol.GetProperty(SymbolProperties.LibraryAddition);

        var label = symbols.OfType<VBClassModuleSymbol>().Single(declared => declared.Name == "Label");
        Assert.IsFalse(Added(label));
        Assert.IsTrue(Added(symbols.OfType<VBClassModuleSymbol>().Single(declared => declared.Name == "Gauge")));
        Assert.IsFalse(Added(label.Members.Single(member => member.Name == "Caption")));
        Assert.IsTrue(Added(label.Members.Single(member => member.Name == "Opacity")));
        Assert.IsFalse(Added(label.Members.Single(member => member.Name == "Click")));
        Assert.IsTrue(Added(label.Members.Single(member => member.Name == "Faded")));
        Assert.IsTrue(Added(symbols.Single(symbol => symbol.Name == "PicturePosition")));
        Assert.IsTrue(Added(symbols.Single(symbol => symbol.Name == "rdPictureAbove")));
        Assert.IsFalse(Added(symbols.Single(symbol => symbol.Name == "AlignConstants")));
        Assert.IsTrue(Added(symbols.Single(symbol => symbol.Name == "rdSolid")));
        Assert.IsFalse(Added(symbols.Single(symbol => symbol.Name == "vbOpaque")));
    }

    [TestMethod]
    public void ACompletionItem_OfAnAddition_SaysItIsOne_AndOneOfAnExportedMemberDoesNot()
    {
        var label = new LibrarySymbolReader(Root).Read([LibraryJson.Read(Library)]).OfType<VBClassModuleSymbol>().Single(declared => declared.Name == "Label");

        var items = CompletionCandidates.MembersOf(VBClassType.FromClassModule(label)).ToDictionary(candidate => candidate.Label);

        Assert.AreEqual(SymbolTexts.LibraryAddition("VB"), items["Opacity"].Documentation);
        Assert.IsNull(items["Caption"].Documentation);
    }

    [TestMethod]
    [DataRow("en", "RD-VBA addition: the type library of VB does not declare it.")]
    [DataRow("fr", "Ajout de RD-VBA : la bibliothèque de types de VB ne le déclare pas.")]
    public void TheMarkOfAnAddition_NamesItsLibrary_InTheLanguageOfTheReader(string language, string expected)
        => Assert.AreEqual(expected, SymbolTexts.LibraryAddition("VB", CultureInfo.GetCultureInfo(language)));
}
