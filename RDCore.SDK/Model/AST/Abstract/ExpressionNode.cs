using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Abstract;

/// <summary>
/// A <see cref="SyntaxNode"/> that can be statically evaluated to a <c>VBType</c>, and with runtime semantics to a <c>VBTypedValue</c>.
/// </summary>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="Location">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
public abstract record class ExpressionNode(string Token, SyntaxNodeId Identity, SourceLocation Location, ImmutableArray<ExpressionNode> Inputs) :
    SyntaxNode(Identity, Location, [.. Inputs.Cast<SyntaxNode>()]), 
    IExecutableNode
{
}
