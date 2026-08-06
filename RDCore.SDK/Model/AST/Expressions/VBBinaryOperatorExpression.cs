using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Semantics.Context.Abstract;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// An <em>infix</em> <c>VBOperatorExpression</c> (bound) that accepts a <em>left</em> and a <em>right</em> operand on either of its sides.
/// </summary>
/// <remarks>
/// Unless specified otherwise in a derived node type, <strong>MS-VBAL 5.6.9 Operator Expressions</strong> defines the static and run-time semantics of this node.
/// </remarks>
public record class VBBinaryOperatorExpression<TContext, TFlags>
    : VBOperatorExpression<TContext, TFlags>
    where TContext : SemanticContext<TFlags>, new()
    where TFlags : struct, Enum
{
    /// <param name="Identity">A unique identifier for this specific syntax node.</param>
    /// <param name="Symbol">The <c>OperatorSymbol</c> associated with this <em>operator expression</em>.</param>
    /// <param name="ResultSymbol">The <see cref="OperatorExpressionValueSymbol"/> associated with the <em>result</em> of this <em>operator expression</em>.</param>
    /// <param name="Location">The <c>Location</c> (holds the document <c>Uri</c> and a <c>Range</c>) of the bound expression.</param>
    /// <param name="Left">The left-hand side (LHS) operand of this <em>binary operator expression</em></param>
    /// <param name="Right">The right-hand side (RHS) operand of this <em>binary operator expression</em></param>
    public VBBinaryOperatorExpression(string token, Guid identity, SourceLocation location, ExpressionNode left, ExpressionNode right) 
        : base(token, identity, location, [left, right])
    {
        Left = left;
        Right = right;
    }

    /// <param name="Identity">A unique identifier for this specific syntax node.</param>
    /// <param name="Symbol">The <c>OperatorSymbol</c> associated with this <em>operator expression</em>.</param>
    /// <param name="ResultSymbol">The <see cref="OperatorExpressionValueSymbol"/> associated with the <em>result</em> of this <em>operator expression</em>.</param>
    /// <param name="Location">The <c>Location</c> (holds the document <c>Uri</c> and a <c>Range</c>) of the bound expression.</param>
    /// <param name="Left">The left-hand side (LHS) operand of this <em>binary operator expression</em></param>
    /// <param name="Right">The right-hand side (RHS) operand of this <em>binary operator expression</em></param>
    public VBBinaryOperatorExpression(Guid identity, OperatorSymbol<TContext, TFlags> symbol, OperatorExpressionValueSymbol resultSymbol, SourceLocation location, ExpressionNode left, ExpressionNode right) 
        : base(identity, symbol, resultSymbol, location, [left, right])
    {
        Left = left;
        Right = right;
    }

    public ExpressionNode Left { get; }
    public ExpressionNode Right { get; }
}
