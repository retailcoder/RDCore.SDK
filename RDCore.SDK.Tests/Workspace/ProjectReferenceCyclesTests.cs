using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// A project can't reference itself, or a project that references it, directly or through other projects: the finder reports each such cycle once, with
/// its path (<strong>RD-VBAL §2.2.3.2</strong>).
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.2.3.2 RDCoreReference")]
public sealed class ProjectReferenceCyclesTests
{
    private static RDCoreProject Project(string name, params string[] projects)
        => new() { Name = name, References = [.. projects.Select(project => new RDCoreReference { Name = project, IsProject = true })] };

    private static string[][] Paths(params RDCoreProject[] projects)
        => [.. ProjectReferenceCycles.Find(projects).Select(cycle => cycle.Path.ToArray())];

    private static void AssertPaths(string[][] expected, string[][] actual)
    {
        Assert.HasCount(expected.Length, actual, string.Join(" | ", actual.Select(path => string.Join(" → ", path))));
        for (var index = 0; index < expected.Length; index++)
        {
            CollectionAssert.AreEqual(expected[index], actual[index], $"cycle {index}");
        }
    }

    [TestMethod]
    public void ProjectsWithoutACycle_HaveNone()
    {
        AssertPaths([], Paths(Project("NavalConsole", "NavalLib"), Project("NavalWindow", "NavalLib"), Project("NavalLib")));
    }

    [TestMethod]
    public void TwoProjectsReferencingEachOther_AreOneCycle_FromTheFirst()
    {
        AssertPaths([["NavalLib", "NavalWindow", "NavalLib"]], Paths(Project("NavalLib", "NavalWindow"), Project("NavalWindow", "NavalLib")));
    }

    [TestMethod]
    public void AProjectReferencingItself_IsACycleOfItsNameTwice()
    {
        AssertPaths([["NavalLib", "NavalLib"]], Paths(Project("NavalLib", "NavalLib")));
    }

    // the cycle is reported from the project that comes first in the workspace, whichever one the references start at.
    [TestMethod]
    public void ACycleThroughThreeProjects_StartsAtItsFirstProjectInTheWorkspace()
    {
        AssertPaths([["A", "B", "C", "A"]], Paths(Project("A", "B"), Project("C", "A"), Project("B", "C")));
    }

    // three projects that each reference the other two: five cycles, each once, in the order of their first project, then of the references.
    [TestMethod]
    public void CyclesSharingProjects_AreEachReportedOnce()
    {
        AssertPaths(
            [["A", "B", "A"], ["A", "B", "C", "A"], ["A", "C", "A"], ["A", "C", "B", "A"], ["B", "C", "B"]],
            Paths(Project("A", "B", "C"), Project("B", "A", "C"), Project("C", "A", "B")));
    }

    [TestMethod]
    public void AReferenceToALibraryOfAProjectsName_IsOnNoCycle()
    {
        var library = new RDCoreProject { Name = "NavalWindow", References = [new RDCoreReference { Name = "NavalLib" }] };

        AssertPaths([], Paths(Project("NavalLib", "NavalWindow"), library));
    }

    [TestMethod]
    public void AReferenceToANameNoProjectHas_IsOnNoCycle()
    {
        AssertPaths([], Paths(Project("NavalLib", "NavalTools"), Project("NavalWindow", "NavalLib")));
    }

    [TestMethod]
    public void AReferenceToAProject_IsResolvedInAnyCase_AndThePathHasTheNamesTheProjectsHave()
    {
        AssertPaths([["NavalLib", "NavalWindow", "NavalLib"]], Paths(Project("NavalLib", "NAVALWINDOW"), Project("NavalWindow", "navallib")));
    }

    // two projects named NavalLib: a reference to that name resolves to neither, so the cycle it would close isn't one.
    [TestMethod]
    public void AReferenceToANameTwoProjectsHave_IsOnNoCycle()
    {
        AssertPaths([], Paths(Project("NavalLib", "NavalWindow"), Project("NavalWindow", "NavalLib"), Project("navallib")));
    }

    [TestMethod]
    public void AProjectReferencedTwice_IsOneEdge_AndTheCycleIsReportedOnce()
    {
        AssertPaths([["NavalLib", "NavalWindow", "NavalLib"]], Paths(Project("NavalLib", "NavalWindow", "navalwindow"), Project("NavalWindow", "NavalLib")));
    }
}
