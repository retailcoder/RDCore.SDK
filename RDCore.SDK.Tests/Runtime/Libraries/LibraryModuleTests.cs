using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Runtime.Libraries;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// A module a library's file declares (<see cref="LibraryDescription.Modules"/>): read from the file, written back, refused in a format that states none, and
/// read as a standard module of the library's project whose procedures carry their keys.
/// </summary>
[TestClass]
public sealed class LibraryModuleTests
{
    private static readonly Uri Root = new("file:///c:/ws/");

    // the OLE Automation library's StdFunctions, with the class its LoadPicture returns.
    private const string Library = """
        {
          "formatVersion": 3,
          "name": "stdole",
          "classes": [
            { "name": "StdPicture", "isCreatable": true, "members": [ { "name": "Type", "kind": "propertyGet", "type": "Integer" } ] }
          ],
          "modules": [
            {
              "name": "StdFunctions",
              "members": [
                {
                  "name": "LoadPicture",
                  "type": "StdPicture",
                  "parameters": [ { "name": "Filename", "isOptional": true }, { "name": "WidthDesired", "type": "Long", "isOptional": true } ]
                },
                { "name": "SavePicture", "parameters": [ { "name": "Picture", "type": "StdPicture" }, { "name": "Filename", "type": "String" } ] }
              ]
            }
          ]
        }
        """;

    [TestMethod]
    public void AModule_IsReadFromTheFile_AndWrittenBackTheSame()
    {
        var library = LibraryJson.Read(Library);
        var module = library.Modules.Single();
        Assert.AreEqual("StdFunctions", module.Name);
        CollectionAssert.AreEqual(new[] { "LoadPicture", "SavePicture" }, module.Members.Select(member => member.Name).ToArray());

        var read = LibraryJson.Read(LibraryJson.Write(library));
        Assert.AreEqual(3, read.FormatVersion);
        Assert.AreEqual("StdPicture", read.Modules.Single().Members[0].Type);
        Assert.AreEqual(2, read.Modules.Single().Members[1].Parameters.Length);
    }

    [TestMethod]
    public void ALibraryWithNoModules_IsWrittenWithNone()
        => Assert.IsFalse(LibraryJson.Write(new LibraryDescription { Name = "Empty" }).Contains("modules", StringComparison.OrdinalIgnoreCase));

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void AFileOfAnEarlierFormat_StillLoads(int version)
    {
        var library = LibraryJson.Read($$"""{ "formatVersion": {{version}}, "name": "Old", "dependsOn": [ "stdole" ], "classes": [ { "name": "Thing" } ] }""");

        Assert.AreEqual(version, library.FormatVersion);
        Assert.AreEqual("Thing", library.Classes.Single().Name);
        Assert.IsTrue(library.Modules.IsEmpty);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void AFileOfAnEarlierFormat_ThatStatesAModule_IsRefused(int version)
        => Assert.ThrowsExactly<InvalidDataException>(() => LibraryJson.Read($$"""{ "formatVersion": {{version}}, "name": "Old", "modules": [ { "name": "M" } ] }"""));

    [TestMethod]
    public void AModuleOfTheNameOfAClass_IsRefused()
        => Assert.ThrowsExactly<InvalidDataException>(() => LibraryJson.Read("""{ "formatVersion": 3, "name": "L", "classes": [ { "name": "X" } ], "modules": [ { "name": "x" } ] }"""));

    [TestMethod]
    public void AModule_IsAStandardModuleOfTheLibrarysProject_WithItsProceduresAtModuleScope()
    {
        var symbols = new LibrarySymbolReader(Root).Read([LibraryJson.Read(Library)]);

        var module = symbols.OfType<VBStandardModuleSymbol>().Single(symbol => symbol.Name == "StdFunctions");
        Assert.AreEqual("stdole", module.GetProperty(SymbolProperties.Library));
        Assert.AreEqual(1, module.GetProperty(SymbolProperties.LibraryPriority));

        var load = symbols.OfType<VBFunctionMemberSymbol>().Single(symbol => symbol.Name == "LoadPicture");
        Assert.AreEqual(module.Uri, load.ParentUri);
        Assert.AreEqual(ScopeKind.Module, load.ScopeKind);
        Assert.AreEqual("stdole.StdFunctions.LoadPicture/method", load.GetProperty(SymbolProperties.ExternalTarget));
        Assert.AreEqual("StdPicture", load.ResolvedType.Name);
        CollectionAssert.AreEqual(new[] { "Filename", "WidthDesired" }, load.Parameters.Select(parameter => parameter.Name).ToArray(), "no Me");

        var save = symbols.OfType<VBProcedureMemberSymbol>().Single(symbol => symbol.Name == "SavePicture");
        Assert.AreEqual("stdole.StdFunctions.SavePicture/method", save.GetProperty(SymbolProperties.ExternalTarget));
    }

    [TestMethod]
    public void TheKeyOfAModuleProcedure_IsInTheFormatOfAClassMembers()
    {
        var library = LibraryJson.Read(Library);
        var member = library.Modules.Single().Members[0];

        Assert.AreEqual("stdole.StdFunctions.LoadPicture/method", LibrarySymbolReader.ExternalTargetOf(library, library.Modules.Single(), member));
        Assert.AreEqual("stdole.StdPicture.LoadPicture/method", LibrarySymbolReader.ExternalTargetOf(library, library.Classes.Single(), member));
    }
}
