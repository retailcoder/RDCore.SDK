using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Runtime.Libraries;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// A property a library's file states is design-time only (<see cref="MemberDescription.IsDesignTimeOnly"/>): read from the file, written back, and carried
/// by the property's symbol.
/// </summary>
[TestClass]
public sealed class DesignTimeOnlyMemberTests
{
    private static readonly Uri Root = new("file:///c:/ws/");

    // a ListBox with one property its designer sets (Sorted) and one it computes (ListCount), both declared with a Property Get alone.
    private const string Library = """
        {
          "formatVersion": 2,
          "name": "Controls",
          "classes": [
            {
              "name": "ListBox",
              "members": [
                { "name": "Sorted", "kind": "propertyGet", "type": "Boolean", "isDesignTimeOnly": true },
                { "name": "ListCount", "kind": "propertyGet", "type": "Integer" }
              ]
            }
          ]
        }
        """;

    private static MemberDescription Member(LibraryDescription library, string name) => library.Classes.Single().Members.Single(member => member.Name == name);

    [TestMethod]
    public void TheFact_IsReadFromTheFile_AndAMemberThatDoesNotStateItIsNot()
    {
        var library = LibraryJson.Read(Library);

        Assert.IsTrue(Member(library, "Sorted").IsDesignTimeOnly);
        Assert.IsFalse(Member(library, "ListCount").IsDesignTimeOnly);
    }

    [TestMethod]
    public void TheFact_IsWrittenWhereItIsTrue_AndReadBackTheSame()
    {
        var written = LibraryJson.Write(LibraryJson.Read(Library));

        Assert.AreEqual(1, written.Split("isDesignTimeOnly").Length - 1, written);
        var read = LibraryJson.Read(written);
        Assert.IsTrue(Member(read, "Sorted").IsDesignTimeOnly);
        Assert.IsFalse(Member(read, "ListCount").IsDesignTimeOnly);
    }

    // a type library's export: what it declares, with no fact it cannot state.
    private static LibraryDescription Exported(params string[] members)
    {
        var library = LibraryJson.Read(Library);
        var listBox = library.Classes.Single();
        return library with
        {
            Classes = [listBox with { Members = [.. listBox.Members.Where(member => members.Contains(member.Name)).Select(member => member with { IsDesignTimeOnly = false })] }],
        };
    }

    [TestMethod]
    public void AnExport_KeepsTheFact_FromTheDescriptionItReplaces()
    {
        var kept = Exported("Sorted", "ListCount").KeepingDesignTimeOnly(LibraryJson.Read(Library), out var lost);

        Assert.IsTrue(Member(kept, "Sorted").IsDesignTimeOnly);
        Assert.IsFalse(Member(kept, "ListCount").IsDesignTimeOnly);
        Assert.IsEmpty(lost);
    }

    [TestMethod]
    public void AnExport_NamesTheMembers_ItNoLongerDeclares()
    {
        var kept = Exported("ListCount").KeepingDesignTimeOnly(LibraryJson.Read(Library), out var lost);

        Assert.IsFalse(Member(kept, "ListCount").IsDesignTimeOnly);
        CollectionAssert.AreEqual(new[] { "ListBox.Sorted" }, lost.ToArray());
    }

    [TestMethod]
    public void AMemberNewToTheExport_IsNotDesignTimeOnly()
    {
        var exported = Exported("Sorted", "ListCount");
        var listBox = exported.Classes.Single();
        exported = exported with
        {
            Classes = [listBox with { Members = [.. listBox.Members, new MemberDescription { Name = "Columns", Kind = MemberKind.PropertyGet, Type = "Integer" }] }],
        };

        var kept = exported.KeepingDesignTimeOnly(LibraryJson.Read(Library), out _);

        Assert.IsTrue(Member(kept, "Sorted").IsDesignTimeOnly);
        Assert.IsFalse(Member(kept, "Columns").IsDesignTimeOnly);
    }

    [TestMethod]
    public void AnExport_ThatReplacesNothing_IsLeftAsItIs()
    {
        var exported = Exported("Sorted", "ListCount");

        Assert.AreSame(exported, exported.KeepingDesignTimeOnly(null, out var lost));
        Assert.IsEmpty(lost);
    }

    [TestMethod]
    public void ThePropertysSymbol_CarriesIt()
    {
        var symbols = new LibrarySymbolReader(Root).Read([LibraryJson.Read(Library)]);
        var listBox = symbols.OfType<VBClassModuleSymbol>().Single(symbol => symbol.Name == "ListBox");

        Assert.IsTrue(listBox.Members.Single(member => member.Name == "Sorted").GetProperty(SymbolProperties.DesignTimeOnly));
        Assert.IsFalse(listBox.Members.Single(member => member.Name == "ListCount").GetProperty(SymbolProperties.DesignTimeOnly));
    }
}
