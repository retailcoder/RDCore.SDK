namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The kinds of the refactorings and source actions the language server offers as code actions (<strong>LSP 3.17</strong> <c>CodeActionKind</c>): what a
/// client asks for in a code action request (<c>CodeActionContext.only</c>), and what names a refactoring in
/// <see cref="RDCore.SDK.Platform.Protocol.RefactorPrepareParams.Kind"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each kind is a refinement of one of LSP's own (<c>refactor</c>, <c>refactor.extract</c>, <c>refactor.inline</c>, <c>refactor.rewrite</c>, <c>source</c>),
/// so a client that asks for <c>refactor.extract</c> is offered every extraction.
/// </para>
/// <para>
/// A client that asks for no kind is offered every action. A client that can show only some of them asks for those, by these names and LSP's own
/// <c>quickfix</c>: it is offered no kind it did not ask for, and none added later.
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
    /// project file of its own. Its command is <c>rdcore.importProject</c>, which the language server runs: it makes the project file in the folder of the
    /// <c>.vbp</c>.
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
}
