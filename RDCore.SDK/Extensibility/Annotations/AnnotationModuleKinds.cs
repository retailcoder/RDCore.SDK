using System.Text.Json.Serialization;
using RDCore.SDK.Model.AST.Declarations;

namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// The kinds of module an annotation may be written in (<see cref="AnnotationDeclaration.ModuleKinds"/>).
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<AnnotationModuleKinds>))]
public enum AnnotationModuleKinds
{
    /// <summary>
    /// No kind of module.
    /// </summary>
    None = 0,

    /// <summary>
    /// A standard module (<see cref="ModuleType.StdModule"/>).
    /// </summary>
    Standard = 1,

    /// <summary>
    /// A class module that is not a form (<see cref="ModuleType.ClassModule"/>).
    /// </summary>
    Class = 2,

    /// <summary>
    /// A form: a class module with a designer.
    /// </summary>
    Form = 4,

    /// <summary>
    /// A host's document module (<see cref="ModuleType.DocumentModule"/>).
    /// </summary>
    Document = 8,

    /// <summary>
    /// Every kind of module.
    /// </summary>
    All = Standard | Class | Form | Document,
}
