using System.Text.Json.Serialization;

namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// What an annotation's parameter takes (<see cref="AnnotationParameter.Takes"/>), which says how its argument is read and what completion offers for it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnnotationArgumentKind>))]
public enum AnnotationArgumentKind
{
    /// <summary>
    /// A string literal: <c>'@Description("Adds two numbers.")</c>.
    /// </summary>
    Text,

    /// <summary>
    /// A numeric literal.
    /// </summary>
    Number,

    /// <summary>
    /// A name, written without quotes.
    /// </summary>
    Identifier,

    /// <summary>
    /// The code of a diagnostic, written without quotes: <c>'@Ignore RDC01001</c>.
    /// </summary>
    DiagnosticCode,

    /// <summary>
    /// Any literal (<strong>MS-VBAL §3.3</strong>).
    /// </summary>
    Literal,

    /// <summary>
    /// A run-time error: the diagnostic code of a run-time error, written without quotes, whose digits are the error's number (<c>'@ExpectedError VBR00013</c>
    /// is error 13), or a number or a constant expression (<strong>MS-VBAL §5.6.16.1</strong>) for an error the project raises itself
    /// (<c>'@ExpectedError 13</c>, <c>'@ExpectedError vbObjectError + 513</c>).
    /// </summary>
    ErrorNumber,
}
