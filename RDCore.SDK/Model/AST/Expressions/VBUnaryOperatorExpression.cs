using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Semantics.Context.Abstract;

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
public record class VBUnaryOperatorExpression<TContext, TFlags>(Guid Identity, string Token, SourceLocation Location, VBOperatorExpression<TContext, TFlags> Expression) 
    : VBOperatorExpression<TContext, TFlags>(Token, Identity, Location, [Expression]) 
    where TContext : SemanticContext<TFlags>, new()
    where TFlags : struct, Enum
{ }
