using System.Globalization;
using RDCore.SDK.Server.Configuration;

namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// Every annotation the platform knows: the ones each extension declares (<see cref="ExtensionAnnotationsAttribute"/>), the core extension's
/// among them. It answers what an annotation is called, where it is legal, and how it is described.
/// </summary>
/// <remarks>
/// An annotation that two extensions declare, under a name or an alias, is the first one's: the extensions in the order they are given.
/// </remarks>
public sealed class AnnotationCatalogue
{
    private readonly (AnnotationDeclaration Declaration, AnnotationSet Set)[] _annotations;

    /// <summary>
    /// The catalogue of the annotations of some extensions.
    /// </summary>
    /// <param name="sets">The annotations of each extension, in the order the platform lists the extensions.</param>
    public AnnotationCatalogue(IEnumerable<AnnotationSet> sets)
    {
        var annotations = new List<(AnnotationDeclaration, AnnotationSet)>();
        foreach (var set in sets)
        {
            foreach (var declaration in set.Declarations)
            {
                if (!annotations.Any(known => known.Item1.IsCalled(declaration.Name) || declaration.Aliases.Any(known.Item1.IsCalled)))
                {
                    annotations.Add((declaration, set));
                }
            }
        }

        _annotations = [.. annotations];
    }

    /// <summary>
    /// Every annotation of the catalogue, each once.
    /// </summary>
    public IEnumerable<AnnotationDeclaration> Declarations => _annotations.Select(annotation => annotation.Declaration);

    /// <summary>
    /// The annotation written under a name, its own or one of its aliases; case does not matter (<strong>MS-VBAL §3.3.5.1</strong>).
    /// </summary>
    /// <param name="name">The name written after <c>'@</c>.</param>
    /// <returns>The annotation, or <see langword="null"/> when no extension declares one of that name.</returns>
    public AnnotationDeclaration? Find(string name) => _annotations.FirstOrDefault(annotation => annotation.Declaration.IsCalled(name)).Declaration;

    /// <summary>
    /// The annotations legal at a place: of a target there, in a kind of module.
    /// </summary>
    /// <param name="target">What an annotation written there annotates; several flags for a place that annotates several things.</param>
    /// <param name="kind">The kind of the module.</param>
    public IEnumerable<AnnotationDeclaration> LegalAt(AnnotationTargets target, AnnotationModuleKinds kind)
        => Declarations.Where(declaration => (declaration.Targets & target) != 0 && (declaration.ModuleKinds & kind) != 0);

    /// <summary>
    /// The description of an annotation in a language: <paramref name="culture"/>'s, or the platform's first when its provider has none in it.
    /// </summary>
    /// <param name="annotation">An annotation of the catalogue.</param>
    /// <param name="culture">The language of the user interface.</param>
    /// <returns>The description, or <see langword="null"/> when the annotation is not the catalogue's, or its provider describes it in no language.</returns>
    public string? DescriptionOf(AnnotationDeclaration annotation, CultureInfo culture) => Describe(annotation, AnnotationSet.KeyOf(annotation), culture);

    /// <summary>
    /// The description of a parameter of an annotation in a language: <paramref name="culture"/>'s, or the platform's first when its provider has none in it.
    /// </summary>
    /// <param name="annotation">An annotation of the catalogue.</param>
    /// <param name="parameter">One of its parameters.</param>
    /// <param name="culture">The language of the user interface.</param>
    /// <returns>
    /// The description, or <see langword="null"/> when the annotation is not the catalogue's, or its provider describes the parameter in no language.
    /// </returns>
    public string? DescriptionOf(AnnotationDeclaration annotation, AnnotationParameter parameter, CultureInfo culture)
        => Describe(annotation, AnnotationSet.KeyOf(annotation, parameter), culture);

    private string? Describe(AnnotationDeclaration annotation, string key, CultureInfo culture)
    {
        if (_annotations.FirstOrDefault(known => known.Declaration.IsCalled(annotation.Name)).Set is not { } set)
        {
            return null;
        }

        return (set.Descriptions.GetValueOrDefault(culture.TwoLetterISOLanguageName) is { } local && local.TryGetValue(key, out var text) ? text : null)
            ?? (set.Descriptions.GetValueOrDefault(SettingsSchema.Languages[0]) is { } first && first.TryGetValue(key, out var fallback) ? fallback : null);
    }
}
