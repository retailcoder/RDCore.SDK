using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Abstract;

/// <summary>
/// A node in the <em>abstract syntax tree</em> (AST).
/// </summary>
/// <remarks>
/// This is the base abstract node type every AST node is derived from.
/// </remarks>
/// <param name="SemanticId">A semantic <c>Uri</c> uniquely identifying this specific node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of this node.</param>
public abstract record class BoundNode(Uri SemanticId, SourceLocation SourceLocation, ImmutableArray<BoundNode> Children)
{
    public Uri SemanticId { get; init; } = SemanticId;
    public SourceLocation SourceLocation { get; init; } = SourceLocation;
    public ImmutableArray<BoundNode> Children { get; init; } = Children;
}