namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The kinds of the refactorings the language server offers as code actions (<strong>LSP 3.17</strong> <c>CodeActionKind</c>): what a client asks for in a
/// code action request (<c>CodeActionContext.only</c>), and what names a refactoring in <see cref="RDCore.SDK.Platform.Protocol.RefactorPrepareParams.Kind"/>.
/// </summary>
/// <remarks>
/// Each kind is a refinement of one of LSP's own (<c>refactor.extract</c>, <c>refactor.inline</c>, <c>refactor.rewrite</c>), so a client that asks for
/// <c>refactor.extract</c> is offered every extraction.
/// </remarks>
public static class RefactoringKinds
{
    /// <summary>
    /// Extract Method: the selected statements of a procedure become a procedure of their own, and a call to it. Its choices are the new procedure's name,
    /// the value it returns, and whether it routes the exits of the statements back through its result; it shows the parameters, and why each is passed as
    /// it is.
    /// </summary>
    public const string ExtractMethod = "refactor.extract.method";
}
