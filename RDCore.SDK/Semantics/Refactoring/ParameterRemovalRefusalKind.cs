namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// Why the removal of a parameter is refused: what <see cref="RefactoringTitles.ParameterRemovalRefusal"/> tells the reader.
/// </summary>
public enum ParameterRemovalRefusalKind
{
    /// <summary>
    /// There is no parameter at the position the removal is asked for.
    /// </summary>
    NoParameter,

    /// <summary>
    /// The procedure is declared by a library, or by nothing the workspace declares.
    /// </summary>
    NotDeclaredByWorkspace,

    /// <summary>
    /// The procedure uses the parameter: without it, the name would refer to nothing (<strong>MS-VBAL §5.3.1.5</strong>).
    /// </summary>
    Used,

    /// <summary>
    /// The parameter is a <c>ParamArray</c> (<strong>MS-VBAL §5.3.1.5</strong>).
    /// </summary>
    ParamArray,

    /// <summary>
    /// The parameter is the value a <c>Property Let</c> or <c>Property Set</c> assigns, which it always takes (<strong>MS-VBAL §5.3.1.7</strong>).
    /// </summary>
    Value,

    /// <summary>
    /// The parameter is an event's.
    /// </summary>
    Event,

    /// <summary>
    /// The procedure is bound to an event it handles, and takes the parameters of the event (<strong>MS-VBAL §5.3.1.8</strong>, <see cref="DeclarationBinding"/>).
    /// </summary>
    Handler,

    /// <summary>
    /// The procedure is an external procedure (<c>Declare</c>), which its library declares.
    /// </summary>
    ExternalProcedure,

    /// <summary>
    /// An argument passed for the parameter does more than read a value: removing it would change what the code does (<strong>MS-VBAL §5.6.13.1</strong>).
    /// </summary>
    Evaluated,

    /// <summary>
    /// The procedure may be called late-bound, where the argument passed for the parameter would not be removed (<strong>MS-VBAL §5.6.12</strong>).
    /// </summary>
    LateBound,

    /// <summary>
    /// The procedure is called by its name in a string, where the argument passed for the parameter would not be removed
    /// (<strong>MS-VBAL §6.1.2.8.1.1</strong>).
    /// </summary>
    CalledByName,

    /// <summary>
    /// What a name written as the procedure's refers to cannot be told where it is written, so the removal could miss a call.
    /// </summary>
    Unresolved,

    /// <summary>
    /// The procedure is a document module's whose host is not known, or whose host's class the project does not resolve: what the host calls it for, and
    /// with which arguments, cannot be told.
    /// </summary>
    UnknownDocumentHost,
}
