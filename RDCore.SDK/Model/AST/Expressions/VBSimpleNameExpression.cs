using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// <strong>MS-VBAL 5.6.10 Simple Name Expression</strong><br/>
/// A <see cref="ExpressionNode"/> that statically resolves an expression consisting of a single identifier without any <em>qualifiers</em> or <em>arguments</em>.
/// </summary>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="Location">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
/// <param name="IdentifierName">The parsed <em>identifier name</em>.</param>
public sealed record class VBSimpleNameExpression(SyntaxNodeId Identity, SourceLocation Location, string IdentifierName)
    : ExpressionNode(IdentifierName, Identity, Location, []) { }