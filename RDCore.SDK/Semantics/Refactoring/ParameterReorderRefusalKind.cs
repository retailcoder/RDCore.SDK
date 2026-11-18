namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// Why the reordering of a procedure's parameters is refused: what <see cref="RefactoringTitles.ParameterReorderRefusal"/> tells the reader.
/// </summary>
public enum ParameterReorderRefusalKind
{
    /// <summary>
    /// There is no procedure at the position the reordering is asked for.
    /// </summary>
    NoProcedure,

    /// <summary>
    /// The procedure has fewer than two parameters.
    /// </summary>
    OneParameter,

    /// <summary>
    /// The procedure is declared by a library, or by nothing the workspace declares.
    /// </summary>
    NotDeclaredByWorkspace,

    /// <summary>
    /// The procedure is an event.
    /// </summary>
    Event,

    /// <summary>
    /// The procedure is bound to an event it handles, and takes the parameters of the event in their order (<strong>MS-VBAL §5.3.1.8</strong>,
    /// <see cref="DeclarationBinding"/>).
    /// </summary>
    Handler,

    /// <summary>
    /// The procedure is an external procedure (<c>Declare</c>), which its library declares.
    /// </summary>
    ExternalProcedure,

    /// <summary>
    /// The new order does not name each parameter of the procedure once.
    /// </summary>
    Order,

    /// <summary>
    /// A required parameter would come after an optional one: optional parameters come last (<strong>MS-VBAL §5.3.1.5</strong>).
    /// </summary>
    Optional,

    /// <summary>
    /// The new order moves the <c>ParamArray</c>, which stays the last parameter (<strong>MS-VBAL §5.3.1.5</strong>).
    /// </summary>
    ParamArray,

    /// <summary>
    /// The parameters of a procedure that changes with this one (an implementation, an accessor of the same property) are not this one's.
    /// </summary>
    Mismatched,

    /// <summary>
    /// The procedure may be called late-bound, where the arguments would not be reordered (<strong>MS-VBAL §5.6.12</strong>).
    /// </summary>
    LateBound,

    /// <summary>
    /// The procedure is called by its name in a string, where the arguments would not be reordered (<strong>MS-VBAL §6.1.2.8.1.1</strong>).
    /// </summary>
    CalledByName,

    /// <summary>
    /// What a name written as the procedure's refers to cannot be told where it is written, so the reordering could miss a call.
    /// </summary>
    Unresolved,

    /// <summary>
    /// The arguments of a call do more than read a value, and would be evaluated in another order (<strong>MS-VBAL §5.3.1.11</strong>).
    /// </summary>
    Evaluated,

    /// <summary>
    /// A call passes arguments that do more than read a value, which another order of the parameters could evaluate in another order
    /// (<strong>MS-VBAL §5.3.1.11</strong>).
    /// </summary>
    MayEvaluate,

    /// <summary>
    /// The procedure is a document module's whose host is not known, or whose host's class the project does not resolve: what the host calls it for, and
    /// with which arguments, cannot be told.
    /// </summary>
    UnknownDocumentHost,
}
