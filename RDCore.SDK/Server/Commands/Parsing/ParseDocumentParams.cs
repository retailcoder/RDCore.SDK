using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Server.Commands.Parsing;

/// <summary>
/// The <em>parameter</em> object for a <c>ParseDocumentCommand</c>.
/// </summary>
public record class ParseDocumentParams
{
    /// <summary>
    /// The <c>Uri</c> of the document to parse.
    /// </summary>
    public Uri? DocumentUri { get; init; } = default;
    /// <summary>
    /// The fragment of source code to parse.
    /// </summary>
    /// <remarks>
    /// An <c>AnchorOffset</c> should also be specified.
    /// </remarks>
    public string? Fragment { get; init; } = default;
    /// <summary>
    /// The position of the fragment in the source document.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>This property is ignored if a <c>DocumentUri</c> is specified.</item>
    /// <item>Anchor offset is <c>L0C0</c> unless specified otherwise.</item>
    /// </list>
    /// </remarks>
    public SourcePosition AnchorOffset { get; init; } = SourcePosition.Zero;
}
