using System.Text.Json.Serialization;

namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// What an annotation annotates: where in a module it may be written (<see cref="AnnotationDeclaration.Targets"/>).
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<AnnotationTargets>))]
public enum AnnotationTargets
{
    /// <summary>
    /// Nothing: an annotation of no target is legal nowhere.
    /// </summary>
    None = 0,

    /// <summary>
    /// The module: written in its declarations section, or above a block's header (<c>'@ModuleDescription</c>, <c>'@IgnoreModule</c>).
    /// </summary>
    Module = 1,

    /// <summary>
    /// A member: written on the lines directly above a procedure's declaration (<c>'@Description</c>).
    /// </summary>
    Member = 2,

    /// <summary>
    /// A variable or a constant of the module: written on the lines directly above its declaration.
    /// </summary>
    Variable = 4,

    /// <summary>
    /// The line below it, whatever that line is (<c>'@Ignore</c>).
    /// </summary>
    Line = 8,
}
