using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Platform.Components;

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

    [TestMethod]
    public void AModuleOfTheSessionsOwnProject_IsOfNoLibrary_ByDefault()
    {
        var request = new DefineSymbolsParams { ModuleName = "Module1" };

        Assert.IsNull(request.Library);
        Assert.AreEqual(0, request.LibraryPriority);
        Assert.AreEqual(ModuleType.StdModule, request.ModuleType);
    }
}
