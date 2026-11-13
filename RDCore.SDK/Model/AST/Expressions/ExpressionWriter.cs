using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Symbols.Operators;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;
using System.Collections.Immutable;
using System.Globalization;

namespace RDCore.SDK.Model.AST.Expressions;

/// <summary>
/// Writes an expression of the syntax tree back as the VBA code that says it: what a fix or a refactoring writes when it moves an expression, or builds
/// one out of the parts of others (<c>t = c</c> out of <c>If c Then t = True Else t = False</c>).
/// </summary>
/// <remarks>
/// <para>
/// What the tree keeps of how an expression is written is written as it was: a name's type-declaration character and its square brackets
/// (<c>Left$</c>, <c>x.[My Field]</c>), a literal's spelling (<see cref="LiteralExpressionNode.Spelling"/>), and every pair of parentheses. A pair of
/// parentheses is an operation of its own (<strong>MS-VBAL §5.6.6</strong>): the writer never adds or drops one for precedence's sake, so what it writes
/// parses back to the tree it was written from (<see cref="ExpressionEquivalence"/>).
/// </para>
/// <para>
/// What the tree does not keep is written the one way the writer has: one space around a binary operator, none inside parentheses, <c>", "</c> between
/// arguments, and the keywords as VBA spells them.
/// </para>
/// </remarks>
public static class ExpressionWriter
{
    // the keyword of an ArrayExpressionNode, Array(...).
    private const string ArrayKeyword = "Array";

    // the text of an operator, by the token the parser gives its node and by its operator symbol name.
    private static readonly ImmutableDictionary<string, string> BinaryOperators = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["+"] = "+", [OperatorSymbolNames.BinaryArithmeticAdditionOp] = "+",
        ["-"] = "-", [OperatorSymbolNames.BinaryArithmeticSubtractionOp] = "-",
        ["*"] = "*", [OperatorSymbolNames.BinaryArithmeticMultiplicationOp] = "*",
        ["/"] = "/", [OperatorSymbolNames.BinaryArithmeticDivisionOp] = "/",
        ["\\"] = "\\", [OperatorSymbolNames.BinaryArithmeticIntegerDivisionOp] = "\\",
        ["^"] = "^", [OperatorSymbolNames.BinaryArithmeticExponentOp] = "^",
        [Tokens.Mod] = Tokens.Mod, [OperatorSymbolNames.BinaryArithmeticModuloOp] = Tokens.Mod,
        ["&"] = "&", [OperatorSymbolNames.BinaryStringConcatOp] = "&",
        ["="] = "=", [OperatorSymbolNames.BinaryCompareEqOp] = "=",
        ["<>"] = "<>", [OperatorSymbolNames.BinaryCompareNeqOp] = "<>",
        ["<"] = "<", [OperatorSymbolNames.BinaryCompareLtOp] = "<",
        ["<="] = "<=", [OperatorSymbolNames.BinaryCompareLtEqOp] = "<=",
        [">"] = ">", [OperatorSymbolNames.BinaryCompareGtOp] = ">",
        [">="] = ">=", [OperatorSymbolNames.BinaryCompareGtEqOp] = ">=",
        [Tokens.Like] = Tokens.Like, [OperatorSymbolNames.BinaryCompareLikeOp] = Tokens.Like,
        [Tokens.Is] = Tokens.Is, [OperatorSymbolNames.BinaryCompareIsOp] = Tokens.Is,
        [Tokens.And] = Tokens.And, [OperatorSymbolNames.BinaryBitwiseAndOp] = Tokens.And,
        [Tokens.Or] = Tokens.Or, [OperatorSymbolNames.BinaryBitwiseOrOp] = Tokens.Or,
        [Tokens.XOr] = Tokens.XOr, [OperatorSymbolNames.BinaryBitwiseXOrOp] = Tokens.XOr,
        [Tokens.Eqv] = Tokens.Eqv, [OperatorSymbolNames.BinaryBitwiseEqvOp] = Tokens.Eqv,
        [Tokens.Imp] = Tokens.Imp, [OperatorSymbolNames.BinaryBitwiseImpOp] = Tokens.Imp,
    }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The VBA code that says an expression.
    /// </summary>
    /// <param name="expression">The expression, a node of a syntax tree or one a fix or a refactoring builds.</param>
    /// <returns>
    /// The code, or <see langword="null"/> when the expression, or any part of it, is one the writer does not write: an expression the parse could not read
    /// (<see cref="UnbuiltExpressionTriviaNode"/>), a part of a declaration or of a <c>Print</c> statement rather than an expression, or a literal of a value
    /// no literal writes (a <c>Byte</c>, a <c>Decimal</c>, a string that holds a line break). It is never a guess.
    /// </returns>
    public static string? Write(ExpressionNode expression) => expression switch
    {
        SimpleNameExpressionNode name => NameOf(name),
        LiteralExpressionNode literal => literal.Spelling ?? LiteralOf(literal.StaticValue),
        InstanceExpressionNode => Tokens.Me,
        PrecompilerNameExpressionNode name => name.Name,
        BuiltInTypeExpressionNode type => type.IsBracketed ? $"[{type.TypeName}]" : type.TypeName,
        MemberAccessExpressionNode access => Access(access.Owner, ".", access.Member),
        DictionaryAccessExpressionNode access => Access(access.Owner, "!", access.Member),
        IndexExpressionNode index => Write(index.Callee) is { } callee && List(index.Arguments) is { } arguments ? $"{callee}({arguments})" : null,
        MissingArgumentNode => string.Empty,
        NamedArgumentNode named => Write(named.Value) is { } value ? $"{named.Name}:={value}" : null,
        ByValArgumentExpressionNode byVal => Prefixed($"{Tokens.ByVal} ", byVal.Operand),
        VBUnaryOperatorExpressionNode unary => Unary(unary),
        VBBinaryOperatorExpressionNode binary => BinaryOperators.TryGetValue(binary.Token, out var op)
            && Write(binary.Left) is { } left && Write(binary.Right) is { } right ? $"{left} {op} {right}" : null,
        NewExpressionNode @new => Prefixed($"{Tokens.New} ", @new.TypeExpression),
        AddressOfExpressionNode addressOf => Prefixed($"{Tokens.AddressOf} ", addressOf.Target),
        TypeOfIsExpressionNode typeOf => Write(typeOf.Operand) is { } operand && Write(typeOf.TypeExpression) is { } type
            ? $"{Tokens.TypeOf} {operand} {Tokens.Is} {type}" : null,
        ArrayExpressionNode array => List(array.Elements) is { } elements ? $"{ArrayKeyword}({elements})" : null,
        ArrayBoundExpressionNode bound => Write(bound.Array) is { } array
            ? $"{(bound.Kind == ArrayBoundKind.Lower ? Tokens.LBound : Tokens.UBound)}({array}{(bound.Dimension is null ? string.Empty : Write(bound.Dimension) is { } dimension ? $", {dimension}" : null)})"
            : null,
        ConditionalExpressionNode { Inputs: [ExpressionNode condition] } => Write(condition),
        _ => null,
    };

    // the text of a binary operator, by the token of its node; null for a token that is none.
    internal static string? BinaryOperatorText(string token) => BinaryOperators.TryGetValue(token, out var text) ? text : null;

    private static string NameOf(SimpleNameExpressionNode name)
        => (name.IsBracketed ? $"[{name.IdentifierName}]" : name.IdentifierName) + name.TypeHint;

    private static string? Access(ExpressionNode? owner, string separator, SimpleNameExpressionNode member)
        => owner is null ? separator + NameOf(member)
            : Write(owner) is { } written ? written + separator + NameOf(member)
            : null;

    private static string? Prefixed(string prefix, ExpressionNode operand) => Write(operand) is { } written ? prefix + written : null;

    private static string? List(IEnumerable<ExpressionNode> items)
    {
        var written = items.Select(Write).ToArray();
        return written.Any(item => item is null) ? null : string.Join(", ", written);
    }

    private static string? Unary(VBUnaryOperatorExpressionNode unary) => unary.Token switch
    {
        OperatorSymbolNames.UnaryArithmeticPrecedenceOp or OperatorSymbolNames.UnaryLetCoerceOp => Write(unary.Operand) is { } operand ? $"({operand})" : null,
        Tokens.NegationOp or OperatorSymbolNames.UnaryArithmeticNegationOp => Prefixed("-", unary.Operand),
        _ when string.Equals(unary.Token, Tokens.Not, StringComparison.OrdinalIgnoreCase) || unary.Token == OperatorSymbolNames.UnaryBitwiseNotOp
            => Prefixed($"{Tokens.Not} ", unary.Operand),
        _ => null,
    };

    /// <summary>
    /// The literal that writes a value the one way the writer has (<strong>MS-VBAL §3.3</strong>): with the type-declaration character that keeps its
    /// type, so that it reads back as the value it is.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The literal, or <see langword="null"/> when no literal writes the value.</returns>
    internal static string? LiteralOf(VBTypedValue value) => value switch
    {
        VBBooleanValue boolean => boolean.Value.Equals(VBBooleanValue.True.Value) ? Tokens.True : Tokens.False,
        VBIntegerValue integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        VBLongValue @long => @long.Value.ToString(CultureInfo.InvariantCulture) + (@long.Value is >= short.MinValue and <= short.MaxValue ? "&" : string.Empty),
        VBLongLongValue longLong => longLong.Value.ToString(CultureInfo.InvariantCulture) + "^",
        VBSingleValue single => single.Value.ToString("R", CultureInfo.InvariantCulture) + "!",
        VBDoubleValue @double => @double.Value.ToString("R", CultureInfo.InvariantCulture) + "#",
        VBCurrencyValue currency => currency.Value.Value.ToString(CultureInfo.InvariantCulture) + "@",
        VBStringValue text when !text.Value.Contains('\r') && !text.Value.Contains('\n') => $"\"{text.Value.Replace("\"", "\"\"")}\"",
        VBDateValue date => $"#{DateOf(date.Value)}#",
        VBEmptyValue => Tokens.Empty,
        VBNullValue => Tokens.Null,
        VBObjectValue @object when @object.IsNothing() => Tokens.Nothing,
        _ => null,
    };

    // a date literal of the date alone, of the time alone (on the date of a time, 30 December 1899), or of both.
    private static string DateOf(DateTime value)
    {
        var timeOfDay = value.TimeOfDay == TimeSpan.Zero;
        var isTimeOnly = value.Date == new DateTime(1899, 12, 30);
        return isTimeOnly ? value.ToString("h:mm:ss tt", CultureInfo.InvariantCulture)
            : timeOfDay ? value.ToString("M/d/yyyy", CultureInfo.InvariantCulture)
            : value.ToString("M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
    }
}
