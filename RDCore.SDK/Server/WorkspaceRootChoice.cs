using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Server.Configuration;
using RDCore.SDK.Workspace;
using System.Globalization;
using System.Resources;

namespace RDCore.SDK.Server;

/// <summary>
/// The root folder of the workspace a server works on, of the two it is told: the one its client opened, in the <c>initialize</c> request, and the one it
/// was started with (<see cref="SdkWorkspaceOptions.WorkspaceUri"/>, the <c>--workspace</c> argument).
/// </summary>
/// <remarks>
/// <para>
/// The request's folder wins: its <c>rootUri</c>, or else its first workspace folder, is what LSP says the client opened, and a client that is not the
/// platform's knows nothing of <c>--workspace</c>. The folder the server was started with is the root only when the request gives none.
/// </para>
/// <para>
/// When the two are different folders (<see cref="Differs"/>), the workspace is the request's: its settings, and the workspace the server's own children are
/// started on. The server says so in its log, and tells its user (<c>window/showMessage</c>, which LSP allows before <c>initialize</c> is answered).
/// </para>
/// </remarks>
/// <param name="Requested">The folder the request gives; <see langword="null"/> when it gives none, or a URI that is not a <c>file</c> URI.</param>
/// <param name="Started">
/// The folder the server was started with; <see langword="null"/> when it was started with none, or with a URI that is not a <c>file</c> URI.
/// </param>
public sealed record class WorkspaceRootChoice(string? Requested, string? Started)
{
    private static readonly ResourceManager Messages = new("RDCore.SDK.Workspace.WorkspaceFolderMessages", typeof(WorkspaceRootChoice).Assembly);

    /// <summary>
    /// The root folder of the workspace: the one the request gives, or else the one the server was started with; <see langword="null"/> when neither names
    /// a folder.
    /// </summary>
    public string? Root => Requested ?? Started;

    /// <summary>
    /// Whether the request and the server's start both name a folder, and not the same one (<see cref="WorkspacePaths.Same"/>): a separator at the end, the
    /// case of the drive on Windows, or a character a URI escapes, makes no difference.
    /// </summary>
    public bool Differs => Requested is not null && Started is not null && !WorkspacePaths.Same(Requested, Started);

    /// <summary>
    /// What the server tells its user when the two are different folders, in the language of the user interface; <see langword="null"/> when they are not.
    /// </summary>
    public string? Message => Differs
        ? string.Format(CultureInfo.CurrentCulture, Messages.GetString("WorkspaceRoot_Differs", CultureInfo.CurrentUICulture)!, Requested, Started)
        : null;

    /// <summary>
    /// The folders an <c>initialize</c> request and a server's start name.
    /// </summary>
    /// <param name="request">The request: its <c>rootUri</c>, or else its first workspace folder.</param>
    /// <param name="workspace">The workspace the server was started with, a path or a URI (<see cref="SdkWorkspaceOptions.WorkspaceUri"/>).</param>
    public static WorkspaceRootChoice Of(InitializeParams request, string? workspace) => new(
        FolderOf(request.RootUri) ?? FolderOf(request.WorkspaceFolders?.FirstOrDefault()?.Uri),
        workspace is { Length: > 0 } ? WorkspaceSettingsStore.FolderOf(workspace) : null);

    private static string? FolderOf(DocumentUri? uri)
        => uri is not null && string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase) ? uri.GetFileSystemPath() : null;
}
