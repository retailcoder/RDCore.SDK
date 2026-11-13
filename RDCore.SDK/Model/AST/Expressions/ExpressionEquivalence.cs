using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Symbols.Operators;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// Whether two expressions of the syntax tree are the same expression, written in two places: the targets of the two assignments of
/// <c>If c Then flags(i) = True Else flags(i) = False</c>, or two call sites of one extracted method.
/// </summary>
/// <remarks>
/// <para>
/// Two expressions are the same when they are made of the same operations on the same operands, the way VBA reads them: names compare without regard to
/// case (<c>A.B(1)</c> and <c>a.b( 1 )</c>), and literals by their type and value (<c>&amp;HFF</c> and <c>255</c>). Where they are written, and the
/// blanks, comments and line continuations between their parts, are no part of what they are.
/// </para>
/// <para>
/// How a name is written is part of it: <c>s$</c> is not <c>s</c>, and <c>[A1]</c> is not <c>A1</c>. Every pair of parentheses is an operation of its own
/// (<strong>MS-VBAL §5.6.6</strong>), so <c>(x)</c> is not <c>x</c>: as an argument it passes <c>x</c> by value, and it reads an object's default member.
/// A member accessed in a <c>With</c> block (<c>.Enabled</c>) is not the member of a named object (<c>b.Enabled</c>), even of the object the block is
/// on. An expression of a kind it does not compare (<see cref="ExpressionWriter.Write"/> writes none of them either) is the same as no other.
/// </para>
/// </remarks>
public static class ExpressionEquivalence
{
    /// <summary>
    /// Whether two expressions are the same expression.
    /// </summary>
    /// <param name="left">An expression, or <see langword="null"/>.</param>
    /// <param name="right">Another expression, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both are the same expression, or both are <see langword="null"/>.</returns>
    public static bool AreEquivalent(ExpressionNode? left, ExpressionNode? right) => (left, right) switch
    {
        (null, null) => true,
        (null, _) or (_, null) => false,
        _ when left!.GetType() != right!.GetType() => false,
        (SimpleNameExpressionNode a, SimpleNameExpressionNode b) => SameName(a, b),
        (LiteralExpressionNode a, LiteralExpressionNode b) => SameValue(a, b),
        (InstanceExpressionNode, InstanceExpressionNode) => true,
        (MissingArgumentNode, MissingArgumentNode) => true,
        (PrecompilerNameExpressionNode a, PrecompilerNameExpressionNode b) => SameText(a.Name, b.Name),
        (BuiltInTypeExpressionNode a, BuiltInTypeExpressionNode b) => SameText(a.TypeName, b.TypeName) && a.IsBracketed == b.IsBracketed,
        (MemberAccessExpressionNode a, MemberAccessExpressionNode b) => AreEquivalent(a.Owner, b.Owner) && SameName(a.Member, b.Member),
        (DictionaryAccessExpressionNode a, DictionaryAccessExpressionNode b) => AreEquivalent(a.Owner, b.Owner) && SameName(a.Member, b.Member),
        (IndexExpressionNode a, IndexExpressionNode b) => AreEquivalent(a.Callee, b.Callee) && AllEquivalent(a.Arguments, b.Arguments),
        (NamedArgumentNode a, NamedArgumentNode b) => SameText(a.Name, b.Name) && AreEquivalent(a.Value, b.Value),
        (ByValArgumentExpressionNode a, ByValArgumentExpressionNode b) => AreEquivalent(a.Operand, b.Operand),
        (VBUnaryOperatorExpressionNode a, VBUnaryOperatorExpressionNode b) => SameOperator(a.Token, b.Token) && AreEquivalent(a.Operand, b.Operand),
        (VBBinaryOperatorExpressionNode a, VBBinaryOperatorExpressionNode b)
            => SameOperator(a.Token, b.Token) && AreEquivalent(a.Left, b.Left) && AreEquivalent(a.Right, b.Right),
        (NewExpressionNode a, NewExpressionNode b) => AreEquivalent(a.TypeExpression, b.TypeExpression),
        (AddressOfExpressionNode a, AddressOfExpressionNode b) => AreEquivalent(a.Target, b.Target),
        (TypeOfIsExpressionNode a, TypeOfIsExpressionNode b) => AreEquivalent(a.Operand, b.Operand) && AreEquivalent(a.TypeExpression, b.TypeExpression),
        (ArrayExpressionNode a, ArrayExpressionNode b) => AllEquivalent(a.Elements, b.Elements),
        (ArrayBoundExpressionNode a, ArrayBoundExpressionNode b) => a.Kind == b.Kind && AreEquivalent(a.Array, b.Array) && AreEquivalent(a.Dimension, b.Dimension),
        (ConditionalExpressionNode { Inputs: [ExpressionNode a] }, ConditionalExpressionNode { Inputs: [ExpressionNode b] }) => AreEquivalent(a, b),
        _ => false,
    };

    private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SameName(SimpleNameExpressionNode a, SimpleNameExpressionNode b)
        => SameText(a.IdentifierName, b.IdentifierName) && a.TypeHint == b.TypeHint && a.IsBracketed == b.IsBracketed;

    private static bool AllEquivalent(IReadOnlyList<ExpressionNode> a, IReadOnlyList<ExpressionNode> b)
        => a.Count == b.Count && a.Zip(b).All(pair => AreEquivalent(pair.First, pair.Second));

    // the two parentheses (the precedence and the let-coerce operations) are two tokens: the same text, `(x)`, where they are written.
    private static bool SameOperator(string a, string b)
        => SameText(a, b) || (Canonical(a) is { } x && x == Canonical(b) && !IsParentheses(a) && !IsParentheses(b));

    private static bool IsParentheses(string token) => token is OperatorSymbolNames.UnaryArithmeticPrecedenceOp or OperatorSymbolNames.UnaryLetCoerceOp;

    private static string? Canonical(string token) => token switch
    {
        OperatorSymbolNames.UnaryArithmeticNegationOp => Tokens.NegationOp,
        OperatorSymbolNames.UnaryBitwiseNotOp => Tokens.Not,
        _ => ExpressionWriter.BinaryOperatorText(token),
    };

    private static bool SameValue(LiteralExpressionNode a, LiteralExpressionNode b)
        => a.StaticValue.GetType() == b.StaticValue.GetType()
            && (ExpressionWriter.LiteralOf(a.StaticValue) is { } x && ExpressionWriter.LiteralOf(b.StaticValue) is { } y ? x == y : a.StaticValue.Equals(b.StaticValue));
}
