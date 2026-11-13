namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The commands of the platform's code actions that the language server runs (<strong>LSP 3.17</strong> <c>workspace/executeCommand</c>): a client that
/// takes such an action sends its command back as it is, with its arguments. The commands a client runs itself are <see cref="RefactoringCommands"/>.
/// </summary>
public static class ServerCommands
{
    /// <summary>
    /// Imports a VB6 project: writes a <c>.rdproj</c> project file (<see cref="RDCore.SDK.Workspace.ProjectFile"/>) of the VB6 project in the folder of its
    /// <c>.vbp</c>. Its one argument is the URI of the <c>.vbp</c>. It is the command of the <see cref="RefactoringKinds.ImportProject"/> source action.
    /// </summary>
    public const string ImportProject = "rdcore.importProject";
}
