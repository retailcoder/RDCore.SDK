using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Model.AST.Statements;

/// <summary>
/// Represents a conditional executable statement.
/// </summary>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
/// <param name="Condition">A <em>Boolean expression</em> that determines whether execution branches into the <em>conditional statement</em> or not.</param>
/// <param name="ConditionalStatement">An <em>executable node</em> that is executed if the <em>Condition</em> expression evaluates to <c>True</c>.</param>
/// <param name="ElseStatement">An <em>executable node</em> that is executed if the <em>Condition</em> expression evaluates to <c>False</c>. <strong>Optional</strong></param>
public record class InlineIfStatement(SyntaxNodeId Identity, SourceLocation SourceLocation, ExpressionNode Condition, StatementNode ConditionalStatement, StatementNode? ElseStatement = default)
    : StatementNode(Identity, SourceLocation, [Condition]);
