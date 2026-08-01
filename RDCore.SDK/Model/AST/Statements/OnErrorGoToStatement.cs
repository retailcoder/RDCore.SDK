using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Model.AST.Statements;

/// <summary>
/// Represents a statement that defines a locally-scoped error handler label.
/// </summary>
/// <param name="SemanticId">A semantic <c>Uri</c> uniquely identifying this specific node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
/// <param name="LabelExpression">An expression that resolves to a <em>label</em> denoting the error-handling subroutine.</param>
public record class OnErrorGoToStatement(Uri SemanticId, SourceLocation SourceLocation, ExpressionNode LabelExpression)
    : StatementNode(SemanticId, SourceLocation, $"{Tokens.On}{Tokens.Error}", [LabelExpression]);
