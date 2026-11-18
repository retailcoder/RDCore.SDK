using System.Collections.Immutable;

namespace RDCore.SDK.Workspace;

/// <summary>
/// A circular reference among the projects of a workspace: the projects on it, each referencing the next, from the one it starts at back to that one, as
/// <c>NavalLib → NavalWindow → NavalLib</c> (<see cref="RDCoreReference.IsProject"/>).
/// </summary>
/// <param name="Path">The names of the projects on the cycle, as each project names itself, the first repeated last; a project that references itself is
/// a path of two, its name twice.</param>
public sealed record class ProjectReferenceCycle(ImmutableArray<string> Path);

/// <summary>
/// Finds the circular references among the projects of a workspace. A project can't reference itself, or a project that references it, directly or through
/// other projects (<strong>RD-VBAL §2.2.3.2</strong>).
/// </summary>
public static class ProjectReferenceCycles
{
    /// <summary>
    /// Finds every cycle of references among <paramref name="projects"/>, each once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reference to a project is resolved by its name, in any case, among <paramref name="projects"/>. A reference to a library, and one to a name that no
    /// project has, are on no cycle. A name that two projects have resolves to neither: the projects that share it can't be referenced until one is renamed.
    /// </para>
    /// <para>
    /// Each cycle starts at its project that comes first in <paramref name="projects"/>, and the cycles are in that order, then in the order of the
    /// references, so a workspace's cycles are found the same way each time.
    /// </para>
    /// </remarks>
    /// <param name="projects">The projects of the workspace, in its order.</param>
    /// <returns>The cycles; none when the projects reference one another without one.</returns>
    public static ImmutableArray<ProjectReferenceCycle> Find(IEnumerable<RDCoreProject> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        var all = projects.ToArray();
        var named = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var shared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < all.Length; index++)
        {
            if (!named.TryAdd(all[index].Name, index))
            {
                shared.Add(all[index].Name);
            }
        }

        var references = all
            .Select(project => project.ProjectReferences
                .Where(reference => !shared.Contains(reference.Name) && named.ContainsKey(reference.Name))
                .Select(reference => named[reference.Name])
                .Distinct()
                .ToArray())
            .ToArray();

        var cycles = ImmutableArray.CreateBuilder<ProjectReferenceCycle>();
        var path = new List<int>();
        for (var start = 0; start < all.Length; start++)
        {
            path.Add(start);
            Walk(start, start);
            path.Clear();
        }

        return cycles.ToImmutable();

        // the paths from the start through projects after it in the workspace's order only: a cycle through an earlier project was found from that one.
        void Walk(int start, int at)
        {
            foreach (var next in references[at])
            {
                if (next == start)
                {
                    cycles.Add(new ProjectReferenceCycle([.. path.Select(index => all[index].Name), all[start].Name]));
                }
                else if (next > start && !path.Contains(next))
                {
                    path.Add(next);
                    Walk(start, next);
                    path.RemoveAt(path.Count - 1);
                }
            }
        }
    }
}
