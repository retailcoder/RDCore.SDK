using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Runtime.Libraries;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// The classes an object of a library's class is as well (<see cref="ClassDescription.Implements"/>): read from the file, written back only where there are
/// some, refused in a format that states none, and read as the class symbol's <see cref="VBClassModuleSymbol.ImplementedInterfaces"/>, whatever the order of
/// the libraries.
/// </summary>
[TestClass]
public sealed class LibraryClassImplementsTests
{
    private static readonly Uri Root = new("file:///c:/ws/");

    // a forms library whose Font is an OLE Automation StdFont too, and a Font of its own of which it names the default interface.
    private static readonly LibraryDescription Forms = new()
    {
        Name = "Forms",
        DependsOn = [new LibraryIdentity("stdole")],
        Classes =
        [
            new ClassDescription { Name = "Font", DefaultInterface = "IFont", Implements = ["stdole.StdFont"] },
            new ClassDescription { Name = "Brush" },
            new ClassDescription { Name = "SolidBrush", Implements = ["Brush"] },
        ],
    };

    private static readonly LibraryDescription Stdole = new()
    {
        Name = "stdole",
        Classes = [new ClassDescription { Name = "StdFont", DefaultInterface = "Font" }],
    };

    private static VBClassModuleSymbol ClassOf(IEnumerable<Symbol> symbols, string library, string name)
        => symbols.OfType<VBClassModuleSymbol>().Single(symbol => symbol.Name == name && symbol.GetProperty(SymbolProperties.Library) == library);

    private static string[] InterfacesOf(VBClassModuleSymbol symbol) => [.. symbol.ImplementedInterfaces.Select(implemented => implemented.Uri.AbsoluteUri)];

    [TestMethod]
    public void AClassOfALibraryListedAfterIt_IsOneOfTheClassesInterfaces_AfterItsDefaultInterface()
    {
        var symbols = new LibrarySymbolReader(Root).Read([Forms, Stdole]);

        var font = ClassOf(symbols, "Forms", "Font");
        var interfaces = InterfacesOf(font);
        var defaultInterface = Array.IndexOf(interfaces, ClassOf(symbols, "Forms", "IFont").Uri.AbsoluteUri);
        var stdFont = Array.IndexOf(interfaces, ClassOf(symbols, "stdole", "StdFont").Uri.AbsoluteUri);

        Assert.IsGreaterThanOrEqualTo(0, defaultInterface);
        Assert.IsGreaterThan(defaultInterface, stdFont);
    }

    [TestMethod]
    public void AClassOfALibraryListedBeforeIt_IsOneOfTheClassesInterfaces()
    {
        var symbols = new LibrarySymbolReader(Root).Read([Stdole, Forms]);

        CollectionAssert.Contains(InterfacesOf(ClassOf(symbols, "Forms", "Font")), ClassOf(symbols, "stdole", "StdFont").Uri.AbsoluteUri);
    }

    [TestMethod]
    public void AClassNamedWithoutALibrary_IsAClassOfTheSameLibrary()
    {
        var symbols = new LibrarySymbolReader(Root).Read([Forms, Stdole]);

        CollectionAssert.Contains(InterfacesOf(ClassOf(symbols, "Forms", "SolidBrush")), ClassOf(symbols, "Forms", "Brush").Uri.AbsoluteUri);
    }

    [TestMethod]
    public void AClassNamedWithoutALibrary_IsNotAClassOfAnotherLibrary()
    {
        var library = new LibraryDescription { Name = "L", Classes = [new ClassDescription { Name = "X", Implements = ["StdFont"] }] };

        var symbols = new LibrarySymbolReader(Root).Read([library, Stdole]);

        CollectionAssert.DoesNotContain(InterfacesOf(ClassOf(symbols, "L", "X")), ClassOf(symbols, "stdole", "StdFont").Uri.AbsoluteUri);
    }

    [TestMethod]
    public void ANameThatIsNoClassRead_IsLeftOut()
    {
        var library = new LibraryDescription
        {
            Name = "L",
            Classes =
            [
                new ClassDescription { Name = "X", Implements = ["Missing", "stdole.Missing", "Absent.Thing", "Y"] },
                new ClassDescription { Name = "Y" },
            ],
        };

        var symbols = new LibrarySymbolReader(Root).Read([library, Stdole]);

        var none = library with { Classes = [library.Classes[0] with { Implements = [] }, library.Classes[1]] };
        var plain = ClassOf(new LibrarySymbolReader(Root).Read([none]), "L", "X");
        string[] expected = [.. InterfacesOf(plain), ClassOf(symbols, "L", "Y").Uri.AbsoluteUri];
        CollectionAssert.AreEqual(expected, InterfacesOf(ClassOf(symbols, "L", "X")));
    }

    [TestMethod]
    public void AClassThatNamesItself_OrAClassTwice_HasItOnce()
    {
        var library = new LibraryDescription
        {
            Name = "L",
            Classes =
            [
                new ClassDescription { Name = "X", DefaultInterface = "IX", Implements = ["X", "Y", "l.y", "IX"] },
                new ClassDescription { Name = "Y" },
            ],
        };

        var x = ClassOf(new LibrarySymbolReader(Root).Read([library]), "L", "X");

        var interfaces = InterfacesOf(x);
        CollectionAssert.AllItemsAreUnique(interfaces);
        CollectionAssert.DoesNotContain(interfaces, x.Uri.AbsoluteUri);
        Assert.AreEqual(1, x.ImplementedInterfaces.Count(implemented => implemented.Name == "Y"));
        Assert.AreEqual(1, x.ImplementedInterfaces.Count(implemented => implemented.Name == "IX"));
    }

    [TestMethod]
    public void TwoClassesThatNameEachOther_AreEachOneOfTheOthersInterfaces()
    {
        var library = new LibraryDescription
        {
            Name = "L",
            Classes = [new ClassDescription { Name = "X", Implements = ["Y"] }, new ClassDescription { Name = "Y", Implements = ["X"] }],
        };

        var symbols = new LibrarySymbolReader(Root).Read([library]);

        var x = ClassOf(symbols, "L", "X");
        var y = ClassOf(symbols, "L", "Y");
        CollectionAssert.Contains(InterfacesOf(x), y.Uri.AbsoluteUri);
        CollectionAssert.Contains(InterfacesOf(y), x.Uri.AbsoluteUri);
    }

    [TestMethod]
    public void AClassOfAClassThatIsAnotherAsWell_NamesTheSymbolThatIsTheOther()
    {
        var library = new LibraryDescription
        {
            Name = "L",
            Classes = [new ClassDescription { Name = "X", Implements = ["Y"] }, new ClassDescription { Name = "Y", Implements = ["stdole.StdFont"] }],
        };

        var symbols = new LibrarySymbolReader(Root).Read([library, Stdole]);

        var y = ClassOf(symbols, "L", "X").ImplementedInterfaces.Single(implemented => implemented.Name == "Y");
        CollectionAssert.Contains(InterfacesOf(y), ClassOf(symbols, "stdole", "StdFont").Uri.AbsoluteUri);
    }

    [TestMethod]
    public void AMemberAndAParameterTypedAsTheClass_HaveATypeWhoseSupertypesHaveTheClassOfALibraryListedAfterIt()
    {
        var forms = Forms with
        {
            Classes =
            [
                new ClassDescription
                {
                    Name = "Control",
                    Members =
                    [
                        new MemberDescription { Name = "Font", Kind = MemberKind.PropertyGet, Type = "Font" },
                        new MemberDescription { Name = "Restyle", Parameters = [new ParameterDescription { Name = "NewFont", Type = "Forms.Font" }] },
                    ],
                },
                .. Forms.Classes,
            ],
        };

        var symbols = new LibrarySymbolReader(Root).Read([forms, Stdole]);

        var stdFont = ClassOf(symbols, "stdole", "StdFont").Uri.AbsoluteUri;
        var control = ClassOf(symbols, "Forms", "Control");
        var member = control.Members.OfType<VBPropertyGetMemberSymbol>().Single(declared => declared.Name == "Font");
        var parameter = control.Members.OfType<VBProcedureMemberSymbol>().Single(declared => declared.Name == "Restyle")
            .Parameters.Single(declared => declared.Name == "NewFont");
        foreach (var type in new[] { member.ResolvedType, parameter.ResolvedType })
        {
            var classType = (VBClassType)type;
            CollectionAssert.Contains(classType.Supertypes.OfType<VBClassType>().Select(supertype => supertype.Symbol.Uri.AbsoluteUri).ToArray(), stdFont);
            Assert.AreEqual(ClassOf(symbols, "Forms", "Font").Uri.AbsoluteUri, classType.Symbol.Uri.AbsoluteUri);
        }
    }

    [TestMethod]
    public void AMemberTypedAsAClassThatWaitsForAnother_IsResolvedByItsName()
    {
        var partnerOfX = new MemberDescription { Name = "Partner", Kind = MemberKind.PropertyGet, Type = "Y" };
        var library = new LibraryDescription
        {
            Name = "L",
            Classes =
            [
                new ClassDescription { Name = "X", Implements = ["Y"], Members = [partnerOfX] },
                new ClassDescription { Name = "Y", Implements = ["X"] },
            ],
        };

        var x = ClassOf(new LibrarySymbolReader(Root).Read([library]), "L", "X");

        var partner = (VBUnresolvedType)x.Members.OfType<VBPropertyGetMemberSymbol>().Single().ResolvedType;
        Assert.AreEqual("L.Y", partner.DeclaredName);
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void ClassesThatNameOneAnotherInACycle_EachReachEveryOtherThroughTheirSupertypes(int length)
    {
        static string NameOf(int index) => $"C{index}";
        var library = new LibraryDescription
        {
            Name = "L",
            Classes =
            [
                .. Enumerable.Range(0, length).Select(index => new ClassDescription { Name = NameOf(index), Implements = [NameOf((index + 1) % length)] }),
            ],
        };

        var symbols = new LibrarySymbolReader(Root).Read([library]);

        var uris = Enumerable.Range(0, length).Select(index => ClassOf(symbols, "L", NameOf(index)).Uri.AbsoluteUri).ToArray();
        for (var index = 0; index < length; index++)
        {
            CollectionAssert.IsSubsetOf(uris, Reached(VBClassType.FromClassModule(ClassOf(symbols, "L", NameOf(index)))), $"from {NameOf(index)}");
        }
    }

    // the classes a walk of a class type's supertypes reaches, the class itself among them.
    private static string[] Reached(VBClassType start)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<VBClassType>([start]);
        while (pending.TryPop(out var type))
        {
            if (reached.Add(type.Symbol.Uri.AbsoluteUri))
            {
                foreach (var supertype in type.Supertypes.OfType<VBClassType>())
                {
                    pending.Push(supertype);
                }
            }
        }

        return [.. reached];
    }

    [TestMethod]
    public void ClassesOfLibrariesThatDependOnEachOther_EachReachTheOtherThroughTheirSupertypes()
    {
        var a = new LibraryDescription
        {
            Name = "A",
            DependsOn = [new LibraryIdentity("B")],
            Classes = [new ClassDescription { Name = "X", Implements = ["B.Y"] }],
        };
        var b = new LibraryDescription
        {
            Name = "B",
            DependsOn = [new LibraryIdentity("A")],
            Classes = [new ClassDescription { Name = "Y", Implements = ["A.X"] }],
        };

        var symbols = new LibrarySymbolReader(Root).Read([a, b]);

        string[] both = [ClassOf(symbols, "A", "X").Uri.AbsoluteUri, ClassOf(symbols, "B", "Y").Uri.AbsoluteUri];
        CollectionAssert.IsSubsetOf(both, Reached(VBClassType.FromClassModule(ClassOf(symbols, "A", "X"))));
        CollectionAssert.IsSubsetOf(both, Reached(VBClassType.FromClassModule(ClassOf(symbols, "B", "Y"))));
    }

    [TestMethod]
    public void TheSymbols_AreInTheOrderOfTheList_WhateverTheOrderTheyAreReadIn()
    {
        var symbols = new LibrarySymbolReader(Root).Read([Forms, Stdole]);

        var libraries = symbols.OfType<VBClassModuleSymbol>().Select(symbol => symbol.GetProperty(SymbolProperties.Library)).Distinct().ToArray();
        CollectionAssert.AreEqual(new[] { "Forms", "stdole" }, libraries);
        Assert.AreEqual(1, ClassOf(symbols, "Forms", "Font").GetProperty(SymbolProperties.LibraryPriority));
        Assert.AreEqual(2, ClassOf(symbols, "stdole", "StdFont").GetProperty(SymbolProperties.LibraryPriority));
    }

    [TestMethod]
    public void TheClassesAnObjectIsAsWell_AreWrittenBack_AndAClassWithNoneStatesNothing()
    {
        var written = LibraryJson.Write(Forms);
        var read = LibraryJson.Read(written);

        // written in the current format, which states them from version 6
        Assert.AreEqual(LibraryJson.CurrentFormatVersion, read.FormatVersion);
        StringAssert.Contains(written, $"\"formatVersion\": {LibraryJson.CurrentFormatVersion}");
        Assert.AreEqual(2, written.Split("\"implements\"").Length - 1);
        CollectionAssert.AreEqual(new[] { "stdole.StdFont" }, read.Classes.Single(declared => declared.Name == "Font").Implements.ToArray());
        CollectionAssert.AreEqual(new[] { "Brush" }, read.Classes.Single(declared => declared.Name == "SolidBrush").Implements.ToArray());
        Assert.IsTrue(read.Classes.Single(declared => declared.Name == "Brush").Implements.IsEmpty);
    }

    [TestMethod]
    public void AFileOfFormatVersion5_IsRead_WithNoClassesAnObjectIsAsWell()
    {
        var read = LibraryJson.Read("""{ "formatVersion": 5, "name": "Old", "classes": [ { "name": "X", "defaultInterface": "IX" } ] }""");

        Assert.AreEqual(5, read.FormatVersion);
        Assert.IsTrue(read.Classes.Single().Implements.IsEmpty);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(5)]
    public void AFileOfAnEarlierFormat_ThatStatesAClassAnObjectIsAsWell_IsRefused(int version)
        => Assert.ThrowsExactly<InvalidDataException>(() => LibraryJson.Read(
            $$"""{ "formatVersion": {{version}}, "name": "Old", "classes": [ { "name": "X", "implements": [ "stdole.StdFont" ] } ] }"""));
}
