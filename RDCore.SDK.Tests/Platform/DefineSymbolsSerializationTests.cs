using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Platform.Components;
using System.Text.Json;

namespace RDCore.Tests.Platform;

/// <summary>
/// <see cref="DefineSymbolsParams"/> as the transport carries it: the addresses of the workspace and of its module stay the absolute ones they were, on any
/// platform, for every test that defines a module through <see cref="Wire"/>.
/// </summary>
[TestClass]
public sealed class DefineSymbolsSerializationTests
{
    [TestMethod]
    public void AWorkspaceRoot_IsTheSameAbsoluteUri_AcrossTheWire()
    {
        var root = Wire.FileUri(Path.Combine(Path.GetTempPath(), "rdcore-wire"));
        var module = new UriBuilder(root) { Fragment = "Program" }.Uri;

        var read = Wire.RoundTrip(new DefineSymbolsParams { WorkspaceRoot = root, ModuleUri = module, ModuleName = "Program" });

        Assert.IsTrue(read.WorkspaceRoot!.IsAbsoluteUri, read.WorkspaceRoot.OriginalString);
        Assert.AreEqual(root.AbsoluteUri, read.WorkspaceRoot.AbsoluteUri);
        Assert.AreEqual(module.AbsoluteUri, read.ModuleUri!.AbsoluteUri);
    }

    // a module of a project the session's project references says whose it is, its precedence and its kind; a module of the session's own says none.
    [TestMethod]
    [DataRow("NavalLib", 2, ModuleType.ClassModule)]
    [DataRow("NavalLib", 1, ModuleType.StdModule)]
    [DataRow(null, 0, ModuleType.StdModule, DisplayName = "a module of the session's own project")]
    public void TheProjectAModuleIsOf_IsTheSameAcrossTheWire(string? library, int priority, ModuleType kind)
    {
        var root = Wire.FileUri(Path.Combine(Path.GetTempPath(), "rdcore-wire", "NavalConsole"));
        var module = new UriBuilder(Wire.FileUri(Path.Combine(Path.GetTempPath(), "rdcore-wire", "NavalLib"))) { Fragment = "Grid" }.Uri;

        var read = Wire.RoundTrip(new DefineSymbolsParams
        {
            WorkspaceRoot = root, ModuleUri = module, ModuleName = "Grid", Library = library, LibraryPriority = priority, ModuleType = kind,
        });

        Assert.AreEqual(library, read.Library);
        Assert.AreEqual(priority, read.LibraryPriority);
        Assert.AreEqual(kind, read.ModuleType);
        Assert.AreEqual(module.AbsoluteUri, read.ModuleUri!.AbsoluteUri);
    }

    // each Implements directive is where its file and its range say, since a module written in several files repeats ranges: a location is written as
    // SourceLocation is anywhere on the wire.
    [TestMethod]
    public void WhereEachImplementsIsWritten_IsItsFileAndItsRange_OnTheWire()
    {
        var part = new Uri("file:///c:/workspace/Shape.Part2.cls");
        var request = new DefineSymbolsParams
        {
            ModuleName = "Shape",
            ImplementedInterfaceNames = ["IShape"],
            ImplementedInterfaceLocations = [new SourceLocation(part, new SourceRange(1, 2, 3, 4))],
        };

        using var written = JsonDocument.Parse(LspSerializer.Instance.SerializeObject(request));
        var read = Wire.RoundTrip(request);

        Assert.AreEqual(
            """[{"uri":"file:///c:/workspace/Shape.Part2.cls","range":{"start":{"line":1,"character":2},"end":{"line":3,"character":4},"isEmpty":false}}]""",
            written.RootElement.GetProperty("implementedInterfaceLocations").GetRawText());
        CollectionAssert.AreEqual(request.ImplementedInterfaceLocations.ToArray(), read.ImplementedInterfaceLocations.ToArray());
    }

    // a symbol says the document it is written in, and a local's UnlessLive where each declaration is: a module written in several files repeats ranges.
    [TestMethod]
    public void TheDocumentOfASymbol_AndWhereItsLocalIsDeclaredUnlessLive_AreOnTheWire()
    {
        var request = new DefineSymbolsParams
        {
            ModuleName = "Module1",
            Symbols =
            [
                new SymbolDescriptor
                {
                    Name = "Main", Kind = SymbolDescriptorKind.Procedure, DocumentUri = new Uri("file:///c:/workspace/Module1.bas"),
                    Locals =
                    [
                        new LocalDescriptor
                        {
                            Name = "x", UnlessLive = [new SourceLocation(new Uri("file:///c:/workspace/Module1.Part2.bas"), new SourceRange(1, 2, 3, 4))],
                        },
                    ],
                },
            ],
        };

        // the symbols ride a string of their own (DefineSymbolsParams.SymbolsJson).
        using var written = JsonDocument.Parse(LspSerializer.Instance.SerializeObject(request));
        using var symbols = JsonDocument.Parse(written.RootElement.GetProperty("symbolsJson").GetString()!);
        var symbol = symbols.RootElement[0];
        var read = Wire.RoundTrip(request).Symbols.Single();

        // PlatformJson writes the names of the members as they are.
        Assert.AreEqual("file:///c:/workspace/Module1.bas", symbol.GetProperty("DocumentUri").GetString());
        Assert.AreEqual("file:///c:/workspace/Module1.Part2.bas", symbol.GetProperty("Locals")[0].GetProperty("UnlessLive")[0].GetProperty("Uri").GetString());
        Assert.AreEqual(request.Symbols[0].DocumentUri, read.DocumentUri);
        CollectionAssert.AreEqual(request.Symbols[0].Locals[0].UnlessLive.ToArray(), read.Locals.Single().UnlessLive.ToArray());
    }

    // the dead branches of a module written in two files are each where its file says, since both files repeat ranges (MS-VBAL §3.4).
    [TestMethod]
    public void TheDeadBranchesOfAReply_AreTheirFilesAndTheirRanges_OnTheWire()
    {
        var result = new DefineSymbolsResult
        {
            DeadBranches =
            [
                new SourceLocation(new Uri("file:///c:/workspace/Module1.bas"), new SourceRange(2, 0, 3, 4)),
                new SourceLocation(new Uri("file:///c:/workspace/Module1.Part2.bas"), new SourceRange(2, 0, 3, 4)),
            ],
        };

        using var written = JsonDocument.Parse(LspSerializer.Instance.SerializeObject(result));
        var read = Wire.RoundTrip(result);

        Assert.AreEqual(2, written.RootElement.GetProperty("deadBranches").GetArrayLength());
        CollectionAssert.AreEqual(result.DeadBranches.ToArray(), read.DeadBranches.ToArray());
    }

    // a host that says no dead branches is read as one whose branches are all live.
    [TestMethod]
    public void AReplyWithoutDeadBranches_HasNone()
    {
        var read = LspSerializer.Instance.DeserializeObject<DefineSymbolsResult>("""{"defined":1}""");

        Assert.AreEqual(1, read.Defined, "the reply was read");
        Assert.IsEmpty(read.DeadBranches);
    }

    [TestMethod]
    public void AModuleOfTheSessionsOwnProject_IsOfNoLibrary_ByDefault()
    {
        var request = new DefineSymbolsParams { ModuleName = "Module1" };

        Assert.IsNull(request.Library);
        Assert.AreEqual(0, request.LibraryPriority);
        Assert.AreEqual(ModuleType.StdModule, request.ModuleType);
    }
}
