using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The kind of module an entry point is in, judged of the symbols themselves. A runtime session gives a class's procedures its object as their first parameter
/// (<c>Me</c>), so no class, form or document of one has a <c>Sub Main()</c> that takes nothing: <c>HostEntryPointsHandlerTests</c> cannot tell the module's
/// kind from that, and this does.
/// </summary>
[TestClass]
public sealed class ProgramEntryPointTests
{
    private static readonly Uri WorkspaceRoot = new("file:///c:/ws/");

    private static VBProcedureMemberSymbol SubMain(Uri module) => Sub(module, "Main");

    private static VBProcedureMemberSymbol Sub(Uri module, string name, AccessModifier access = AccessModifier.Public)
        => new(WorkspaceRoot, module, name, ScopeKind.Module, SymbolKindExt.Procedure, VBVoidType.TypeInfo, new SourceRange(1, 0, 2, 7), new SourceRange(1, 11, 1, 15),
            access);

    // a standard module of that name, declaring Subs of those names.
    private static (VBModuleSymbol Module, IEnumerable<VBTypeMemberSymbol> Members) Standard(string name, params string[] subs)
    {
        var module = new VBStandardModuleSymbol(WorkspaceRoot, WorkspaceRoot, name);
        return (module, subs.Select(sub => (VBTypeMemberSymbol)Sub(module.Uri, sub)).ToArray());
    }

    // the program starts at the Sub Main of the one standard module that declares one, when the project names none: a class's is passed over.
    [TestMethod]
    public void AProjectThatNamesNoEntryPoint_StartsAtTheOneSubMain()
    {
        var helper = new VBClassModuleSymbol(WorkspaceRoot, WorkspaceRoot, "Helper");
        (VBModuleSymbol, IEnumerable<VBTypeMemberSymbol>)[] modules = [Standard("Tools", "Helper"), (helper, [SubMain(helper.Uri)]), Standard("Program", "Main")];

        foreach (var build in new[] { null, new ProjectBuild(), new ProjectBuild { OutputType = ProjectOutputType.Application } })
        {
            var resolved = ProgramEntryPoint.Of(build, modules);

            Assert.IsTrue(resolved.IsFound);
            Assert.AreEqual("Program", resolved.Module.Name);
            Assert.AreEqual("Main", resolved.Procedure.Name);
            Assert.IsNull(resolved.Refusal);
            Assert.IsEmpty(resolved.Candidates);
        }
    }

    // the procedure the project names: in the module it names, whatever the case, or in the one standard module that declares it.
    [TestMethod]
    [DataRow("Two.Main", "Two", "Main")]
    [DataRow("two.MAIN", "Two", "Main", DisplayName = "names are compared without regard to case")]
    [DataRow("Start", "One", "Start")]
    [DataRow("Two.Start", null, null, DisplayName = "a procedure the named module does not declare")]
    [DataRow("Three.Main", null, null, DisplayName = "a module the project does not have")]
    public void TheEntryPointTheProjectNames_IsLookedForWhereItSays(string entryPoint, string? module, string? procedure)
    {
        var resolved = ProgramEntryPoint.Of(new ProjectBuild { EntryPoint = entryPoint }, [Standard("One", "Main", "Start"), Standard("Two", "Main")]);

        Assert.AreEqual(module, resolved.Module?.Name);
        Assert.AreEqual(procedure, resolved.Procedure?.Name);
        Assert.AreEqual(module is null ? ProgramEntryPointRefusal.NotFound : (ProgramEntryPointRefusal?)null, resolved.Refusal);
    }

    // more than one standard module declares it: the project has no entry point, and the refusal names them, in the order they were given.
    [TestMethod]
    [DataRow(null)]
    [DataRow("Main")]
    public void AnEntryPointMoreThanOneModuleDeclares_IsRefused_NamingThem(string? entryPoint)
    {
        var resolved = ProgramEntryPoint.Of(new ProjectBuild { EntryPoint = entryPoint }, [Standard("One", "Main"), Standard("Tools"), Standard("Two", "Main")]);

        Assert.IsFalse(resolved.IsFound);
        Assert.AreEqual(ProgramEntryPointRefusal.Ambiguous, resolved.Refusal);
        CollectionAssert.AreEqual(new[] { "One", "Two" }, resolved.Candidates.Select(candidate => candidate.Name).ToArray());
    }

    // a library is not run: it has no entry point, whatever its modules declare.
    [TestMethod]
    public void ALibrary_HasNoEntryPoint()
    {
        var resolved = ProgramEntryPoint.Of(new ProjectBuild { OutputType = ProjectOutputType.Library, EntryPoint = "Program.Main" }, [Standard("Program", "Main")]);

        Assert.IsFalse(resolved.IsFound);
        Assert.AreEqual(ProgramEntryPointRefusal.Library, resolved.Refusal);
    }

    // a project with no Sub Main, or one the rest of the project cannot call, has none.
    [TestMethod]
    public void AProjectWithNoProcedureToStartAt_HasNoEntryPoint()
    {
        var program = new VBStandardModuleSymbol(WorkspaceRoot, WorkspaceRoot, "Program");

        var resolved = ProgramEntryPoint.Of(null, [Standard("Tools", "Helper"), (program, [Sub(program.Uri, "Main", AccessModifier.Private)])]);

        Assert.IsFalse(resolved.IsFound);
        Assert.AreEqual(ProgramEntryPointRefusal.NotFound, resolved.Refusal);
    }

    [TestMethod]
    public void ASubMainOfAStandardModule_IsWhereTheProgramStarts()
    {
        var module = new VBStandardModuleSymbol(WorkspaceRoot, WorkspaceRoot, "Program");

        Assert.IsTrue(ProgramEntryPoint.IsDeclaredBy(module, [SubMain(module.Uri)]));
    }

    [TestMethod]
    public void ASubMainOfAClass_IsNot_EvenOneThatTakesNoArgument()
    {
        var module = new VBClassModuleSymbol(WorkspaceRoot, WorkspaceRoot, "Helper");

        Assert.IsFalse(ProgramEntryPoint.IsDeclaredBy(module, [SubMain(module.Uri)]));
    }
}
