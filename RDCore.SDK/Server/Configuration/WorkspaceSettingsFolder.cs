using Microsoft.Extensions.Configuration;
using RDCore.SDK.Workspace;
using System.IO.Abstractions;

namespace RDCore.SDK.Server.Configuration;

// the workspace whose settings are a layer of a component's configuration (WorkspaceSettingsConfiguration.AddWorkspaceSettings), which the component moves
// to another folder when it learns that its workspace is another than the one it was started with (WorkspaceRootChoice): the configuration then says what
// that folder's workspace sets, and so do the component's settings objects (LiveSettings.MoveWorkspace).
internal sealed class WorkspaceSettingsFolder(
    IConfiguration configuration, SettingsSchema schema, string? folder, LiveSettings? live, WorkspaceSettingsStore store, IFileSystem? fileSystem = null)
{
    private readonly object _moving = new();

    // the folder whose settings the component has; none when it was started on no folder and has not moved to one.
    public string? Folder { get; private set; } = folder;

    // false when the folder is the one the component has, or the configuration is not one a layer can be read again in.
    public bool MoveTo(string workspaceFolder)
    {
        lock (_moving)
        {
            if ((Folder is { } current && WorkspacePaths.Same(current, workspaceFolder)) || configuration is not IConfigurationRoot root)
            {
                return false;
            }

            // a component started on no folder has no layer yet: a configuration that is also its builder takes one, as AppHost adds it.
            if (!WorkspaceSettingsConfiguration.MoveTo(root, workspaceFolder))
            {
                if (configuration is not IConfigurationBuilder builder)
                {
                    return false;
                }

                builder.AddWorkspaceSettings(workspaceFolder, schema, fileSystem);
            }

            Folder = workspaceFolder;
            var configured = root.GetSection(SettingsSchema.Root).AsEnumerable(makePathsRelative: true)
                .Where(pair => pair.Value is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.OrdinalIgnoreCase);
            live?.MoveWorkspace(configured, store.Values(workspaceFolder));
            return true;
        }
    }
}
