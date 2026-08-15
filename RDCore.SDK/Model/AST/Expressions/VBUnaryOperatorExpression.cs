using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// A <em>prefix</em> <c>VBOperatorExpression</c> that accepts a single operand.
/// </summary>
/// <remarks>
/// Unless specified otherwise in a derived node type, <strong>MS-VBAL 5.6.9 Operator Expressions</strong> defines the static and run-time semantics of this node.
/// </remarks>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="Token">The <c>OperatorSymbol</c> token associated with this <em>operator expression</em>.</param>
/// <param name="Location">The <c>Location</c> (holds the document <c>Uri</c> and a <c>Range</c>) of the bound expression.</param>
/// <param name="Expression">The operand of this <em>unary operator expression</em></param>
public record class VBUnaryOperatorExpression(SyntaxNodeId Identity, string Token, SourceLocation Location, VBOperatorExpression Expression) 
    : VBOperatorExpression(Token, Identity, Location, [Expression]) 
{ }
