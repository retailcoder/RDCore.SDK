namespace RDCore.SDK.Semantics.Flags;

/// <summary>
/// The semantic flags that can be attached to an <c>LSet</c> or <c>RSet</c> statement (<strong>MS-VBAL §5.4.3.6-7</strong>) operation.
/// </summary>
/// <remarks>
/// 🧩 The statement itself says nothing about a program that is wrong in a way it can still run: the facts are flagged here, at analysis time, and it is
/// for an analyzer (<c>RDCore.Diagnostics</c>, or any other) to decide whether one is worth a diagnostic.
/// </remarks>
[Flags]
public enum FixedAssignmentSemanticFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,
    /// <summary>
    /// This operation produces a run-time error.
    /// </summary>
    Failed = 1 << 0,
    /// <summary>
    /// The target is a <c>String</c>: the value is fitted into the width the target already has.
    /// </summary>
    StringTarget = 1 << 1,
    /// <summary>
    /// The target and the source are user-defined types: <c>LSet</c> copies one over the other as bytes.
    /// </summary>
    UserDefinedTypeCopy = 1 << 2,
    /// <summary>
    /// The <em>source</em> of a <see cref="UserDefinedTypeCopy"/> is a user-defined type that has a variable-length <c>String</c> member, at any depth.
    /// </summary>
    /// <remarks>
    /// <strong>MS-VBAL §5.4.3.6</strong> allows <c>LSet</c> between any two user-defined types, and RD-VBA runs it, carrying the member's value across
    /// where the other record has a variable-length <c>String</c> at the same offset. MS-VBA refuses the statement at compile time, with "Type mismatch",
    /// when either record has a variable-length <c>String</c> member (observed in Excel, for a member at the top level of the record): a program that
    /// relies on it does not compile in MS-VBA.
    /// </remarks>
    SourceHoldsVariableLengthString = 1 << 3,
    /// <summary>
    /// The <em>destination</em> of a <see cref="UserDefinedTypeCopy"/> is a user-defined type that has a variable-length <c>String</c> member, at any depth.
    /// </summary>
    /// <remarks>
    /// See <see cref="SourceHoldsVariableLengthString"/>.
    /// </remarks>
    DestinationHoldsVariableLengthString = 1 << 4,
    /// <summary>
    /// The <em>source</em> of a <see cref="UserDefinedTypeCopy"/> is a user-defined type that has a member of an object type (<c>Object</c>, a class or
    /// an interface), at the top level of the type.
    /// </summary>
    /// <remarks>
    /// <strong>MS-VBAL §5.4.3.6</strong> allows <c>LSet</c> between any two user-defined types, and RD-VBA runs it, copying the object, or its
    /// address, where the other record has a member at the same offset. MS-VBA refuses the statement at compile time, with "Type mismatch", when either
    /// record has a member of an object type at its top level (observed in Excel): a program that relies on it does not compile in MS-VBA. A member of a
    /// nested record, or an array of objects, is not looked at.
    /// </remarks>
    SourceHoldsObject = 1 << 5,
    /// <summary>
    /// The <em>destination</em> of a <see cref="UserDefinedTypeCopy"/> is a user-defined type that has a member of an object type (<c>Object</c>, a
    /// class or an interface), at the top level of the type.
    /// </summary>
    /// <remarks>
    /// See <see cref="SourceHoldsObject"/>.
    /// </remarks>
    DestinationHoldsObject = 1 << 6,
    /// <summary>
    /// The <em>source</em> of a <see cref="UserDefinedTypeCopy"/> is a user-defined type that has a <c>Variant</c> member, at the top level of the type.
    /// </summary>
    /// <remarks>
    /// <strong>MS-VBAL §5.4.3.6</strong> allows <c>LSet</c> between any two user-defined types, and RD-VBA runs it, carrying the member's value
    /// across where the other record has a <c>Variant</c> at the same offset. MS-VBA refuses the statement at compile time, with "Type mismatch", when
    /// either record has a <c>Variant</c> member at its top level (observed in Excel): a program that relies on it does not compile in MS-VBA. A member of
    /// a nested record, or an array of <c>Variant</c>, is not looked at.
    /// </remarks>
    SourceHoldsVariant = 1 << 7,
    /// <summary>
    /// The <em>destination</em> of a <see cref="UserDefinedTypeCopy"/> is a user-defined type that has a <c>Variant</c> member, at the top level of the
    /// type.
    /// </summary>
    /// <remarks>
    /// See <see cref="SourceHoldsVariant"/>.
    /// </remarks>
    DestinationHoldsVariant = 1 << 8,
}
