using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// An expression that statically resolves a <c>VBTypedValue</c> directly from the source tokens.
/// </summary>
/// <remarks>
/// Unless specified otherwise in a derived node type, <strong>MS-VBAL 5.6.5 Literal Expressions</strong> defines the static and run-time 
/// semantics of this node. <em>MS-VBAL 3.3 Lexical Tokens</em> static semantics being implemented at the parser level,
/// the <c>VBTypedValue</c> has already resolved its <c>type-suffix</c> ("type hint").
/// </remarks>
/// <param name="Identity">The unique identifier of this specific expression node.</param>
/// <param name="Location">The document location (<c>Uri</c>+<c>Range</c>) of the expression.</param>
/// <param name="StaticValue">The parsed literal value.</param>
public sealed record class LiteralExpressionNode(SyntaxNodeId Identity, SourceLocation Location, VBTypedValue StaticValue)
    : ExpressionNode(Identity, Location, [])
{
    /// <summary>
    /// The literal as it is written: <c>&amp;HFF</c>, <c>1#</c>, <c>"say ""hi"""</c>; <see langword="null"/> for a literal the parser did not read
    /// from a token of its own, and for a literal a fix or a refactoring makes.
    /// </summary>
    /// <remarks>
    /// The value says what the literal means, and the spelling how it is written: <c>&amp;HFF</c> and <c>255</c> are one value, written two ways.
    /// <see cref="ExpressionWriter"/> writes the spelling where there is one.
    /// </remarks>
    public string? Spelling { get; init; }
}

/// <summary>
/// An expression that is evaluated in a Boolean context.
/// </summary>
/// <param name="Identity">The unique identifier of this specific expression node.</param>
/// <param name="Location">The document location (<c>Uri</c>+<c>Range</c>) of the expression.</param>
/// <param name="Inputs">The child binary expression tree.</param>
public sealed record class ConditionalExpressionNode(SyntaxNodeId Identity, SourceLocation Location, ImmutableArray<SyntaxNode> Inputs)
    : ExpressionNode(Identity, Location, Inputs);
