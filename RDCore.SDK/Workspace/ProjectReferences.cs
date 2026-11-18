using System.Collections.Immutable;
using System.Globalization;
using System.Resources;

namespace RDCore.SDK.Workspace;

/// <summary>
/// Why a reference to a project names no project of the workspace (<see cref="ProjectReferenceResolution.Refusal"/>).
/// </summary>
public enum ProjectReferenceRefusal
{
    /// <summary>
    /// No project of the workspace has the name the reference gives.
    /// </summary>
    NoProject,

    /// <summary>
    /// Two or more projects of the workspace have the name the reference gives, in any letter case: the name names none of them until all but one are
    /// renamed.
    /// </summary>
    SharedName,
}

/// <summary>
/// A reference to a project (<see cref="RDCoreReference.IsProject"/>), and the project of the workspace it names (<strong>RD-VBAL §2.2.3.2</strong>).
/// </summary>
/// <param name="Reference">The reference, as the project file lists it.</param>
/// <param name="Index">
/// The position of the project the reference names among the projects of the workspace it was resolved among; <c>-1</c> when it names none.
/// </param>
/// <param name="Refusal">Why the reference names no project; <see langword="null"/> when it names one.</param>
public sealed record class ProjectReferenceResolution(RDCoreReference Reference, int Index, ProjectReferenceRefusal? Refusal)
{
    private static readonly ResourceManager Messages = new("RDCore.SDK.Workspace.ProjectReferenceMessages", typeof(ProjectReferenceResolution).Assembly);

    /// <summary>
    /// Whether the reference names a project of the workspace: <see cref="Index"/> is its position, and <see cref="Refusal"/> is <see langword="null"/>.
    /// </summary>
    public bool IsResolved => Refusal is null;

    /// <summary>
    /// Why the reference names no project, as the user is told it, naming the project that references it and the name it gives.
    /// </summary>
    /// <param name="referencingProject">The name of the project whose file lists the reference.</param>
    /// <param name="culture">The language of the reader; the current UI culture by default.</param>
    /// <returns>The message; <see langword="null"/> when the reference names a project.</returns>
    public string? ToMessage(string referencingProject, CultureInfo? culture = null)
    {
        if (Refusal is not { } refusal)
        {
            return null;
        }

        culture ??= CultureInfo.CurrentUICulture;
        return string.Format(culture, Messages.GetString($"ProjectReference_{refusal}", culture) ?? "{0}: {1}", referencingProject, Reference.Name);
    }
}

/// <summary>
/// Resolves the references to projects of a project file (<see cref="RDCoreReference.IsProject"/>) among the projects of the workspace
/// (<strong>RD-VBAL §2.2.3.2</strong>). Every platform component that binds or loads a referenced project resolves its references with this rule.
/// </summary>
/// <remarks>
/// A reference to a project names the one project of the workspace whose <see cref="RDCoreProject.Name"/> is the name it gives, in any letter case. A name
/// that no project has names none (<see cref="ProjectReferenceRefusal.NoProject"/>), and so does a name that two or more projects have
/// (<see cref="ProjectReferenceRefusal.SharedName"/>). A reference to the project itself names it: that is a circular reference of projects, which
/// <see cref="ProjectReferenceCycles"/> finds.
/// </remarks>
public static class ProjectReferences
{
    /// <summary>
    /// The project of the workspace that a reference to a project names.
    /// </summary>
    /// <param name="reference">The reference; its <see cref="RDCoreReference.Name"/> is what is resolved, whether it is to a project or not.</param>
    /// <param name="projects">The projects of the workspace, in its order.</param>
    /// <returns>The resolution: the position of the project the reference names among <paramref name="projects"/>, or why it names none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="projects"/> is <see langword="null"/>.</exception>
    public static ProjectReferenceResolution Resolve(RDCoreReference reference, IReadOnlyList<RDCoreProject> projects)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(projects);

        var found = -1;
        for (var index = 0; index < projects.Count; index++)
        {
            if (!string.Equals(projects[index].Name, reference.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (found >= 0)
            {
                return new ProjectReferenceResolution(reference, -1, ProjectReferenceRefusal.SharedName);
            }

            found = index;
        }

        return found < 0
            ? new ProjectReferenceResolution(reference, -1, ProjectReferenceRefusal.NoProject)
            : new ProjectReferenceResolution(reference, found, null);
    }

    /// <summary>
    /// The references to projects of a project (<see cref="RDCoreProject.ProjectReferences"/>), in the order its file lists them, each with the project of
    /// the workspace it names. Its references to libraries are left out.
    /// </summary>
    /// <param name="project">The project that references.</param>
    /// <param name="projects">The projects of the workspace, in its order; <paramref name="project"/> may be among them.</param>
    /// <returns>A resolution for each reference to a project; none when the project references no project.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="project"/> or <paramref name="projects"/> is <see langword="null"/>.</exception>
    public static ImmutableArray<ProjectReferenceResolution> Of(RDCoreProject project, IReadOnlyList<RDCoreProject> projects)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projects);

        return [.. project.ProjectReferences.Select(reference => Resolve(reference, projects))];
    }
}
