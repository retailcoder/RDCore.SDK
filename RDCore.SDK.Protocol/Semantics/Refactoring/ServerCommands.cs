namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// The commands the language server runs (<strong>LSP 3.17</strong> <c>workspace/executeCommand</c>): a client that takes a code action of such a
/// command sends the command back as it is, with its arguments, and one that offers its user a command asks for it by its name. The commands a client
/// runs itself are <see cref="RefactoringCommands"/>.
/// </summary>
public static class ServerCommands
{
    /// <summary>
    /// Imports a VB6 project: writes a <c>.rdproj</c> project file (<see cref="RDCore.SDK.Workspace.ProjectFile"/>) of the VB6 project in the folder of its
    /// <c>.vbp</c>. Its one argument is the URI of the <c>.vbp</c>. It is the command of the <see cref="RefactoringKinds.ImportProject"/> source action.
    /// </summary>
    public const string ImportProject = "rdcore.importProject";

    /// <summary>
    /// Imports the indenter settings the user had in Rubberduck: the language server asks the client what it found
    /// (<c>rdcore/formatting/indenterSettings</c>), and asks it to take the <c>Formatting</c> settings they map to as its own
    /// (<c>rdcore/formatting/indenterSettings/apply</c>). It takes no argument, and is refused by a client that did not say it reads those settings.
    /// </summary>
    public const string ImportIndenterSettings = "rdcore.importIndenterSettings";
}
