using System.Globalization;
using System.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RDCore.SDK.Runtime.Libraries;
using RDCore.SDK.Workspace;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// A change of the project that fixes a finding: what the project file (<see cref="ProjectFile"/>) is to say of the module the finding is in, or of the
/// libraries the project references, which the language server writes in the project file of the workspace when the fix is taken.
/// </summary>
/// <remarks>
/// <para>
/// An analyzer is not given the project file, and does not edit its text: it says what is to change (<see cref="Changes"/>), and the platform changes the
/// project, writes its project file and loads the workspace the file then lists. The code action of such a fix is a command the language server runs
/// (<see cref="Semantics.Refactoring.ServerCommands.ChangeProject"/>), which is offered only when every change can be made (<see cref="ApplyTo"/>).
/// </para>
/// <para>
/// No change edits the text of a module: a module that is removed from the project keeps its file where it is, and is not loaded.
/// </para>
/// </remarks>
public sealed record class ProjectFix
{
    /// <summary>
    /// What the code action that changes the project is called, in the language of the analyzer's messages: what the project file is to say, as
    /// <c>Reference the Excel library</c>.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The changes, made in this order.
    /// </summary>
    [JsonProperty("changes")]
    public IReadOnlyList<ProjectChange> Changes { get; init; } = [];

    /// <summary>
    /// Whether the changes change what the code does, as removing a module whose code runs does. Such a fix is never one a client may apply without
    /// showing it (<strong>LSP 3.17</strong> <c>CodeAction.isPreferred</c>).
    /// </summary>
    [JsonProperty("changesBehavior", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ChangesBehavior { get; init; }

    /// <summary>
    /// The project, with the changes made: each change of <see cref="Changes"/>, in order, to the project the one before it made.
    /// </summary>
    /// <param name="project">The project the finding was made of.</param>
    /// <param name="module">The URI of the document the finding is in: the module a change of a module changes.</param>
    /// <param name="libraries">The descriptions of the libraries a project can reference: what a reference that is added refers to.</param>
    /// <param name="refused">The first change that cannot be made; <see langword="null"/> when every one can.</param>
    /// <returns>
    /// The changed project, which is the project itself when no change changes it; <see langword="null"/> when a change cannot be made, and none of them is.
    /// </returns>
    public ProjectFile? ApplyTo(ProjectFile project, Uri module, ILibrarySource libraries, out ProjectChange? refused)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(libraries);

        var changed = project;
        foreach (var change in Changes)
        {
            if (change.ApplyTo(changed, module, libraries) is not { } next)
            {
                refused = change;
                return null;
            }

            changed = next;
        }

        refused = null;
        return changed;
    }
}

/// <summary>
/// What a change of the project (<see cref="ProjectChange"/>) changes.
/// </summary>
public enum ProjectChangeKind
{
    /// <summary>
    /// The project references a library (<see cref="ProjectChange.Library"/>), after the libraries it references already.
    /// </summary>
    AddReference,

    /// <summary>
    /// The module the finding is in is removed from the project: the project file does not list it. Its file is kept.
    /// </summary>
    RemoveModule,

    /// <summary>
    /// The module the finding is in is the code of a host document of a class (<see cref="ProjectChange.DocumentClass"/>), which the project file states as
    /// its <see cref="RDCoreModule.Super"/>.
    /// </summary>
    SetDocumentClass,
}

/// <summary>
/// One change of the project, which a <see cref="ProjectFix"/> makes: a library the project is to reference, or what it is to say of the module a finding is
/// in.
/// </summary>
public sealed record class ProjectChange
{
    /// <summary>
    /// What the change changes.
    /// </summary>
    [JsonProperty("kind")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ProjectChangeKind Kind { get; init; }

    /// <summary>
    /// The name of the library the project is to reference (<see cref="ProjectChangeKind.AddReference"/>), as source code qualifies its types with it:
    /// <c>Excel</c>. <see langword="null"/> for another change.
    /// </summary>
    [JsonProperty("library", NullValueHandling = NullValueHandling.Ignore)]
    public string? Library { get; init; }

    /// <summary>
    /// The class of the host document the module is the code of (<see cref="ProjectChangeKind.SetDocumentClass"/>); <see langword="null"/> for another change.
    /// </summary>
    [JsonProperty("documentClass", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public DocClassType? DocumentClass { get; init; }

    /// <summary>
    /// The project is to reference a library.
    /// </summary>
    /// <param name="library">The name of the library: <c>Excel</c>.</param>
    public static ProjectChange AddReference(string library) => new() { Kind = ProjectChangeKind.AddReference, Library = library };

    /// <summary>
    /// The module the finding is in is to be removed from the project.
    /// </summary>
    public static ProjectChange RemoveModule() => new() { Kind = ProjectChangeKind.RemoveModule };

    /// <summary>
    /// The module the finding is in is to be the code of a host's document of a class.
    /// </summary>
    /// <param name="documentClass">The class of the document.</param>
    public static ProjectChange SetDocumentClass(DocClassType documentClass) => new() { Kind = ProjectChangeKind.SetDocumentClass, DocumentClass = documentClass };

    /// <summary>
    /// The project, with the change made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A library is referenced after every library the project references already: a reference that occurs earlier in the list has the higher precedence
    /// (<strong>MS-VBAL §4.1</strong>), so a name that one of them declares too keeps meaning theirs. The reference is to the type library the library's
    /// description was made from, by its identifier and version (<see cref="LibraryIdentity.Of"/>). A project that references a library of the name already is
    /// not changed. When no library description has the name, the change cannot be made.
    /// </para>
    /// <para>
    /// The module is the one the project lists at the path of the document, relative to the project's folder (<see cref="ProjectFile.Uri"/>), whichever
    /// separators the project file writes it with. A module the project does not list cannot be changed.
    /// </para>
    /// </remarks>
    /// <param name="project">The project.</param>
    /// <param name="module">The URI of the document the finding is in.</param>
    /// <param name="libraries">The descriptions of the libraries a project can reference.</param>
    /// <returns>The changed project, which is the project itself when it says so already; <see langword="null"/> when the change cannot be made.</returns>
    public ProjectFile? ApplyTo(ProjectFile project, Uri module, ILibrarySource libraries)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(libraries);

        switch (Kind)
        {
            case ProjectChangeKind.AddReference:
                if (string.IsNullOrWhiteSpace(Library))
                {
                    return null;
                }

                if (project.ProjectInfo.References.Any(reference => string.Equals(reference.Name, Library, StringComparison.OrdinalIgnoreCase)))
                {
                    return project;
                }

                if (!libraries.TryGet(Library, out var description))
                {
                    return null;
                }

                var identity = LibraryIdentity.Of(description);
                return project.WithReference(new RDCoreReference { Name = identity.Name, Guid = identity.Guid, Major = identity.Major, Minor = identity.Minor });

            case ProjectChangeKind.RemoveModule:
                return ListedModule(project, module) is { } removed ? project.WithoutModule(removed) : null;

            case ProjectChangeKind.SetDocumentClass:
                if (DocumentClass is not { } documentClass || ListedModule(project, module) is not { } listed)
                {
                    return null;
                }

                return listed.Super == documentClass
                    ? project
                    : project with
                    {
                        ProjectInfo = project.ProjectInfo with
                        {
                            Modules = [.. project.ProjectInfo.Modules.Select(entry => ReferenceEquals(entry, listed) ? entry with { Super = documentClass } : entry)],
                        },
                        IsDirty = true,
                    };

            default:
                return null;
        }
    }

    // the module the project lists at the document's path: a project file written on one platform is read on another, so either separator is a separator.
    private static RDCoreModule? ListedModule(ProjectFile project, Uri module)
    {
        if (!module.IsFile || string.IsNullOrEmpty(project.Uri))
        {
            return null;
        }

        var path = Normalized(Path.GetRelativePath(project.Uri, module.LocalPath));
        return project.ProjectInfo.Modules.FirstOrDefault(entry => string.Equals(Normalized(entry.RelativeUri), path, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalized(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("./", StringComparison.Ordinal) ? normalized[2..] : normalized;
    }
}

/// <summary>
/// Why a change of the project that fixes a finding (<see cref="ProjectChange"/>) is not made, in the language of the reader, the current UI culture by
/// default: what a language server answers a client whose project changed since the fix was offered.
/// </summary>
public static class ProjectFixText
{
    private static readonly ResourceManager Messages = new("RDCore.SDK.Extensibility.Diagnostics.ProjectFixMessages", typeof(ProjectFixText).Assembly);

    /// <summary>
    /// Why a change cannot be made (<see cref="ProjectChange.ApplyTo"/>).
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="module">The module the finding is in, as the user knows it: the path of its file.</param>
    /// <param name="culture">The language of the reader; the current UI culture by default.</param>
    public static string Refused(ProjectChange change, string module, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(change);
        culture ??= CultureInfo.CurrentUICulture;
        var (key, subject) = change.Kind is ProjectChangeKind.AddReference
            ? ("ProjectFix_Refused_AddReference", change.Library ?? string.Empty)
            : ("ProjectFix_Refused_Module", module);
        return string.Format(culture, Messages.GetString(key, culture) ?? key, subject);
    }
}
