namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// The annotations one extension provides, with their descriptions in each language of the platform, as its assembly declares them
/// (<see cref="ExtensionAnnotationsAttribute.Read"/>).
/// </summary>
public sealed record class AnnotationSet
{
    /// <summary>
    /// The annotations.
    /// </summary>
    public required AnnotationDeclaration[] Declarations { get; init; }

    /// <summary>
    /// The description of each annotation and of each of its parameters, by its key (<see cref="KeyOf(AnnotationDeclaration)"/>,
    /// <see cref="KeyOf(AnnotationDeclaration, AnnotationParameter)"/>), in each language of the platform, by its two-letter name
    /// (<see cref="Server.Configuration.SettingsSchema.Languages"/>). Read from an extension's resources (<see cref="ExtensionAnnotationsAttribute.Read"/>), a
    /// text the provider has none of in a language is the one its resources fall back on; a text it has in no language is missing from every one.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Descriptions { get; init; } = [];

    /// <summary>
    /// The key of an annotation's description: its name, <c>Description</c>.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    public static string KeyOf(AnnotationDeclaration annotation) => annotation.Name;

    /// <summary>
    /// The key of a parameter's description: the annotation's name and the parameter's, joined by an underscore, <c>Description_Text</c>.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="parameter">One of its parameters.</param>
    public static string KeyOf(AnnotationDeclaration annotation, AnnotationParameter parameter) => $"{annotation.Name}_{parameter.Name}";
}
