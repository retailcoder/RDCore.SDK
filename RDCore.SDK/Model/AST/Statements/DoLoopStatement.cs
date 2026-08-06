using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Model.AST.Statements;

/// <summary>
/// Represents a <c>Do...Loop</c> construct.
/// </summary>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
/// <param name="Body">The executable statements in the body of the loop.</param>
/// <remarks>
/// If the <c>Body</c> contains no <c>Exit</c> statement (conditional or not), the loop is deterministically infinite.
/// </remarks>
public record DoLoopStatement(Guid Identity, SourceLocation SourceLocation, StatementBlock Body)
    : StatementNode(Identity, SourceLocation, $"{Tokens.Do}-{Tokens.Loop}", []);

