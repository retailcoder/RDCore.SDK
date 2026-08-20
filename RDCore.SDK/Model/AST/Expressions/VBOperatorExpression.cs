using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;
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
    protected VBOperatorExpression(SyntaxNodeId identity, SourceLocation location, ImmutableArray<SyntaxNode> operands)
        : base(identity, location, operands) {  }
}