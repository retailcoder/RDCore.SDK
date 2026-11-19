using System.Globalization;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// A reference to a project names the one project of the workspace with that name, in any letter case; a name no project has, or two projects share, names
/// none (<strong>RD-VBAL §2.2.3.2</strong>).
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.2.3.2 RDCoreReference")]
public sealed class ProjectReferencesTests
{
    private static RDCoreReference ToProject(string name) => new() { Name = name, IsProject = true };

    private static RDCoreProject Project(string name, params RDCoreReference[] references) => new() { Name = name, References = [.. references] };

    [TestMethod]
    public void AReferenceToAProjectOfTheWorkspace_NamesIt_InAnyLetterCase()
    {
        RDCoreProject[] workspace = [Project("NavalConsole"), Project("NavalLib")];

        var resolution = ProjectReferences.Resolve(ToProject("navallib"), workspace);

        Assert.IsTrue(resolution.IsResolved);
        Assert.AreEqual(1, resolution.Index);
        Assert.IsNull(resolution.Refusal);
        Assert.IsNull(resolution.ToMessage("NavalConsole"));
    }

    [TestMethod]
    public void AReferenceToANameNoProjectHas_NamesNone()
    {
        var resolution = ProjectReferences.Resolve(ToProject("NavalLib"), [Project("NavalConsole")]);

        Assert.IsFalse(resolution.IsResolved);
        Assert.AreEqual(-1, resolution.Index);
        Assert.AreEqual(ProjectReferenceRefusal.NoProject, resolution.Refusal);
    }

    [TestMethod]
    public void AReferenceToANameTwoProjectsShare_NamesNeither()
    {
        var resolution = ProjectReferences.Resolve(ToProject("NavalLib"), [Project("NavalLib"), Project("NavalConsole"), Project("NAVALLIB")]);

        Assert.IsFalse(resolution.IsResolved);
        Assert.AreEqual(-1, resolution.Index);
        Assert.AreEqual(ProjectReferenceRefusal.SharedName, resolution.Refusal);
    }

    // the project itself is a project of the workspace: a reference to it names it, and is the cycle ProjectReferenceCycles reports.
    [TestMethod]
    public void AReferenceToTheProjectItself_NamesIt()
    {
        var self = Project("NavalLib", ToProject("NavalLib"));

        var resolution = ProjectReferences.Resolve(ToProject("NavalLib"), [self]);

        Assert.AreEqual(0, resolution.Index);
    }

    [TestMethod]
    public void AProjectsReferencesToProjects_AreResolvedInTheirOrder_ItsLibrariesLeftOut()
    {
        var console = Project(
            "NavalConsole", ToProject("NavalWindow"), new RDCoreReference { Name = "Scripting" }, ToProject("NavalLib"), ToProject("Missing"));
        RDCoreProject[] workspace = [console, Project("NavalLib"), Project("NavalWindow")];

        var resolutions = ProjectReferences.Of(console, workspace);

        CollectionAssert.AreEqual(new[] { "NavalWindow", "NavalLib", "Missing" }, resolutions.Select(resolution => resolution.Reference.Name).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 1, -1 }, resolutions.Select(resolution => resolution.Index).ToArray());
    }

    [TestMethod]
    public void ARefusal_IsToldInTheReadersLanguage_NamingTheProjectAndTheReference()
    {
        var missing = ProjectReferences.Resolve(ToProject("NavalLib"), [Project("NavalConsole")]);
        var shared = ProjectReferences.Resolve(ToProject("NavalLib"), [Project("NavalLib"), Project("navallib")]);

        Assert.AreEqual("The project NavalConsole references the project NavalLib: no project of the workspace has that name.",
            missing.ToMessage("NavalConsole", CultureInfo.GetCultureInfo("en-US")));
        Assert.AreEqual("Le projet NavalConsole fait référence au projet NavalLib : aucun projet de l'espace de travail ne porte ce nom.",
            missing.ToMessage("NavalConsole", CultureInfo.GetCultureInfo("fr-CA")));
        Assert.AreEqual(
            "The project NavalConsole references the project NavalLib: more than one project of the workspace has that name, in any letter case.",
            shared.ToMessage("NavalConsole", CultureInfo.GetCultureInfo("en-US")));
        Assert.AreEqual(
            "Le projet NavalConsole fait référence au projet NavalLib : plus d'un projet de l'espace de travail porte ce nom, sans égard à la casse.",
            shared.ToMessage("NavalConsole", CultureInfo.GetCultureInfo("fr-CA")));
    }
}
