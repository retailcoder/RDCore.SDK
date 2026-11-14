using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Errors.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Runtime;
using RDCore.SDK.Semantics.Flags;
using RDCore.SDK.Semantics.Runtime.Operators;

namespace RDCore.SDK.Semantics.Facts;

/// <summary>
/// An operation of an <em>operator</em> (<strong>MS-VBAL 5.6.9</strong>), as the language core states it.
/// </summary>
/// <remarks>
/// 👉 One family of operators is one derived record, so that a new family does not grow the facts of every operation by a property.
/// The conversions of the operands of the operation are facts of their own (<see cref="ConversionFact"/>), at
/// <see cref="ConversionSite.OperatorOperand"/>. A fact is only stated when it is true of the code: see <see cref="ConversionFact"/>.
/// </remarks>
/// <param name="Node">The <c>Identity</c> of the syntax node the operation is evaluated for.</param>
/// <param name="Location">Where in the document the operation is: the whole expression, operands included, where a run-time error of the operation is located.</param>
/// <param name="Operator">The operator: one of the <c>Tokens</c> operators.</param>
/// <param name="EffectiveType">The <em>effective type</em> of the operation, if it has one.</param>
/// <param name="IsValueKnown">Whether every operand of the operation is known, as opposed to a value its type merely allows.</param>
/// <param name="Error">The run-time error the operation raises. Only stated when the operands are known, or when their types alone guarantee it.</param>
public abstract record class OperatorFact(
    SyntaxNodeId Node,
    SourceLocation Location,
    string Operator,
    VBType? EffectiveType,
    bool IsValueKnown,
    VBErrorInfo? Error)
{
    /// <summary>
    /// Where in the document the operator token is: the <c>+</c> of <c>a + b</c>. A diagnostic about the operator is located here, so that each operator
    /// of a chain (<c>a + b + c</c>, whose two operations begin at the same position) has a location of its own.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> when the syntax node has no operator token of its own (<see cref="VBBinaryOperatorExpressionNode.OperatorLocation"/>,
    /// <see cref="VBUnaryOperatorExpressionNode.OperatorLocation"/>): parentheses, a member access, an operation a statement makes of its own operands.
    /// </remarks>
    public SourceLocation? OperatorLocation { get; init; }
}

/// <summary>
/// An operation of an arithmetic operator (<strong>MS-VBAL 5.6.9.3</strong>).
/// </summary>
/// <param name="Node">The <c>Identity</c> of the syntax node the operation is evaluated for.</param>
/// <param name="Location">Where in the document the operation is: the whole expression, operands included, where a run-time error of the operation is located.</param>
/// <param name="Operator">The operator: one of the <c>Tokens</c> operators.</param>
/// <param name="EffectiveType">The <em>effective type</em> of the operation, if it has one.</param>
/// <param name="IsValueKnown">Whether every operand of the operation is known, as opposed to a value its type merely allows.</param>
/// <param name="Error">The run-time error the operation raises. Only stated when the operands are known, or when their types alone guarantee it.</param>
/// <param name="Flags">What is known of the operation.</param>
public sealed record class ArithmeticOperatorFact(
    SyntaxNodeId Node,
    SourceLocation Location,
    string Operator,
    VBType? EffectiveType,
    bool IsValueKnown,
    VBErrorInfo? Error,
    ArithmeticOperatorSemanticFlags Flags)
    : OperatorFact(Node, Location, Operator, EffectiveType, IsValueKnown, Error);

/// <summary>
/// An operation of a relational operator (<strong>MS-VBAL 5.6.9.5</strong>).
/// </summary>
/// <param name="Node">The <c>Identity</c> of the syntax node the operation is evaluated for.</param>
/// <param name="Location">Where in the document the operation is: the whole expression, operands included, where a run-time error of the operation is located.</param>
/// <param name="Operator">The operator: one of the <c>Tokens</c> operators.</param>
/// <param name="EffectiveType">The <em>effective type</em> of the operation, if it has one.</param>
/// <param name="IsValueKnown">Whether every operand of the operation is known, as opposed to a value its type merely allows.</param>
/// <param name="Error">The run-time error the operation raises. Only stated when the operands are known, or when their types alone guarantee it.</param>
/// <param name="Flags">What is known of the operation.</param>
/// <param name="Comparison">How the operation compares <c>String</c> values where it is evaluated.</param>
public sealed record class ComparisonOperatorFact(
    SyntaxNodeId Node,
    SourceLocation Location,
    string Operator,
    VBType? EffectiveType,
    bool IsValueKnown,
    VBErrorInfo? Error,
    ComparisonOperatorSemanticFlags Flags,
    StringComparisonRules Comparison)
    : OperatorFact(Node, Location, Operator, EffectiveType, IsValueKnown, Error)
{
    /// <summary>
    /// The <c>String</c> operand of a comparison of a <c>String</c> with a number, Let-coerced both ways (<see cref="StringOperandFact"/>);
    /// <see langword="null"/> for any other comparison.
    /// </summary>
    public StringOperandFact? StringOperand { get; init; }
}

/// <summary>
/// The <c>String</c> operand of a relational operation that compares a <c>String</c>, or a <c>Variant</c> holding one, with a number
/// (<strong>MS-VBAL 5.6.9.5</strong>): its value Let-coerced (<strong>MS-VBAL 5.5.1.2</strong>) to a <c>Double</c>, and to the effective type that the
/// table of MS-VBAL 5.6.9.5 gives the two operands.
/// </summary>
/// <remarks>
/// Each Let-coercion has a value or raises a run-time error. When the <c>String</c> is not known, neither is stated: the value and the error are both
/// <see langword="null"/>.
/// </remarks>
/// <param name="IsLeft">Whether the <c>String</c> is the left operand of the operation.</param>
/// <param name="SpecifiedEffectiveType">The name of the effective type that the table of MS-VBAL 5.6.9.5 gives the two operands.</param>
/// <param name="AsDouble">
/// The <c>String</c> Let-coerced to a <c>Double</c>; <see langword="null"/> when the <c>String</c> is not known, or the Let-coercion raises an error.
/// </param>
/// <param name="AsDoubleError">The number of the run-time error that the Let-coercion to a <c>Double</c> raises; <see langword="null"/> when it raises none.</param>
/// <param name="AsSpecified">
/// The <c>String</c> Let-coerced to <paramref name="SpecifiedEffectiveType"/>; <see langword="null"/> when the <c>String</c> is not known, or the
/// Let-coercion raises an error.
/// </param>
/// <param name="AsSpecifiedError">
/// The number of the run-time error that the Let-coercion to <paramref name="SpecifiedEffectiveType"/> raises; <see langword="null"/> when it raises none.
/// </param>
/// <param name="Number">The value of the other operand, the number, as it is held; <see langword="null"/> when it is not known.</param>
public sealed record class StringOperandFact(
    bool IsLeft,
    string SpecifiedEffectiveType,
    VBTypedValue? AsDouble,
    int? AsDoubleError,
    VBTypedValue? AsSpecified,
    int? AsSpecifiedError,
    VBTypedValue? Number);

/// <summary>
/// An operation of a logical operator (<strong>MS-VBAL 5.6.9.8</strong>).
/// </summary>
/// <param name="Node">The <c>Identity</c> of the syntax node the operation is evaluated for.</param>
/// <param name="Location">Where in the document the operation is: the whole expression, operands included, where a run-time error of the operation is located.</param>
/// <param name="Operator">The operator: one of the <c>Tokens</c> operators.</param>
/// <param name="EffectiveType">The <em>effective type</em> of the operation, if it has one.</param>
/// <param name="IsValueKnown">Whether every operand of the operation is known, as opposed to a value its type merely allows.</param>
/// <param name="Error">The run-time error the operation raises. Only stated when the operands are known, or when their types alone guarantee it.</param>
/// <param name="Flags">What is known of the operation.</param>
public sealed record class LogicalOperatorFact(
    SyntaxNodeId Node,
    SourceLocation Location,
    string Operator,
    VBType? EffectiveType,
    bool IsValueKnown,
    VBErrorInfo? Error,
    LogicalOperatorSemanticFlags Flags)
    : OperatorFact(Node, Location, Operator, EffectiveType, IsValueKnown, Error);

/// <summary>
/// An operation of the concatenation operator (<strong>MS-VBAL 5.6.9.4</strong>).
/// </summary>
/// <param name="Node">The <c>Identity</c> of the syntax node the operation is evaluated for.</param>
/// <param name="Location">Where in the document the operation is: the whole expression, operands included, where a run-time error of the operation is located.</param>
/// <param name="Operator">The operator: one of the <c>Tokens</c> operators.</param>
/// <param name="EffectiveType">The <em>effective type</em> of the operation, if it has one.</param>
/// <param name="IsValueKnown">Whether every operand of the operation is known, as opposed to a value its type merely allows.</param>
/// <param name="Error">The run-time error the operation raises. Only stated when the operands are known, or when their types alone guarantee it.</param>
/// <param name="Flags">What is known of the operation.</param>
public sealed record class ConcatOperatorFact(
    SyntaxNodeId Node,
    SourceLocation Location,
    string Operator,
    VBType? EffectiveType,
    bool IsValueKnown,
    VBErrorInfo? Error,
    ConcatOperationSemanticFlags Flags)
    : OperatorFact(Node, Location, Operator, EffectiveType, IsValueKnown, Error);

/// <summary>
/// A <em>member access</em> (<strong>MS-VBAL 5.6.12</strong>), a <em>dictionary access</em> (<strong>MS-VBAL 5.6.14</strong>), or the access of a member that the
/// code does not name: the default member of an object that is indexed (<strong>MS-VBAL 5.6.13</strong>), the enumeration member of the object a
/// <c>For Each</c> goes through (<strong>MS-VBAL 5.4.2.4</strong>).
/// </summary>
/// <remarks>
/// 👉 <see cref="OperatorFact.Operator"/> is <c>Tokens.MemberAccess</c> for a member access and a member the code does not name, and
/// <c>Tokens.DictionaryAccess</c> for a dictionary access. <see cref="OperatorFact.EffectiveType"/> is the declared type of the member, when it is known.
/// <see cref="OperatorFact.IsValueKnown"/> says whether the object is.
/// </remarks>
/// <param name="Node">The <c>Identity</c> of the syntax node the access is evaluated for.</param>
/// <param name="Location">Where in the document the access is.</param>
/// <param name="Operator"><c>Tokens.MemberAccess</c> or <c>Tokens.DictionaryAccess</c>.</param>
/// <param name="EffectiveType">The declared type of the member, when it is known.</param>
/// <param name="IsValueKnown">Whether the object is known.</param>
/// <param name="Error">The run-time error the access raises, when it is known to raise one.</param>
/// <param name="Member">The name of the member that is accessed, as the code writes it or, when the code does not name it, as the class declares it;
/// <see langword="null"/> when there is no telling which member it is (the object is not known, or is late-bound).</param>
/// <param name="Flags">What is known of the access.</param>
public sealed record class MemberAccessFact(
    SyntaxNodeId Node,
    SourceLocation Location,
    string Operator,
    VBType? EffectiveType,
    bool IsValueKnown,
    VBErrorInfo? Error,
    string? Member,
    MemberAccessOperationSemanticFlags Flags)
    : OperatorFact(Node, Location, Operator, EffectiveType, IsValueKnown, Error);
