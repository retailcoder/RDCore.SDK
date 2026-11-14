namespace RDCore.SDK.Semantics.Flags;

/// <summary>
/// The semantic flags of a <em>member access</em> operation.
/// </summary>
[Flags]
public enum MemberAccessOperationSemanticFlags
{
    /// <summary>
    /// The member access operation refers to the member explicitly.
    /// </summary>
    Explicit = 1 << 0,
    /// <summary>
    /// The member access operation refers to the member implicitly.
    /// </summary>
    Implicit = 1 << 1,
    /// <summary>
    /// The member access operation references the parent object through a <c>With</c> <em>block variable</em>.
    /// </summary>
    WithBlockVariable = 1 << 2,
    /// <summary>
    /// The member access operation is a <em>dictionary access</em> operation.
    /// </summary>
    /// <remarks>
    /// 👉 This operation involves an <strong>implicit</strong> member call to the <strong>default member</strong> of the parent object; the RHS operand <em>is given as a string argument</em> to that member.
    /// </remarks>
    DictionaryAccess = 1 << 3,
    /// <summary>
    /// The member access operation is semantically <strong>implicit</strong> and <strong>late-bound</strong> through a <c>CallByName</c> call.
    /// </summary>
    CallByNameAccess = 1 << 4,
    /// <summary>
    /// The member access operation refers to a member that <em>static semantics</em> leave unresolved; resolution/binding occurs at run-time, hence "late".
    /// </summary>
    LateBound = 1 << 5,
    /// <summary>
    /// The member access operation refers to the <em>default member</em> of its parent object.
    /// </summary>
    DefaultMember = 1 << 6,
    /// <summary>
    /// The member access operation refers to the <c>NewEnum</c> member of its parent object.
    /// </summary>
    /// <remarks>
    /// If this operation is also <c>Implicit</c>, the parent node is probably a <c>For...Next</c> loop.
    /// </remarks>
    NewEnumMember = 1 << 7,
    /// <summary>
    /// The member access operation gives the member the argument list the code writes for it: <c>b(1)</c>, read or assigned, gives <c>1</c> to the
    /// default member of <c>b</c> (<strong>MS-VBAL §5.6.13</strong>), and <c>b.Item(1)</c> gives it to <c>Item</c>.
    /// </summary>
    /// <remarks>
    /// An element of an array that is assigned a value through the default member of the object it holds (<c>a(1) = 5</c>, <strong>MS-VBAL §5.4.3.8</strong>)
    /// gives that default member no argument: the subscripts select the element. A dictionary access is stated with <see cref="DictionaryAccess"/> instead.
    /// </remarks>
    ArgumentList = 1 << 9,

    /// <summary>
    /// Every flag.
    /// </summary>
    All = Explicit | Implicit | WithBlockVariable | DictionaryAccess | CallByNameAccess | LateBound
        | DefaultMember | NewEnumMember | ArgumentList
}
