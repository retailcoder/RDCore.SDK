using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Runtime.Libraries;

namespace RDCore.Tests.Runtime.Libraries;

/// <summary>
/// What <c>TypeName</c> says of an object of a library's class, when it is not the class's name (<see cref="ClassDescription.TypeName"/>): read from the file,
/// written back, refused in a format that states none, and read as the class symbol's <see cref="SymbolProperties.ReportedTypeName"/>.
/// </summary>
[TestClass]
public sealed class LibraryClassTypeNameTests
{
    private static readonly Uri Root = new("file:///c:/ws/");

    // the OLE Automation library's StdPicture, whose objects report the name of their dispatch interface, and StdFont, which states none.
    private const string Library = """
        {
          "formatVersion": 4,
          "name": "stdole",
          "classes": [
            { "name": "StdPicture", "isCreatable": true, "defaultInterface": "Picture", "typeName": "Picture" },
            { "name": "StdFont", "isCreatable": true, "defaultInterface": "Font" }
          ]
        }
        """;

    [TestMethod]
    public void AClassesTypeName_IsReadFromTheFile_AndWrittenBackTheSame()
    {
        var read = LibraryJson.Read(LibraryJson.Write(LibraryJson.Read(Library)));

        Assert.AreEqual(4, read.FormatVersion);
        Assert.AreEqual("Picture", read.Classes.Single(declared => declared.Name == "StdPicture").TypeName);
        Assert.IsNull(read.Classes.Single(declared => declared.Name == "StdFont").TypeName);
    }

    [TestMethod]
    public void AClassWithNoTypeName_IsWrittenWithNone()
        => Assert.IsFalse(LibraryJson.Write(new LibraryDescription { Name = "L", Classes = [new ClassDescription { Name = "X" }] })
            .Contains("typeName", StringComparison.OrdinalIgnoreCase));

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void AFileOfAnEarlierFormat_ThatStatesATypeName_IsRefused(int version)
        => Assert.ThrowsExactly<InvalidDataException>(() => LibraryJson.Read(
            $$"""{ "formatVersion": {{version}}, "name": "Old", "classes": [ { "name": "X", "typeName": "Y" } ] }"""));

    [TestMethod]
    [DataRow("StdPicture", "Picture")]
    [DataRow("StdFont", null)]
    public void AClassesTypeName_IsWhatItsSymbolReports(string name, string? reported)
    {
        var symbols = new LibrarySymbolReader(Root).Read([LibraryJson.Read(Library)]);

        var symbol = symbols.OfType<VBClassModuleSymbol>().Single(declared => declared.Name == name);
        Assert.AreEqual(reported, symbol.GetProperty(SymbolProperties.ReportedTypeName));
    }
}
