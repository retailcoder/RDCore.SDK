using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Abstract;

/// <summary>
/// A node in the <em>abstract syntax tree</em> (AST).
/// </summary>
/// <remarks>
/// This is the base abstract node type every AST node is derived from.
/// </remarks>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of this node.</param>
public abstract record class SyntaxNode(Guid Identity, SourceLocation SourceLocation, ImmutableArray<SyntaxNode> Children)
{
    public Guid Identity { get; init; } = Identity;
    public SourceLocation SourceLocation { get; init; } = SourceLocation;
    public ImmutableArray<SyntaxNode> Children { get; init; } = Children;
}