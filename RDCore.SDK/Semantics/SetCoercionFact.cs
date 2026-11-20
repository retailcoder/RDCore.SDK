namespace RDCore.SDK.Semantics;

/// <summary>
/// How the class an expression is declared as relates to the class its value is Set-coerced to, through the <c>Implements</c> statements of the
/// workspace's class modules (<strong>MS-VBAL §5.2.4.2</strong>). A variable declared as a class holds an object of that class, or of a class that
/// implements it, and Set-coercion takes an object of the destination's class or of a class that implements it (<strong>MS-VBAL §5.5.2.2.1</strong>).
/// </summary>
public enum ClassRelation
{
    /// <summary>
    /// Both are the same class.
    /// </summary>
    Same = 0,

    /// <summary>
    /// The source's class implements the destination's, itself or through a class it implements.
    /// </summary>
    Implements,

    /// <summary>
    /// The destination's class implements the source's, itself or through a class it implements: an object of the source's declared type can be an
    /// object of the destination's class.
    /// </summary>
    ImplementedBy,

    /// <summary>
    /// Neither implements the other, and a class of the workspace implements both: an object of the source's declared type can be an object of that
    /// class.
    /// </summary>
    SharedImplementer,

    /// <summary>
    /// Neither implements the other, and no class of the workspace implements both: no object of the source's declared type is an object of the
    /// destination's class or of a class that implements it.
    /// </summary>
    Unrelated,
}

/// <summary>
/// The Set-coercion of an expression's value to a class (<strong>MS-VBAL §5.5.2</strong>), where both the expression's declared type and the destination's
/// are classes of the workspace's projects: the value of a <c>Set</c> statement, Set-coerced to the declared type of its target
/// (<strong>MS-VBAL §5.4.3.9</strong>), or an argument given to a parameter declared as a class, Set-assigned to the parameter
/// (<strong>MS-VBAL §5.3.1.11</strong>).
/// </summary>
/// <param name="Destination">The name of the class the value is Set-coerced to.</param>
/// <param name="Relation">How the class the expression is declared as relates to it.</param>
/// <remarks>
/// Not stated for a class of a library: what an object of a library answers to is the library's to say when the code runs. Nor for a destination declared
/// <c>Object</c> or <c>Variant</c>, which takes an object of any class, nor for an argument given to a <c>ByRef</c> parameter of its own declared type,
/// which is passed by reference.
/// </remarks>
public sealed record class SetCoercionFact(string Destination, ClassRelation Relation);
