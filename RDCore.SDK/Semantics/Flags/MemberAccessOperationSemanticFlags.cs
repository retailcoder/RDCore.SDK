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
    /// The member access operation refers to the default member of an object that the default member of its parent object returned: that default member
    /// takes no argument and returns an object, so the arguments given to it, the name of a dictionary access, or the value expected of it go on to the
    /// default member of that object (<strong>MS-VBAL §5.6.2.3</strong>, <strong>§5.6.13</strong>, <strong>§5.6.14</strong>). <c>rs("Name")</c> and
    /// <c>rs!Name</c> of an ADO <c>Recordset</c>, whose default member <c>Fields</c> returns its <c>Fields</c> collection, are such accesses.
    /// </summary>
    /// <remarks>
    /// It is stated with <see cref="DefaultMember"/> and <see cref="Implicit"/>, and with <see cref="DictionaryAccess"/> for a dictionary access. A value
    /// expected of an object, and read through more than one default member, is also a conversion stated <see cref="ConversionSemanticFlags.Recursive"/>.
    /// </remarks>
    Recursive = 1 << 8,
    /// <summary>
    /// The member access operation gives the member the argument list the code writes for it, empty or not: <c>b(1)</c> and <c>b()</c>, read or assigned,
    /// give their lists to the default member of <c>b</c> (<strong>MS-VBAL §5.6.13</strong>), and <c>b.Item(1)</c> gives its list to <c>Item</c>.
    /// </summary>
    /// <remarks>
    /// Arguments given to a member that declares no parameters are not its: they index the array that a public variable holds, or what a
    /// <c>Property Get</c> or a <c>Function</c> returns. An element of an array that is assigned a value through the default member of the object it holds
    /// (<c>a(1) = 5</c>, <strong>MS-VBAL §5.4.3.8</strong>) gives that default member no argument: the subscripts select the element. A dictionary access is
    /// stated with <see cref="DictionaryAccess"/> instead.
    /// <para>
    /// A member the code names and assigns (<c>b.Item(1) = 5</c>) has this flag where it is given arguments and the host knows the members of the object's
    /// class. An assigned member of an object called late through automation does not have it.
    /// </para>
    /// </remarks>
    ArgumentList = 1 << 9,
    /// <summary>
    /// The member access operation names a member that the class its object is declared as does not declare, and that class is a class of a referenced
    /// library, which is extensible: the member is bound where the code runs, to a member of that name the object has (<strong>MS-VBAL §5.6.12</strong>).
    /// <c>ws.Rnage</c> of a variable declared as Excel's <c>Worksheet</c> is such an access; so is <c>c.Nope</c> of a <c>Collection</c>.
    /// </summary>
    /// <remarks>
    /// It is stated with <see cref="LateBound"/>. The object of a form's control is wrapped in its extender, whose members the control's class does not
    /// declare: an access to a member of a control does not have this flag, and neither does one on an object declared as a class module of the workspace,
    /// <c>VB_Extensible</c> or not.
    /// </remarks>
    NotOnDeclaredInterface = 1 << 10,

    /// <summary>
    /// Every flag.
    /// </summary>
    All = Explicit | Implicit | WithBlockVariable | DictionaryAccess | CallByNameAccess | LateBound
        | DefaultMember | NewEnumMember | Recursive | ArgumentList | NotOnDeclaredInterface
}
