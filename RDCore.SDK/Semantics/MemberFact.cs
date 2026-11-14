namespace RDCore.SDK.Semantics;

/// <summary>
/// The accessors a property declares (<strong>MS-VBAL §5.3.1.7</strong>): the <c>Property Get</c>, <c>Property Let</c> and <c>Property Set</c> of one name.
/// </summary>
[Flags]
public enum PropertyAccessors
{
    /// <summary>
    /// No accessor: what is described is not a property.
    /// </summary>
    None = 0,

    /// <summary>
    /// A <c>Property Get</c>.
    /// </summary>
    Get = 1 << 0,

    /// <summary>
    /// A <c>Property Let</c>.
    /// </summary>
    Let = 1 << 1,

    /// <summary>
    /// A <c>Property Set</c>.
    /// </summary>
    Set = 1 << 2,
}

/// <summary>
/// The default member of a class, as an expression of the class's type has it: the member a value of the expression is read or assigned through
/// (<strong>MS-VBAL §5.6.2.3</strong>, <strong>§5.4.3.8</strong>).
/// </summary>
/// <param name="Name">The name of the member.</param>
/// <param name="Classification">
/// What the member is: a <see cref="ExpressionClassification.Function"/>, a <see cref="ExpressionClassification.Property"/>, or a
/// <see cref="ExpressionClassification.Subroutine"/>.
/// </param>
/// <param name="Accessors">The accessors of the member, for a property; <see cref="PropertyAccessors.None"/> for any other member.</param>
/// <param name="RequiredParameters">
/// How many parameters of the member are neither <c>Optional</c> nor a <c>ParamArray</c>: the fewest arguments a call of it can be given.
/// </param>
public sealed record class DefaultMemberFact(string Name, ExpressionClassification Classification, PropertyAccessors Accessors, int RequiredParameters);
