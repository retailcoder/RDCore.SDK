using System.IO.Abstractions;
using System.Text.Json;

namespace RDCore.SDK.Workspace;

/// <summary>
/// Reads and writes a workspace's <c>.rdworkspace</c> (<see cref="WorkspaceFile"/>, <strong>RD-VBAL §2.2.2</strong>).
/// </summary>
public interface IWorkspaceFileLoader
{
    /// <summary>
    /// Resolves the path a <c>.rdworkspace</c> would have under <paramref name="workspaceRoot"/>, and reports whether a file is there.
    /// </summary>
    /// <param name="workspaceRoot">The absolute path of the folder.</param>
    /// <param name="workspaceFilePath">The path of the <c>.rdworkspace</c>, whether or not there is one.</param>
    /// <returns><see langword="true"/> when a file is at <paramref name="workspaceFilePath"/>.</returns>
    bool TryLocate(string workspaceRoot, out string workspaceFilePath);

    /// <summary>
    /// Reads the <c>.rdworkspace</c> under <paramref name="workspaceRoot"/>: the folder is a workspace when it has one that can be read.
    /// </summary>
    /// <param name="workspaceRoot">The absolute path of the folder.</param>
    /// <param name="token">A token that cancels the read.</param>
    /// <returns>The workspace; <see langword="null"/> when the folder has no <c>.rdworkspace</c>, or one that is not JSON or not a JSON object.</returns>
    Task<WorkspaceFile?> LoadAsync(string workspaceRoot, CancellationToken token = default);

    /// <summary>
    /// Writes <paramref name="workspace"/> to the <c>.rdworkspace</c> under <paramref name="workspaceRoot"/>, indented, over any file there. The folder must
    /// exist.
    /// </summary>
    /// <param name="workspaceRoot">The absolute path of the workspace root.</param>
    /// <param name="workspace">The workspace to write.</param>
    /// <param name="token">A token that cancels the write.</param>
    /// <exception cref="IOException">The file could not be written.</exception>
    Task SaveAsync(string workspaceRoot, WorkspaceFile workspace, CancellationToken token = default);
}

/// <inheritdoc cref="IWorkspaceFileLoader"/>
/// <param name="fileSystem">The file system the workspace is on.</param>
public sealed class WorkspaceFileLoader(IFileSystem fileSystem) : IWorkspaceFileLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <inheritdoc/>
    public bool TryLocate(string workspaceRoot, out string workspaceFilePath)
    {
        workspaceFilePath = fileSystem.Path.Combine(workspaceRoot, WorkspaceFile.FileName);
        return fileSystem.File.Exists(workspaceFilePath);
    }

    /// <inheritdoc/>
    public async Task<WorkspaceFile?> LoadAsync(string workspaceRoot, CancellationToken token = default)
    {
        if (!TryLocate(workspaceRoot, out var path))
        {
            return null;
        }

        await using var stream = fileSystem.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            return await WorkspaceFile.ReadAsync(stream, token);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(string workspaceRoot, WorkspaceFile workspace, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        var path = fileSystem.Path.Combine(workspaceRoot, WorkspaceFile.FileName);
        await using var stream = fileSystem.File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, workspace, SerializerOptions, token);
    }
}
