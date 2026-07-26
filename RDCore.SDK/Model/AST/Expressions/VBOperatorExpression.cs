using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Semantics.Context.Abstract;
using System.Collections.Immutable;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// A <c>BoundExpression</c> that is associated to an <c>OperatorSymbol</c>.
/// </summary>
/// <remarks>
/// Unless specified otherwise in a derived node type, <strong>MS-VBAL 5.6.9 Operator Expressions</strong> defines the static and run-time semantics of this node.
/// </remarks>
/// <param name="SemanticId">A semantic <c>Uri</c> uniquely identifying this specific node.</param>
/// <param name="Symbol">The <see cref="OperatorSymbol{TContext, TFlags}"/> associated with this <em>operator expression</em>.</param>
/// <param name="ResultSymbol">A <see cref="BoundTypedSymbol"/> bound to the result of this expression.</param>
/// <param name="Location">The <c>Location</c> (holds the document <c>Uri</c> and a <c>Range</c>) of the bound expression.</param>
public abstract record class VBOperatorExpression<TContext, TFlags> : BoundExpression 
where TContext : SemanticContext<TFlags>, new()
where TFlags : struct, Enum
{
    protected VBOperatorExpression(Uri SemanticId,
    OperatorSymbol<TContext, TFlags> Symbol,
    OperatorExpressionValueSymbol ResultSymbol,
    SourceLocation Location,
    ImmutableArray<BoundExpression> Operands)
        : base(Symbol.Name, SemanticId, Location, Operands)
    { }

    protected VBOperatorExpression(string token, Uri semanticId, SourceLocation location, 
        ImmutableArray<BoundExpression> operands)
        : base(token, semanticId, location, operands) {  }
}