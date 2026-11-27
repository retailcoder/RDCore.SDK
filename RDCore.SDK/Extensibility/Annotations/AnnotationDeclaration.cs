using System.Text.Json.Serialization;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// What an annotation is: its name, where it is legal, its parameters, and whether it is deprecated. Each extension declares its own
/// (<see cref="ExtensionAnnotationsAttribute"/>), and a catalogue holds every extension's (<see cref="AnnotationCatalogue"/>).
/// </summary>
/// <remarks>
/// The declaration says what an annotation looks like, and nothing of what it does: that is its provider's. Its description and its parameters' are strings of
/// the provider's resources, in each language of the platform (<see cref="AnnotationSet.Descriptions"/>).
/// </remarks>
public sealed record class AnnotationDeclaration
{
    /// <summary>
    /// The name of the annotation, written after <c>'@</c>: <c>Description</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The other names it is written as, such as a legacy spelling; none for most.
    /// </summary>
    public string[] Aliases { get; init; } = [];

    /// <summary>
    /// What it annotates; an annotation of several targets is legal at each.
    /// </summary>
    public required AnnotationTargets Targets { get; init; }

    /// <summary>
    /// The kinds of module it may be written in.
    /// </summary>
    public AnnotationModuleKinds ModuleKinds { get; init; } = AnnotationModuleKinds.All;

    /// <summary>
    /// Whether it may be written more than once for the same target.
    /// </summary>
    public bool Repeats { get; init; }

    /// <summary>
    /// Its parameters, in order: the required ones, then the optional ones, the last of which may repeat.
    /// </summary>
    public AnnotationParameter[] Parameters { get; init; } = [];

    /// <summary>
    /// Whether it is deprecated: still read, and reported where it is written.
    /// </summary>
    public bool IsDeprecated { get; init; }

    /// <summary>
    /// The name of the annotation that replaces a deprecated one; <see langword="null"/> when none does.
    /// </summary>
    public string? ReplacedBy { get; init; }

    /// <summary>
    /// The fewest arguments it takes: one for each parameter that is not optional, a list among them.
    /// </summary>
    [JsonIgnore]
    public int MinArguments => Parameters.Count(parameter => !parameter.IsOptional);

    /// <summary>
    /// The most arguments it takes: one for each parameter; <see langword="null"/> when its last parameter repeats, and any number is legal.
    /// </summary>
    [JsonIgnore]
    public int? MaxArguments => Parameters is [.., { Repeats: true }] ? null : Parameters.Length;

    /// <summary>
    /// Whether its parameters are in the order the declaration requires: no required parameter after an optional one, and only the last one repeated.
    /// </summary>
    [JsonIgnore]
    public bool IsWellFormed
        => Parameters.Select((parameter, index) => (parameter, index)).All(item
            => (!item.parameter.Repeats || item.index == Parameters.Length - 1)
                && (item.parameter.IsOptional || Parameters.Take(item.index).All(before => !before.IsOptional)));

    /// <summary>
    /// Whether a name is the annotation's own or one of its aliases. Case does not matter, as in any VBA name (<strong>MS-VBAL §3.3.5.1</strong>).
    /// </summary>
    /// <param name="name">The name written after <c>'@</c>.</param>
    public bool IsCalled(string name)
        => string.Equals(Name, name, StringComparison.OrdinalIgnoreCase)
            || Aliases.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether it declares an annotation of the code, written under its name or an alias.
    /// </summary>
    /// <param name="annotation">The annotation, as the parser reads it.</param>
    public bool Declares(AnnotationTriviaNode annotation) => IsCalled(annotation.Name);
}
