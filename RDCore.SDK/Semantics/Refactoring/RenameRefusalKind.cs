namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// Why a rename is refused: what <see cref="RefactoringTitles.RenameRefusal"/> tells the reader.
/// </summary>
public enum RenameRefusalKind
{
    /// <summary>
    /// There is no name at the position the rename is asked for.
    /// </summary>
    NoName,

    /// <summary>
    /// The name is declared by a library, or by nothing the workspace declares.
    /// </summary>
    NotDeclaredByWorkspace,

    /// <summary>
    /// The name is the name of a document module, which its document gives it.
    /// </summary>
    Type,

    /// <summary>
    /// The name is a procedure's that is named after the event it handles or the interface member it implements (MS-VBAL §5.3.1.3, §5.3.1.8).
    /// </summary>
    NamedByConvention,

    /// <summary>
    /// The name is an event's, which its handlers are named after.
    /// </summary>
    Event,

    /// <summary>
    /// The name is a variable's declared <c>WithEvents</c>, which its handlers are named after.
    /// </summary>
    WithEvents,

    /// <summary>
    /// The name is a control's, which its form's designer gives it.
    /// </summary>
    Control,

    /// <summary>
    /// The name is an external procedure's (<c>Declare</c>), which its library gives it.
    /// </summary>
    ExternalProcedure,

    /// <summary>
    /// The name is a member of an interface, which its implementations are named after.
    /// </summary>
    InterfaceMember,

    /// <summary>
    /// The new name is empty.
    /// </summary>
    Empty,

    /// <summary>
    /// The new name is not an identifier.
    /// </summary>
    NotAnIdentifier,

    /// <summary>
    /// The new name is a reserved identifier of the language.
    /// </summary>
    Reserved,

    /// <summary>
    /// The new name already means something where the name is written: renamed, the code would refer to something else.
    /// </summary>
    Conflict,

    /// <summary>
    /// What a name is bound to cannot be told where it is written, so the rename could miss it.
    /// </summary>
    Unresolved,

    /// <summary>
    /// The new name of a module is longer than a module's name can be (MS-VBAL §4.2).
    /// </summary>
    ModuleNameTooLong,
}
