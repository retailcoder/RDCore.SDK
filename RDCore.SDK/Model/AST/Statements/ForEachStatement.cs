using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Model.AST.Statements;

/// <summary>
/// Represents a <c>For Each...Next</c> loop construct.
/// </summary>
/// <param name="SemanticId">A semantic <c>Uri</c> uniquely identifying this specific node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
/// <param name="ControlExpression">An object or variant expression that resolves to the loop control variable.</param>
/// <param name="CollectionExpression">An object expression that resolves to an enumerable object (or array).</param>
/// <param name="Body">The executable statements in the body of the loop.</param>
public record class ForEachStatement(Uri SemanticId, SourceLocation SourceLocation, ExpressionNode ControlExpression, ExpressionNode CollectionExpression, StatementBlock Body)
    : StatementNode(SemanticId, SourceLocation, $"{Tokens.ForEach}-{Tokens.Next}", [ControlExpression, CollectionExpression]);

