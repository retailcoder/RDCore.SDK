using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Model.AST.Statements;

/// <summary>
/// Represents a <c>Do Until...Loop</c> construct.
/// </summary>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
/// <param name="ConditionExpression">An object or variant expression that controls loop entry and continuation.</param>
/// <param name="Body">The executable statements in the body of the loop.</param>
/// <remarks>
/// This loop construct exits (and may not even enter) when the <c>ConditionExpression</c> evaluates to <c>True</c>.
/// </remarks>
public record DoUntilLoopStatement(Guid Identity, SourceLocation SourceLocation, ExpressionNode ConditionExpression, StatementBlock Body)
    : StatementNode(Identity, SourceLocation, $"{Tokens.Until}-{Tokens.Loop}", [ConditionExpression]);

