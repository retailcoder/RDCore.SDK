using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Model.AST.Statements;

/// <summary>
/// Represents a <c>Do... Loop Until</c> construct.
/// </summary>
/// <param name="SemanticId">A semantic <c>Uri</c> uniquely identifying this specific node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
/// <param name="ConditionExpression">An object or variant expression that controls loop entry and continuation.</param>
/// <param name="Body">The executable statements in the body of the loop.</param>
/// <remarks>
/// This loop construct exits when the <c>ConditionExpression</c> evaluates to <c>True</c>.
/// </remarks>
public record DoLoopUntilStatement(Uri SemanticId, SourceLocation SourceLocation, ExpressionNode ConditionExpression, StatementBlock Body)
    : StatementNode(SemanticId, SourceLocation, $"{Tokens.Loop}-{Tokens.Until}", [ConditionExpression]);

