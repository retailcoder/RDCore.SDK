using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime;
using RDCore.SDK.Runtime.Libraries;
using RDCore.SDK.Semantics;
using RDCore.SDK.Semantics.Facts;
using RDCore.SDK.Semantics.Flags;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// The members a hidden default member of a library's class stands for (<see cref="MemberDescription.WrittenAs"/>): read from the file, written back,
/// kept by an export, carried by the member's symbol, and by a default member's fact over the wire.
/// </summary>
[TestClass]
public sealed class WrittenAsMemberTests
{
    private static readonly Uri Root = new("file:///c:/ws/");

    // a Range whose hidden default member is Item given arguments and Value given none, and an Item that stands for nothing.
    private const string Library = """
        {
          "formatVersion": 7,
          "name": "Sheets",
          "classes": [
            {
              "name": "Range",
              "members": [
                {
                  "name": "_Default", "kind": "propertyGet", "type": "Variant", "dispId": 0, "isHidden": true,
                  "writtenAs": { "withArguments": "Item", "withoutArguments": "Value" },
                  "parameters": [ { "name": "RowIndex", "type": "Variant", "isByVal": true, "isOptional": true } ]
                },
                { "name": "Item", "kind": "propertyGet", "type": "Variant" }
              ]
            }
          ]
        }
        """;

    private static readonly DefaultMemberNames ItemOrValue = new("Item", "Value");

    private static MemberDescription Member(LibraryDescription library, string name) => library.Classes.Single().Members.Single(member => member.Name == name);

    [TestMethod]
    public void TheMembers_AreReadFromTheFile_AndAMemberThatStatesNoneHasNone()
    {
        var library = LibraryJson.Read(Library);

        Assert.AreEqual(ItemOrValue, Member(library, "_Default").WrittenAs);
        Assert.IsNull(Member(library, "Item").WrittenAs);
    }

    [TestMethod]
    [DataRow(1, "classes", DisplayName = "version 1, a class")]
    [DataRow(6, "classes", DisplayName = "version 6, a class")]
    [DataRow(6, "modules", DisplayName = "version 6, a module")]
    public void AFileOfAnEarlierFormat_ThatStatesTheMembers_IsRefused(int version, string owners)
    {
        var json = Library.Replace("\"formatVersion\": 7", $"\"formatVersion\": {version}").Replace("\"classes\"", $"\"{owners}\"");

        var refused = Assert.ThrowsExactly<InvalidDataException>(() => LibraryJson.Read(json));

        StringAssert.Contains(refused.Message, "'Range._Default'");
    }

    [TestMethod]
    public void AFileOfFormatVersion6_ThatStatesNone_IsRead()
    {
        var library = LibraryJson.Read(Library.Replace("\"formatVersion\": 7", "\"formatVersion\": 6")
            .Replace("\"writtenAs\": { \"withArguments\": \"Item\", \"withoutArguments\": \"Value\" },", string.Empty));

        Assert.AreEqual(6, library.FormatVersion);
        Assert.IsNull(Member(library, "_Default").WrittenAs);
    }

    [TestMethod]
    public void TheMembers_AreWrittenWhereTheyAreStated_AndReadBackTheSame()
    {
        var written = LibraryJson.Write(LibraryJson.Read(Library));

        Assert.AreEqual(1, written.Split("writtenAs").Length - 1, written);
        Assert.AreEqual(ItemOrValue, Member(LibraryJson.Read(written), "_Default").WrittenAs);
    }

    // a type library's export: what it declares, with no fact it cannot state.
    private static LibraryDescription Exported(params string[] members)
    {
        var library = LibraryJson.Read(Library);
        var range = library.Classes.Single();
        return library with
        {
            Classes =
            [
                range with { Members = [.. range.Members.Where(member => members.Contains(member.Name)).Select(member => member with { WrittenAs = null })] },
            ],
        };
    }

    [TestMethod]
    public void AnExport_KeepsTheMembers_FromTheDescriptionItReplaces()
    {
        var kept = Exported("_Default", "Item").KeepingWrittenAs(LibraryJson.Read(Library), out var lost);

        Assert.AreEqual(ItemOrValue, Member(kept, "_Default").WrittenAs);
        Assert.IsNull(Member(kept, "Item").WrittenAs);
        Assert.IsEmpty(lost);
    }

    [TestMethod]
    public void AnExport_NamesTheMembers_ItNoLongerDeclares()
    {
        Exported("Item").KeepingWrittenAs(LibraryJson.Read(Library), out var lost);

        CollectionAssert.AreEqual(new[] { "Range._Default" }, lost.ToArray());
    }

    [TestMethod]
    public void AnExport_ThatReplacesNothing_IsLeftAsItIs()
    {
        var exported = Exported("_Default", "Item");

        Assert.AreSame(exported, exported.KeepingWrittenAs(null, out var lost));
        Assert.IsEmpty(lost);
    }

    [TestMethod]
    public void TheMembersSymbol_CarriesThem()
    {
        var symbols = new LibrarySymbolReader(Root).Read([LibraryJson.Read(Library)]);
        var range = symbols.OfType<VBClassModuleSymbol>().Single(symbol => symbol.Name == "Range");

        Assert.AreEqual(ItemOrValue, range.Members.Single(member => member.Name == "_Default").GetProperty(SymbolProperties.WrittenAs));
        Assert.IsNull(range.Members.Single(member => member.Name == "Item").GetProperty(SymbolProperties.WrittenAs));
    }

    [TestMethod]
    public void ADefaultMembersFact_CarriesThemOverTheWire()
    {
        var fact = new MemberAccessFact(new SyntaxNodeId("/Module1.bas", [2, 0]), new SourceLocation(Root, SourceRange.Empty), Tokens.MemberAccess, null, true,
            null, "_Default", MemberAccessOperationSemanticFlags.Implicit | MemberAccessOperationSemanticFlags.DefaultMember)
        {
            DefaultMembers = [new DefaultMemberFact("_Default", ExpressionClassification.Property, PropertyAccessors.Get, 0) { WrittenAs = ItemOrValue }],
        };

        var read = PlatformJson.Deserialize<OperatorFactDto>(PlatformJson.Serialize(OperatorFactDto.From(fact))).DefaultMembers;

        Assert.IsNotNull(read);
        Assert.AreEqual(ItemOrValue, read.Value.Single().WrittenAs);
    }
}
