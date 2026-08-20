using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// A <em>prefix</em> <c>VBOperatorExpression</c> that accepts a single operand.
/// </summary>
/// <remarks>
/// Unless specified otherwise in a derived node type, <strong>MS-VBAL 5.6.9 Operator Expressions</strong> defines the static and run-time semantics of this node.
/// </remarks>
/// <param name="Token">The <c>OperatorSymbol</c> token associated with this <em>operator expression</em>.</param>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="Location">The <c>Location</c> (holds the document <c>Uri</c> and a <c>Range</c>) of the bound expression.</param>
public record class VBUnaryOperatorExpressionNode(string Token, SyntaxNodeId Identity, SourceLocation Location, ImmutableArray<SyntaxNode> Children)
    : VBOperatorExpression(Identity, Location, Children)
{ }
