using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Semantics.Context.Abstract;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// An <em>expression</em> syntax node representing an operator.
/// </summary>
/// <remarks>
/// Unless specified otherwise in a derived node type, <strong>MS-VBAL 5.6.9 Operator Expressions</strong> defines the static and run-time semantics of this node.
/// </remarks>
public abstract record class VBOperatorExpression : ExpressionNode 
{
    protected VBOperatorExpression(string token, SyntaxNodeId identity, SourceLocation location, ImmutableArray<ExpressionNode> operands)
        : base(token, identity, location, operands) {  }
}