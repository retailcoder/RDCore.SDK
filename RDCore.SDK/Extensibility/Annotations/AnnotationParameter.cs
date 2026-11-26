namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// A parameter of an annotation (<see cref="AnnotationDeclaration.Parameters"/>): what signature help shows for its argument.
/// </summary>
/// <remarks>
/// Its description is not here: it is a string of the provider's resources, keyed <c>Annotation_Parameter</c> (<see cref="AnnotationSet.Descriptions"/>).
/// </remarks>
public sealed record class AnnotationParameter
{
    /// <summary>
    /// The name of the parameter, as signature help shows it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the parameter takes.
    /// </summary>
    public required AnnotationArgumentKind Takes { get; init; }

    /// <summary>
    /// Whether an argument for it may be left out; only the parameters after the required ones may be.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Whether it takes any number of arguments, as a list: only the last parameter may.
    /// </summary>
    public bool Repeats { get; init; }
}
