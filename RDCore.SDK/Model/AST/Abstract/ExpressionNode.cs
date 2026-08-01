using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Abstract;

/// <summary>
/// A <see cref="SyntaxNode"/> that can be statically evaluated to a <c>VBType</c>, and with runtime semantics to a <c>VBTypedValue</c>.
/// </summary>
/// <param name="SemanticId">A semantic <c>Uri</c> uniquely identifying this specific node.</param>
/// <param name="Location">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
public abstract record class ExpressionNode(string Token, Uri SemanticId, SourceLocation Location, ImmutableArray<ExpressionNode> Inputs) :
    SyntaxNode(SemanticId, Location, [.. Inputs.Cast<SyntaxNode>()]), 
    IExecutableNode
{
}
