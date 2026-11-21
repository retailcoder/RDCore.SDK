namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The kinds of the refactorings and source actions the language server offers as code actions (<strong>LSP 3.17</strong> <c>CodeActionKind</c>): what a
/// client asks for in a code action request (<c>CodeActionContext.only</c>), and what names a refactoring in
/// <see cref="RDCore.SDK.Platform.Protocol.RefactorPrepareParams.Kind"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each kind is a refinement of one of LSP's own (<c>refactor</c>, <c>refactor.extract</c>, <c>refactor.inline</c>, <c>refactor.move</c>,
/// <c>refactor.rewrite</c>, <c>source</c>), so a client that asks for <c>refactor.extract</c> is offered every extraction. <c>refactor.move</c> is a kind
/// of <strong>LSP 3.18</strong> and not of 3.17: a client of 3.17 asks for the kinds that move code as <c>refactor</c>, or by their own names. A
/// refactoring of a whole module is a source action.
/// </para>
/// <para>
/// A client that asks for no kind is offered every action. A client that can show only some of them asks for those, by these names and LSP's own
/// <c>quickfix</c>: it is offered only those kinds and their refinements, so a kind it does not know is never offered to it.
/// </para>
/// </remarks>
public static class RefactoringKinds
{
    /// <summary>
    /// Rename: a symbol and every reference to it, in every module that refers to it, take a new name. The action has no edit: its command is
    /// <see cref="RefactoringCommands.Rename"/>, with which the client asks its user for the name and sends <c>textDocument/rename</c>. A name that a diagnostic
    /// flags is offered the same command as a quick fix (<c>quickfix</c>) instead.
    /// </summary>
    public const string Rename = "refactor.rename";

    /// <summary>
    /// Import Project: a source action of the whole workspace, offered in every document of a workspace whose folder has a VB6 project (<c>.vbp</c>) and no
    /// project file of its own. Its command is <see cref="ServerCommands.ImportProject"/>, which the language server runs: it writes a <c>.rdproj</c>
    /// project file (<see cref="RDCore.SDK.Workspace.ProjectFile"/>) of the VB6 project in the folder of the <c>.vbp</c>.
    /// </summary>
    public const string ImportProject = "source.rdcore.importProject";

    /// <summary>
    /// Extract Method: the selected statements of a procedure become a procedure of their own, and a call to it. Its choices are the new procedure's name,
    /// the value it returns, and whether it routes the exits of the statements back through its result; it shows the parameters, and why each is passed as
    /// it is.
    /// </summary>
    public const string ExtractMethod = "refactor.extract.method";

    /// <summary>
    /// Convert GoSub to Procedure: the <c>GoSub</c> routine a label starts becomes a procedure of its own (<strong>MS-VBAL §5.4.2.14</strong>), each
    /// <c>GoSub</c> of it a call, each <c>On…GoSub</c> of it a <c>Select Case</c> with the call in its case (<strong>MS-VBAL §5.4.2.16</strong>), and each
    /// <c>Return</c> a way out of it. The range it is asked for is on the label, on a <c>GoSub</c> statement that names it, or on one of its <c>Return</c>
    /// statements. Its choices are the new procedure's name, and its parameters, with why each is passed as it is.
    /// </summary>
    public const string ConvertGoSub = "refactor.extract.gosub";

    /// <summary>
    /// Inline Member: a call of a <c>Sub</c>, a <c>Function</c> or a <c>Property Get</c> of the workspace is replaced by the member's body
    /// (<strong>MS-VBAL §5.3.1</strong>). Each argument is given to the parameter it binds to (<strong>MS-VBAL §5.3.1.11</strong>), and the result of a
    /// function is given to a local, or written in place of the call when its body is one assignment of its result. The range it is asked for is on a call
    /// of the member, which inlines that call, or on the member's name where it is declared, which inlines every call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its choices are the sites, a <see cref="RDCore.SDK.Platform.Protocol.RefactorChoiceKind.List"/> whose items are the calls
    /// (<see cref="RDCore.SDK.Platform.Protocol.RefactorItem.Location"/>), each with a <see cref="RDCore.SDK.Platform.Protocol.RefactorChoiceKind.Flag"/> that
    /// inlines it and a reason that says the form it is written in; and whether the member is removed, a
    /// <see cref="RDCore.SDK.Platform.Protocol.RefactorChoiceKind.Flag"/> offered when every reference to it is a site that is inlined. A site that is
    /// refused is a <see cref="RDCore.SDK.Platform.Protocol.RefactorNoticeKind.Refusal"/> of its item
    /// (<see cref="RDCore.SDK.Platform.Protocol.RefactorNotice.Choice"/> and <see cref="RDCore.SDK.Platform.Protocol.RefactorNotice.Item"/>), located where
    /// what refuses it is written.
    /// </para>
    /// <para>
    /// A member that the runtime or a host calls (an event handler, an implemented member, an entry point), a recursive or <c>Static</c> one, and a
    /// <c>Declare</c>, which has no body, are refused. So is a call that does not bind to the member early: a late-bound call, or one through an interface.
    /// </para>
    /// </remarks>
    public const string InlineMember = "refactor.inline.member";

    /// <summary>
    /// Move Member Up: the procedure a range is in, with the comments directly above it and its <c>Attribute</c> lines, moves above the procedure before it.
    /// The <c>Property Get</c>, <c>Let</c> and <c>Set</c> of one name, written one after another, move together, and a procedure that moves
    /// past them moves past all of them. A move into a branch of an <c>#If</c> block, or out of one, is refused (<strong>MS-VBAL §3.4</strong>), and the
    /// first procedure of a module does not move into its declaration section (<strong>MS-VBAL §5.2</strong>). It asks nothing: the action has its edit.
    /// </summary>
    public const string MoveMemberUp = "refactor.move.member.up";

    /// <summary>
    /// Move Member Down: the procedure a range is in, with the comments directly above it and its <c>Attribute</c> lines, moves below the procedure after it,
    /// as <see cref="MoveMemberUp"/> moves it above the one before it. A move into a branch of an <c>#If</c> block, or out of one, is refused
    /// (<strong>MS-VBAL §3.4</strong>). It asks nothing: the action has its edit.
    /// </summary>
    public const string MoveMemberDown = "refactor.move.member.down";

    /// <summary>
    /// Sort Members: the procedures of a module, each with the comments directly above it and its <c>Attribute</c> lines, are written by kind (the
    /// <c>Sub</c>s, then the <c>Function</c>s, then the properties), then by name. The <c>Property Get</c>, <c>Let</c> and <c>Set</c> of one name, written one
    /// after another, stay together, in the order they are written. Procedures are sorted among those written one after another with nothing but blank lines
    /// between them: none is moved past a comment on lines of its own or an <c>#If</c> directive, so none goes into a branch of an <c>#If</c> block or out of
    /// one (<strong>MS-VBAL §3.4</strong>). It asks nothing: the action has its edit.
    /// </summary>
    public const string SortMembers = "source.sortMembers";

    /// <summary>
    /// Encapsulate Field: a <c>Public</c> variable of a module becomes private state, and the module gets <c>Public</c> properties of the variable's name
    /// (<strong>MS-VBAL §5.3.1.7</strong>): a <c>Property Get</c>, with a <c>Property Let</c> for a value, a <c>Property Set</c> for an object, and both for a
    /// <c>Variant</c>. The code of other modules is unchanged, and calls the properties. A caller that passed the variable <c>ByRef</c> passes a copy of the
    /// property's value instead, so a change it makes to it is lost: the user accepts that change for each such caller.
    /// </summary>
    public const string EncapsulateField = "refactor.rewrite.encapsulateField";

    /// <summary>
    /// Implement Interface: a class module gets a <c>Private</c> procedure for each member of an interface class it names in an <c>Implements</c> directive
    /// and does not implement (<strong>MS-VBAL §5.2.4.2</strong>, <strong>§5.3.1.9</strong>), or a handler for each event of a <c>WithEvents</c>
    /// variable's class that it does not handle (<strong>MS-VBAL §5.3.1.8</strong>). The range it is asked for is on the directive or on the variable's
    /// declaration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each new procedure is named for the interface or the variable and the member. It has the member's parameters, with their names, explicit passing,
    /// types and <c>Optional</c> defaults, and the member's type. A <c>Public</c> variable of the interface gets a <c>Property Get</c>, with a
    /// <c>Property Let</c> for a value, a <c>Property Set</c> for an object, and both for a <c>Variant</c>.
    /// </para>
    /// <para>
    /// An interface member is implemented by a call to the class's own <c>Public</c> member of the same kind, name, parameters and type, when it has one;
    /// otherwise it raises error 5. An event handler is an <c>Exit Sub</c> with a comment, so that it is not an empty procedure and raises nothing to the
    /// code that raises the event. It only adds code and asks nothing: the action has its edit.
    /// </para>
    /// </remarks>
    public const string ImplementInterface = "refactor.rewrite.implementInterface";

    /// <summary>
    /// Remove Parameter: a parameter of a procedure is removed from its declaration, and its argument from every call that passes it, by position or by
    /// name (<strong>MS-VBAL §5.3.1.5</strong>, <strong>§5.6.13.1</strong>). The accessors of a property lose it together, and so do the member of a class
    /// that other classes implement and the procedures that implement it. The range it is asked for is on the parameter.
    /// </summary>
    public const string RemoveParameter = "refactor.rewrite.removeParameter";

    /// <summary>
    /// Reorder Parameters: the parameters of a procedure are put in another order in its declaration, and the arguments passed by position in every call
    /// to it with them (<strong>MS-VBAL §5.3.1.5</strong>, <strong>§5.6.13.1</strong>). The accessors of a property change together, and so do the member
    /// of a class that other classes implement and the procedures that implement it; the value of a <c>Property Let</c> or <c>Property Set</c> stays last.
    /// The range it is asked for is on the name of the procedure where it is declared, or on one of its parameters. Its choice is the list of the
    /// parameters, in the order the user puts them in.
    /// </summary>
    public const string ReorderParameters = "refactor.rewrite.reorderParameters";

    /// <summary>
    /// Move Closer To Usage: the declaration of a variable moves to a line of its own before the statement that first uses it, in the innermost block that
    /// holds every use of it (<strong>MS-VBAL §5.4.3.1</strong>). A <c>Private</c> variable of a module that a single procedure uses moves into that
    /// procedure, declared <c>Static</c> or <c>Dim</c>, the user's choice: <c>Static</c> by default, which keeps its value between calls. In a class module
    /// either keyword changes the variable's extent (<strong>MS-VBAL §2.3</strong>), and the user accepts that change. The range it is asked for is on the
    /// variable's declaration or on a use of it.
    /// </summary>
    public const string MoveCloserToUsage = "refactor.move.closerToUsage";
}
