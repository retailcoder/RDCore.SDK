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
public sealed record class DefaultMemberFact(string Name, ExpressionClassification Classification, PropertyAccessors Accessors, int RequiredParameters)
{
    /// <summary>
    /// The members the code can name in place of this one, when it is a hidden member of a library's class that the library states them for
    /// (<see cref="RDCore.SDK.Model.Symbols.Abstract.SymbolProperties.WrittenAs"/>): Excel's <c>Range._Default</c> is <c>Item</c> given arguments and
    /// <c>Value</c> given none. <see langword="null"/> for any other member, whose <see cref="Name"/> is the one to write.
    /// </summary>
    public DefaultMemberNames? WrittenAs { get; init; }
}

/// <summary>
/// The members of its class that a hidden default member of a library's class stands for (<strong>MS-VBAL §5.6.2.2</strong>,
/// <strong>§5.6.13</strong>): what the code names when it writes the access out in full. A type library does not state them; a library's file does
/// (<see cref="RDCore.SDK.Runtime.Libraries.MemberDescription.WrittenAs"/>).
/// </summary>
/// <param name="WithArguments">
/// The member to name when the access gives the default member arguments (<c>r(1)</c> is <c>r.Item(1)</c>, <c>r!A1</c> is <c>r.Item("A1")</c>);
/// <see langword="null"/> when the library states none.
/// </param>
/// <param name="WithoutArguments">
/// The member to name when the access gives it none (<c>x = r</c> is <c>x = r.Value</c>); <see langword="null"/> when the library states none.
/// </param>
public sealed record class DefaultMemberNames(string? WithArguments, string? WithoutArguments);
