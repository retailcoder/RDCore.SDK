using System.IO.Abstractions;
using System.Text;
using System.Text.Json;

namespace RDCore.SDK.Workspace;

/// <summary>
/// Loads a workspace's <c>.rdproj</c> <see cref="ProjectFile"/> from disk, read-only. Every platform
/// component that needs the project structure (the language server's workspace services and the
/// environment host's session composition) resolves the project through this service so there is a
/// single deserialization path.
/// </summary>
public interface IProjectFileLoader
{
    /// <summary>
    /// Resolves the path a <c>.rdproj</c> would occupy under <paramref name="workspaceRoot"/> and
    /// reports whether a file exists there.
    /// </summary>
    /// <param name="workspaceRoot">The absolute path of the workspace root directory.</param>
    /// <param name="projectFilePath">The resolved <c>.rdproj</c> path, whether or not it exists.</param>
    /// <returns><c>true</c> if a file exists at <paramref name="projectFilePath"/>.</returns>
    bool TryLocate(string workspaceRoot, out string projectFilePath);

    /// <summary>
    /// Loads and deserializes the <c>.rdproj</c> under <paramref name="workspaceRoot"/>, stamping its
    /// <see cref="ProjectFile.Uri"/> with the workspace root everything else is relative to. A reference to the standard library, which a project file written
    /// before lists, is not read: every project has it (<see cref="RDCoreProject.References"/>).
    /// </summary>
    /// <param name="workspaceRoot">The absolute path of the workspace root directory.</param>
    /// <param name="token">A token that cancels the read.</param>
    /// <exception cref="FileNotFoundException">No <c>.rdproj</c> exists under the workspace root.</exception>
    /// <exception cref="InvalidDataException">
    /// The <c>.rdproj</c> is not JSON, or not a JSON object; the message says where, in the line and the column an editor shows
    /// (<see cref="JsonErrorPlace.PlaceIn"/>).
    /// </exception>
    Task<ProjectFile> LoadAsync(string workspaceRoot, CancellationToken token = default);

    /// <summary>
    /// Loads the <c>.rdproj</c> under <paramref name="workspaceRoot"/> as <see cref="LoadAsync"/> does, or, when the folder has none, makes up the project
    /// the folder holds (<see cref="ProjectFile.IsSynthesized"/>): every source file of <paramref name="language"/> under it is a module, and nothing is
    /// written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A client that opens a folder - rather than a project - is given the project the folder is. The folder is searched to every depth, except the folders
    /// whose name starts with a dot (<c>.git</c>, <c>.github</c>) and links to other folders. Each folder that holds a module, at any depth, is a folder of the
    /// project. Paths are relative to the workspace root, separated by <c>/</c>, and in ordinal order, so the language server and the environment host, which
    /// each synthesize the project, make up the same one.
    /// </para>
    /// <para>
    /// A module name is unique in a project (<strong>RD-VBAL §2.2.3.3</strong>): of two files that declare the same <c>VB_Name</c>, the first in that order
    /// is the module, and the other is not in the project. The project is named after the folder when its name is an identifier, and <c>VBAProject</c>
    /// otherwise, as the VBE names a new one.
    /// </para>
    /// <para>
    /// A folder that holds exactly one Visual Basic 6 project (a <c>.vbp</c>) directly under it, and no project group (a <c>.vbg</c>), is that project's: its
    /// <see cref="RDCoreProject.SourceProject"/> is the <c>.vbp</c>'s file name, as an import of it writes, so the project's file says it is written in
    /// <see cref="SupportedLanguages.VB6"/> (<see cref="SupportedLanguages.OfProject"/>) and keeps saying so once it is first written. A folder with several
    /// <c>.vbp</c> files, or with a <c>.vbg</c>, names no source project, and no language.
    /// </para>
    /// </remarks>
    /// <param name="workspaceRoot">The absolute path of the workspace root directory.</param>
    /// <param name="language">The language the workspace is written in, whose <see cref="SupportedLanguage.FileTypes"/> are its source files.</param>
    /// <param name="token">A token that cancels the read.</param>
    /// <exception cref="InvalidDataException">
    /// The <c>.rdproj</c> is not JSON, or not a JSON object; the message says where, as <see cref="LoadAsync"/> says it.
    /// </exception>
    Task<ProjectFile> LoadOrSynthesizeAsync(string workspaceRoot, SupportedLanguage language, CancellationToken token = default);

    /// <summary>
    /// Loads the <c>.rdproj</c> under <paramref name="workspaceRoot"/> as <see cref="LoadAsync"/> does, when the folder has one that can be read: what is
    /// about to replace a project file keeps what it says (<see cref="VbpImport.Into"/>).
    /// </summary>
    /// <param name="workspaceRoot">The absolute path of the folder.</param>
    /// <param name="token">A token that cancels the read.</param>
    /// <returns>The project file; <see langword="null"/> when the folder has none, or one that is not JSON or not a project file.</returns>
    async Task<ProjectFile?> LoadIfReadableAsync(string workspaceRoot, CancellationToken token = default)
    {
        if (!TryLocate(workspaceRoot, out _))
        {
            return null;
        }

        try
        {
            return await LoadAsync(workspaceRoot, token);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <inheritdoc cref="IProjectFileLoader"/>
public sealed class ProjectFileLoader(IFileSystem fileSystem) : IProjectFileLoader
{
    /// <inheritdoc/>
    public bool TryLocate(string workspaceRoot, out string projectFilePath)
    {
        projectFilePath = fileSystem.Path.Combine(workspaceRoot, ProjectFile.FileName);
        return fileSystem.File.Exists(projectFilePath);
    }

    /// <inheritdoc/>
    public async Task<ProjectFile> LoadAsync(string workspaceRoot, CancellationToken token = default)
    {
        if (!TryLocate(workspaceRoot, out var path))
        {
            throw new FileNotFoundException($"No {ProjectFile.FileName} was found under the workspace root.", path);
        }

        var bytes = await fileSystem.File.ReadAllBytesAsync(path, token);
        await using var stream = new MemoryStream(bytes, writable: false);
        try
        {
            // read as any client of the platform reads it: what the file leaves out is empty, and the standard library is no reference of it.
            return await ProjectFile.ReadAsync(stream, workspaceRoot, token);
        }
        catch (JsonException exception)
        {
            // where the file stops being a project file (not JSON, or JSON of another shape), in the lines and columns an editor shows.
            var (line, column) = exception.PlaceIn(TextOf(bytes)) ?? (1, 1);
            throw new InvalidDataException(string.Format(Exceptions.ProjectFile_InvalidJson, path, line, column, WhatIsWrong(exception)), exception);
        }
    }

    // the text the reader read, which starts after the byte order mark.
    private static string TextOf(byte[] bytes)
    {
        var preamble = Encoding.UTF8.Preamble;
        return Encoding.UTF8.GetString(bytes.AsSpan(bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0));
    }

    // the reader's description of what is wrong, without the place it appends to it ("Path: $.Name | LineNumber: 2 | BytePositionInLine: 4."): the
    // message says where once, counted from 1.
    private static string WhatIsWrong(JsonException exception)
    {
        var message = exception.Message;
        var place = message.LastIndexOf(" Path: ", StringComparison.Ordinal);
        if (place < 0)
        {
            place = message.LastIndexOf(" LineNumber: ", StringComparison.Ordinal);
        }

        return place < 0 ? message : message[..place];
    }

    /// <inheritdoc/>
    public async Task<ProjectFile> LoadOrSynthesizeAsync(string workspaceRoot, SupportedLanguage language, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (TryLocate(workspaceRoot, out _))
        {
            return await LoadAsync(workspaceRoot, token);
        }

        var modules = new List<RDCoreModule>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new List<string>();
        await ScanAsync(workspaceRoot, workspaceRoot, language, modules, names, folders, token);

        var project = new RDCoreProject
        {
            Name = ProjectNameOf(fileSystem.Path.GetFileName(fileSystem.Path.TrimEndingDirectorySeparator(workspaceRoot))),
            Modules = [.. modules],
            Folders = [.. folders.Order(StringComparer.Ordinal)],
            SourceProject = SourceProjectOf(workspaceRoot),
        };

        return new ProjectFile(workspaceRoot, project) { IsSynthesized = true };
    }

    // the one Visual Basic 6 project directly under the folder, by its file name; none when there are several, or a project group says which go together.
    private string? SourceProjectOf(string workspaceRoot)
    {
        string[] files;
        try
        {
            files = [.. fileSystem.Directory.EnumerateFiles(workspaceRoot)];
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return null;
        }

        var projects = files.Where(file => HasExtension(file, ".vbp")).ToArray();
        return projects is [var project] && !files.Any(file => HasExtension(file, ".vbg")) ? fileSystem.Path.GetFileName(project) : null;
    }

    private bool HasExtension(string path, string extension) => string.Equals(fileSystem.Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);

    private async Task ScanAsync(string root, string directory, SupportedLanguage language,
        List<RDCoreModule> modules, HashSet<string> names, List<string> folders, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string[] files, directories;
        try
        {
            files = [.. fileSystem.Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal)];
            directories = [.. fileSystem.Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal)];
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // a folder that cannot be read holds nothing of the project.
            return;
        }

        foreach (var file in files.Where(language.IsSourceFile))
        {
            var relativeUri = RelativeUriOf(root, file);
            var name = ModuleName.Resolve(await ReadSourceAsync(file, token), relativeUri);
            if (names.Add(name))
            {
                modules.Add(new RDCoreModule { RelativeUri = relativeUri });
            }
        }

        foreach (var subdirectory in directories)
        {
            if (fileSystem.Path.GetFileName(subdirectory).StartsWith('.')
                || fileSystem.File.GetAttributes(subdirectory).HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var count = modules.Count;
            await ScanAsync(root, subdirectory, language, modules, names, folders, token);
            if (modules.Count > count)
            {
                folders.Add(RelativeUriOf(root, subdirectory));
            }
        }
    }

    private async Task<string?> ReadSourceAsync(string path, CancellationToken token)
    {
        try
        {
            return ProjectModuleKinds.ReadText(await fileSystem.File.ReadAllBytesAsync(path, token));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // the file name names a module whose source cannot be read.
            return null;
        }
    }

    private string RelativeUriOf(string root, string path) => fileSystem.Path.GetRelativePath(root, path).Replace('\\', '/');

    // an identifier (MS-VBAL §3.3.5): a letter, then letters, digits and underscores; not VBA, which names the standard library.
    private static string ProjectNameOf(string? folderName)
        => folderName is { Length: > 0 and <= 255 } name
            && char.IsAsciiLetter(name[0])
            && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            && !string.Equals(name, "VBA", StringComparison.OrdinalIgnoreCase)
            ? name
            : RDCoreProject.DefaultName;
}
