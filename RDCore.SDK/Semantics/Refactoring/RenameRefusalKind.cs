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
    /// The name is a procedure's that is bound to the event it handles or the interface member it implements (<see cref="DeclarationBinding"/>,
    /// <strong>MS-VBAL §5.3.1.8</strong>, <strong>§5.3.1.9</strong>, <strong>§5.3.1.10</strong>), and the workspace declares neither part of its name:
    /// neither the source the name begins with (the interface, the variable or the control; the <c>Class</c> and <c>UserForm</c> of a module's own
    /// handlers name no declaration), nor the event or member it is bound to. Renamed whole, it would be bound to neither. When the workspace declares a
    /// part, a rename of the procedure is a rename of a part (the one it declares, or, when it declares both, the one the rename starts at), and the
    /// procedure is renamed with it.
    /// </summary>
    BoundHandlerOrImplementation,

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

    /// <summary>
    /// The name is a procedure's of a document module whose host is not known, or whose host's class the project does not resolve: what the host calls
    /// the procedure for cannot be told, so none of its procedures is renamed, whatever its name.
    /// </summary>
    UnknownDocumentHost,

    /// <summary>
    /// The name is a control's of a VBA UserForm, whose designer keeps its controls' names in the form's companion <c>.frx</c> file, and the server
    /// could not read that file as it is on disk: the form names none, the file is not there or cannot be read, or it changed since the server read it.
    /// </summary>
    UserFormControl,

    /// <summary>
    /// The name is a control's that a string property of its form, or of another of its controls, holds as its whole value
    /// (<see cref="Workspace.Forms.FormDesigner.PropertiesNaming"/>): the property may name the control, as an UpDown's <c>BuddyControl</c> names its buddy.
    /// The property is the refusal's other name, as <c>UpDown1.BuddyControl</c>.
    /// </summary>
    ControlNamedByProperty,

    /// <summary>
    /// The name is a control's of a VBA UserForm, which <c>MSFormsWriter.RenameControl</c> does not rename in the form's <c>.frx</c> file.
    /// Its reason is the refusal's other name.
    /// </summary>
    UserFormControlNotWritten,

    /// <summary>
    /// The name is a control's of a VBA UserForm, whose rename replaces the form's <c>.frx</c> file, and the client cannot undo a file change when the
    /// rename's edit fails (<c>WorkspaceEditExtensions.TakesFileReplacement</c>): the code would name the control by its
    /// new name, and the file by its old one.
    /// </summary>
    FileChangeNotUndoable,
}
