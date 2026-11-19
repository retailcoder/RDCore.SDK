using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Platform.Protocol;

/// <summary>
/// The parameters of <c>rdcore/semantics/describe</c>: a position of a document, which the language server describes the name or expression written at.
/// </summary>
/// <remarks>
/// The position counts as LSP counts it: 0-based lines, and 0-based columns in UTF-16 code units (<strong>LSP 3.17</strong> §Position).
/// </remarks>
public record class SemanticsDescribeParams
{
    /// <summary>
    /// The address of the document.
    /// </summary>
    public Uri? Uri { get; init; }

    /// <summary>
    /// The position in the document, 0-based.
    /// </summary>
    public SourcePosition Position { get; init; }
}

/// <summary>
/// The parameters of <c>rdcore/semantics/outline</c>: a document, whose module the language server outlines.
/// </summary>
public record class SemanticsOutlineParams
{
    /// <summary>
    /// The address of the document.
    /// </summary>
    public Uri? Uri { get; init; }
}
