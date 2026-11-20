using System.Text.Json;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Semantics.Refactoring;
using RDCore.SDK.Server.ProtocolExtensions;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// The arguments of the commands a client runs itself (<see cref="RefactoringCommands"/>): written by a server, carried as JSON, and read back by a client
/// as they were written; and what a client is told of arguments it cannot read.
/// </summary>
[TestClass]
public sealed class RefactoringCommandArgumentsTests
{
    // a URI as OmniSharp writes it, with its escaped drive colon, which a client sends back as it is.
    private const string Square = "file:///c%3A/ws/Square.cls";
    private const string Shape = "file:///c%3A/ws/IShape.cls";

    private static readonly SourceRange AreaName = new(4, 16, 4, 20);

    // the arguments of a command as a client is given them: the JSON the server sent, parsed.
    private static JsonElement[] OnTheWire(Command command)
    {
        using var document = JsonDocument.Parse(JsonConvert.SerializeObject(command.Arguments));
        return [.. document.RootElement.EnumerateArray().Select(argument => argument.Clone())];
    }

    private static JsonElement[] Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(argument => argument.Clone())];
    }

    [TestMethod]
    public void TheArgumentsOfARename_AreReadAsTheServerWroteThem()
    {
        var written = new RenameCommandArguments(Square, new SourcePosition(3, 17));
        var command = written.ToCommand("Rename");

        Assert.AreEqual(RefactoringCommands.Rename, command.Name);
        Assert.AreEqual("""["file:///c%3A/ws/Square.cls",{"line":3,"character":17}]""", JsonConvert.SerializeObject(command.Arguments));
        Assert.IsTrue(RenameCommandArguments.TryRead(OnTheWire(command), out var read, out var failure), failure);
        Assert.AreEqual(written, read);
        Assert.IsTrue(command.TryReadRename(out var readByOmniSharp, out failure), failure);
        Assert.AreEqual(written, readByOmniSharp);
    }

    [TestMethod]
    public void TheArgumentsOfAShowReferences_AreReadAsTheServerWroteThem_EachLocationInItsOrder()
    {
        CommandLocation[] locations = [new(Square, new SourceRange(5, 17, 5, 28)), new("file:///c%3A/ws/Disc.cls", new SourceRange(7, 17, 7, 28))];
        var written = new ShowReferencesCommandArguments(Shape, AreaName.Start, locations);
        var command = written.ToCommand("2 implementations");

        Assert.AreEqual(RefactoringCommands.ShowReferences, command.Name);
        Assert.IsTrue(ShowReferencesCommandArguments.TryRead(OnTheWire(command), out var read, out var failure), failure);
        Assert.AreEqual(Shape, read.DocumentUri);
        Assert.AreEqual(AreaName.Start, read.Position);
        CollectionAssert.AreEqual(locations, read.Locations.ToArray());
        Assert.IsTrue(command.TryReadShowReferences(out var readByOmniSharp, out failure), failure);
        CollectionAssert.AreEqual(locations, readByOmniSharp.Locations.ToArray());
    }

    [TestMethod]
    public void TheArgumentsOfAShowReferences_MayHaveNoLocation()
    {
        var command = new ShowReferencesCommandArguments(Shape, AreaName.Start, []).ToCommand("0 references");

        Assert.IsTrue(ShowReferencesCommandArguments.TryRead(OnTheWire(command), out var read, out var failure), failure);
        Assert.IsEmpty(read.Locations);
    }

    [TestMethod]
    public void TheArgumentsOfAGoToLocation_AreReadAsTheServerWroteThem()
    {
        var written = new GoToLocationCommandArguments(Shape, AreaName);
        var command = written.ToCommand("implements IShape.Area");

        Assert.AreEqual(RefactoringCommands.GoToLocation, command.Name);
        Assert.AreEqual("""["file:///c%3A/ws/IShape.cls",{"start":{"line":4,"character":16},"end":{"line":4,"character":20}}]""",
            JsonConvert.SerializeObject(command.Arguments));
        Assert.IsTrue(GoToLocationCommandArguments.TryRead(OnTheWire(command), out var read, out var failure), failure);
        Assert.AreEqual(written, read);
        Assert.IsTrue(command.TryReadGoToLocation(out var readByOmniSharp, out failure), failure);
        Assert.AreEqual(written, readByOmniSharp);
    }

    [TestMethod]
    public void ArgumentsTheProtocolAssemblyWrites_AreReadBack_WithNoServer()
    {
        var written = new GoToLocationCommandArguments(Shape, AreaName);

        Assert.IsTrue(GoToLocationCommandArguments.TryRead(written.ToArguments(), out var read, out var failure), failure);
        Assert.AreEqual(written, read);
    }

    [TestMethod]
    public void ALocationOfOmniSharp_IsALocationOfTheArguments_WithItsUriAsOmniSharpWritesIt()
    {
        var location = new Location { Uri = DocumentUri.From(Square), Range = new LspRange(5, 17, 5, 28) };

        Assert.AreEqual(new CommandLocation(location.Uri.ToString(), new SourceRange(5, 17, 5, 28)), location.ToCommandLocation());
    }

    [TestMethod]
    public void AnArgumentAfterThoseOfTheCommand_IsIgnored()
        => Assert.IsTrue(RenameCommandArguments.TryRead(Arguments("""["file:///a.bas",{"line":0,"character":1},"more"]"""), out _, out var failure), failure);

    [TestMethod]
    public void ACommandOfAnotherName_IsNotReadAsARename()
    {
        var command = new GoToLocationCommandArguments(Shape, AreaName).ToCommand("implements IShape.Area");

        Assert.IsFalse(command.TryReadRename(out _, out var failure));
        Assert.AreEqual($"{RefactoringCommands.GoToLocation} is not {RefactoringCommands.Rename}.", failure);
    }

    [TestMethod]
    [DataRow(null, "rdcore.rename takes 2 arguments (the URI of the document and a position), and was given 0.", DisplayName = "no arguments")]
    [DataRow("""["file:///a.bas"]""", "rdcore.rename takes 2 arguments (the URI of the document and a position), and was given 1.",
        DisplayName = "one argument")]
    [DataRow("""[3,{"line":0,"character":1}]""", "rdcore.rename: argument 1 is not the URI of a document, but the number 3.",
        DisplayName = "a number for the URI")]
    [DataRow("""["",{"line":0,"character":1}]""", "rdcore.rename: argument 1 is not the URI of a document, but an empty string.",
        DisplayName = "an empty URI")]
    [DataRow("""["file:///a.bas","L0C1"]""", "rdcore.rename: argument 2 is not an object, but a string.", DisplayName = "a string for the position")]
    [DataRow("""["file:///a.bas",{"line":0}]""", "rdcore.rename: argument 2 has no 'character'.", DisplayName = "no character")]
    [DataRow("""["file:///a.bas",{"line":-1,"character":1}]""", "rdcore.rename: argument 2: 'line' is not an integer of zero or more, but the number -1.",
        DisplayName = "a negative line")]
    [DataRow("""["file:///a.bas",{"line":"0","character":1}]""", "rdcore.rename: argument 2: 'line' is not an integer of zero or more, but a string.",
        DisplayName = "a string for the line")]
    public void ARenameItsArgumentsDoNotDescribe_IsNotRead_AndTheFailureSaysWhatIsWrong(string? json, string expected)
    {
        Assert.IsFalse(RenameCommandArguments.TryRead(json is null ? null : Arguments(json), out var read, out var failure));
        Assert.IsNull(read);
        Assert.AreEqual(expected, failure);
    }

    [TestMethod]
    [DataRow("""["file:///a.cls",{"line":0,"character":1},{}]""", "rdcore.showReferences: argument 3 is not an array of locations, but an object.",
        DisplayName = "an object for the locations")]
    [DataRow("""["file:///a.cls",{"line":0,"character":1},[{"uri":"file:///b.cls","range":"""
        + """{"start":{"line":1,"character":0},"end":{"line":1,"character":4}}},{"uri":"file:///c.cls"}]]""",
        "rdcore.showReferences: argument 3, location 2 has no 'range'.", DisplayName = "a location with no range")]
    [DataRow("""["file:///a.cls",{"line":0,"character":1},[{"uri":"file:///b.cls","range":{"start":{"line":1,"character":0}}}]]""",
        "rdcore.showReferences: argument 3, location 1, range has no 'end'.", DisplayName = "a range with no end")]
    [DataRow("""["file:///a.cls",{"line":0,"character":1},[{"uri":null,"range":{"start":{"line":1,"character":0},"end":{"line":1,"character":4}}}]]""",
        "rdcore.showReferences: argument 3, location 1: 'uri' is not the URI of a document, but null.", DisplayName = "a location with a null URI")]
    public void AShowReferencesItsArgumentsDoNotDescribe_IsNotRead_AndTheFailureSaysWhichLocation(string json, string expected)
    {
        Assert.IsFalse(ShowReferencesCommandArguments.TryRead(Arguments(json), out _, out var failure));
        Assert.AreEqual(expected, failure);
    }

    [TestMethod]
    [DataRow("""["file:///a.cls",{"line":0,"character":1}]""", "rdcore.goToLocation: argument 2 has no 'start'.", DisplayName = "a position for the range")]
    [DataRow("""["file:///a.cls",{"start":{"line":0,"character":1},"end":{"line":0,"character":1.5}}]""",
        "rdcore.goToLocation: argument 2, end: 'character' is not an integer of zero or more, but the number 1.5.", DisplayName = "a fraction")]
    public void AGoToLocationItsArgumentsDoNotDescribe_IsNotRead_AndTheFailureSaysWhatIsWrong(string json, string expected)
    {
        Assert.IsFalse(GoToLocationCommandArguments.TryRead(Arguments(json), out _, out var failure));
        Assert.AreEqual(expected, failure);
    }
}
