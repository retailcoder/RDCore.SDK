using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Statements;

/// <summary>
/// An executable statement node that represents a procedure (or function) call.
/// </summary>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
/// <param name="Token">The <c>string</c> <em>token</em> of the statement, e.g. <c>Open</c>, <c>Input</c>, <c>Print</c>, <c>Assert</c>, etc..</param>
/// <param name="Inputs">The <em>inputs</em> of the executable statement; expressions evaluated immediately before the call.</param>
public record class CallStatementNode(SyntaxNodeId Identity, SourceLocation SourceLocation, string Token, ImmutableArray<ExpressionNode> Inputs)
    : StatementNode(Identity, SourceLocation, Inputs);
