using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Runtime.Libraries;

/// <summary>
/// A source of library descriptions that is several sources in order: for each name, the first source that has a library of that name gives its descriptions.
/// </summary>
/// <remarks>
/// A package that describes a library it provides comes before the platform's <c>Symbols</c> folder (<see cref="DirectoryLibrarySource"/>), so that its
/// description of the library replaces the folder's file of the same name, and the folder still gives every other library.
/// </remarks>
/// <param name="sources">The sources, the first of them first; an empty list has no library.</param>
public sealed class CompositeLibrarySource(params IReadOnlyList<ILibrarySource> sources) : ILibrarySource
{
    /// <summary>
    /// The sources, in the order they are asked.
    /// </summary>
    public IReadOnlyList<ILibrarySource> Sources { get; } = sources ?? throw new ArgumentNullException(nameof(sources));

    /// <summary>
    /// The names of the libraries of every source, each once, the first source's first.
    /// </summary>
    public IEnumerable<string> Names => Sources.SelectMany(source => source.Names).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the description of the library of a name from the first source that has one.
    /// </summary>
    /// <param name="name">The name of the library; the case of it does not matter.</param>
    /// <param name="description">The description.</param>
    /// <returns><see langword="false"/> when no source has a library of the name.</returns>
    /// <exception cref="InvalidDataException">The first source that has one cannot read it.</exception>
    public bool TryGet(string name, [NotNullWhen(true)] out LibraryDescription? description)
    {
        foreach (var source in Sources)
        {
            if (source.TryGet(name, out description))
            {
                return true;
            }
        }

        description = null;
        return false;
    }

    /// <summary>
    /// The descriptions of the libraries of a name, from the first source that has any: a later source's descriptions of that name are not given.
    /// </summary>
    /// <param name="name">The name of the libraries.</param>
    /// <returns>The descriptions of the first source that has any of that name; none when no source has one.</returns>
    /// <exception cref="InvalidDataException">The first source that has one cannot read it.</exception>
    public IEnumerable<LibraryDescription> DescriptionsOf(string name)
    {
        foreach (var source in Sources)
        {
            var descriptions = source.DescriptionsOf(name).ToArray();
            if (descriptions.Length > 0)
            {
                return descriptions;
            }
        }

        return [];
    }
}
