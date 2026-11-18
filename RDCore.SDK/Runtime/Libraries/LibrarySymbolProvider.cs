using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Runtime.Abstract.Execution;
using RDCore.SDK.Runtime.StdLib;
using RDCore.SDK.Workspace;
using System.Collections.Immutable;

namespace RDCore.SDK.Runtime.Libraries;

/// <summary>
/// The symbols of the libraries a project references: the ones that are not the standard library, which is the platform's own
/// (<c>StdLibSymbolProvider</c>).
/// </summary>
/// <remarks>
/// The language server and the environment host each build their symbols from the same libraries, with this, so that a name the language server bound
/// binds to the same symbol in the host - which is why the symbols are addressed under the workspace and nothing else decides their address.
/// </remarks>
public sealed class LibrarySymbolProvider : ISymbolProvider
{
    private readonly Uri _workspaceRoot;
    private readonly bool _is64Bit;

    /// <summary>
    /// Creates the provider for libraries that are loaded.
    /// </summary>
    /// <param name="workspaceRoot">The workspace the symbols are addressed under.</param>
    /// <param name="libraries">The libraries (<see cref="Load(ILibrarySource, RDCoreProject, SupportedLanguage?)"/>).</param>
    /// <param name="is64Bit">The pointer width of the environment.</param>
    public LibrarySymbolProvider(Uri workspaceRoot, ReferencedLibraries libraries, bool is64Bit = true)
    {
        _workspaceRoot = workspaceRoot;
        _is64Bit = is64Bit;
        Libraries = libraries;
    }

    /// <summary>
    /// The libraries, and the ones that could not be loaded.
    /// </summary>
    public ReferencedLibraries Libraries { get; }

    /// <summary>
    /// Loads the libraries a project references.
    /// </summary>
    /// <param name="source">Where the descriptions are.</param>
    /// <param name="references">
    /// The references of the project. A reference that gives the identifier of a type library (<see cref="RDCoreReference.Guid"/>) is to the description made from
    /// it, of its <see cref="RDCoreReference.Major"/> version and of exactly its <see cref="RDCoreReference.Minor"/> one, or else the highest above it
    /// (<see cref="LibraryIdentity"/>); one that gives none is to the library of its name.
    /// </param>
    /// <param name="language">
    /// The language the project is written in, whose implicit references (<see cref="SupportedLanguage.ImplicitReferences"/>) come before the ones the project lists;
    /// RD-VBA, which has none, when it is not given.
    /// </param>
    public static ReferencedLibraries Load(ILibrarySource source, IEnumerable<RDCoreReference> references, SupportedLanguage? language = null)
        => ReferencedLibraries.Load(source, Referenced(references, language?.ImplicitReferences ?? []), [StandardLibrary.LibraryName]);

    /// <summary>
    /// Loads the libraries a project references: the ones its language and its modules reference implicitly
    /// (<see cref="SupportedLanguage.ImplicitReferencesOf"/>), then the ones its project file lists.
    /// </summary>
    /// <param name="source">Where the descriptions are.</param>
    /// <param name="project">
    /// The project: its references (<see cref="RDCoreProject.References"/>) and its modules (<see cref="RDCoreProject.Modules"/>).
    /// </param>
    /// <param name="language">
    /// The language the project is written in; RD-VBA when it is not given. RD-VBA has no implicit reference of its own, so a project written in it
    /// references RDForms implicitly only when it has an RDForms form.
    /// </param>
    /// <returns>The libraries, and the ones that could not be loaded.</returns>
    public static ReferencedLibraries Load(ILibrarySource source, RDCoreProject project, SupportedLanguage? language = null)
        => ReferencedLibraries.Load(source, Referenced(project.References ?? [], ImplicitReferencesOf(project, language)), [StandardLibrary.LibraryName]);

    /// <summary>
    /// The libraries a project references, by name, each at its position in the list of its references, as a session ranks them
    /// (<see cref="IRuntimeSession.References"/>): the standard library at <c>0</c>, then the ones its language and its modules reference implicitly
    /// (<see cref="SupportedLanguage.ImplicitReferencesOf"/>), then the ones its project file lists, in the order
    /// <see cref="Load(ILibrarySource, RDCoreProject, SupportedLanguage?)"/> loads them.
    /// </summary>
    /// <remarks>
    /// A second reference to a library already in the list is left out, as <see cref="Load(ILibrarySource, RDCoreProject, SupportedLanguage?)"/> leaves
    /// it out, and so is a reference to the standard library in the project file (<see cref="RDCoreReference.IsStandardLibrary"/>: by its name or by its
    /// type library's identifier). A reference that does not load keeps its position.
    /// </remarks>
    /// <param name="project">
    /// The project: its references (<see cref="RDCoreProject.References"/>) and its modules (<see cref="RDCoreProject.Modules"/>).
    /// </param>
    /// <param name="language">The language the project is written in; RD-VBA when it is not given.</param>
    /// <returns>The references, the standard library first.</returns>
    public static IReadOnlyList<ReferencePriorityInfo> ReferencePriorities(RDCoreProject project, SupportedLanguage? language = null)
        => [new ReferencePriorityInfo(StandardLibrary.LibraryName, 0),
            .. Referenced((project.References ?? []).Where(reference => !reference.IsStandardLibrary), ImplicitReferencesOf(project, language))
                .Select(reference => reference.Name)
                .Where(name => !string.Equals(name, StandardLibrary.LibraryName, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((name, rank) => new ReferencePriorityInfo(name, rank + 1))];

    private static IReadOnlyList<string> ImplicitReferencesOf(RDCoreProject project, SupportedLanguage? language)
        => (language ?? SupportedLanguages.RDVBA).ImplicitReferencesOf((project.Modules ?? []).Select(module => module?.RelativeUri));

    // the implicit references come before the ones the project lists (RD-VBAL §2.3.1.3).
    private static IEnumerable<LibraryIdentity> Referenced(IEnumerable<RDCoreReference> references, IEnumerable<string> implicitReferences)
        => [
            .. implicitReferences.Select(name => new LibraryIdentity(name)),
            .. references.Select(reference => new LibraryIdentity(reference.Name, reference.Guid, reference.Major, reference.Minor)),
        ];

    /// <summary>
    /// Reads the symbols of a library whether or not the project references it, and of the libraries it depends on: what tooling knows of the class of an object
    /// created by name (<see cref="Semantics.ObjectCreationFact"/>).
    /// </summary>
    /// <remarks>
    /// The symbols are never names of the project: the workspace is not composed with them, and no code can use them. They are addressed as the symbols of a
    /// referenced library are, so that the class they describe is the same one when the project does reference it.
    /// </remarks>
    /// <param name="source">Where the descriptions are.</param>
    /// <param name="workspaceRoot">The workspace the symbols are addressed under.</param>
    /// <param name="library">The name of the library.</param>
    /// <param name="is64Bit">The pointer width of the environment.</param>
    /// <returns>The symbols; none when the source has no library of the name, or it cannot be loaded.</returns>
    public static ImmutableArray<Symbol> ReadDescribed(ILibrarySource source, Uri workspaceRoot, string library, bool is64Bit = true)
    {
        var libraries = ReferencedLibraries.Load(source, [library], [StandardLibrary.LibraryName]);
        return libraries.Libraries.Any(loaded => string.Equals(loaded.Name, library, StringComparison.OrdinalIgnoreCase))
            ? new LibrarySymbolReader(workspaceRoot, is64Bit).Read(libraries.Libraries, libraries.Priorities)
            : [];
    }

    /// <inheritdoc/>
    public IEnumerable<Symbol> ProvideSymbols()=> new LibrarySymbolReader(_workspaceRoot, _is64Bit).Read(Libraries.Libraries, Libraries.Priorities);
}
