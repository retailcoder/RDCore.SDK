using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using System.Globalization;
using System.IO.Abstractions;

namespace RDCore.SDK.Server.Configuration;

/// <summary>
/// The settings a workspace keeps for itself, in the <c>Configuration</c> section of the <c>appsettings.json</c> at its root: whoever opens the workspace
/// works with them (<see cref="WorkspaceSettingsConfiguration.AddWorkspaceSettings"/>).
/// </summary>
/// <remarks>
/// <para>
/// A workspace sets only what <see cref="CanSet"/> says it can: no setting of the deployment (<see cref="SettingReach.Deployment"/>), and none that decides
/// what a program may do to the machine it runs on (<see cref="SettingDescriptor.IsTrustSensitive"/>). A workspace can come from anywhere, a cloned
/// repository included, and it grants itself no rights. Nothing else of the file is read: not a setting the schema does not have, not a value the setting
/// cannot be, and not the program's own settings (<c>ProgramSettings</c>), which are kept in the same file.
/// </para>
/// <para>
/// Adding an item to a list, or removing one, writes the file again, as <see cref="UserSettingsStore"/> writes the user's: its other sections are kept, and
/// its comments are not.
/// </para>
/// </remarks>
/// <param name="fileSystem">The file system the workspace is on.</param>
/// <param name="schema">The settings of the application.</param>
public sealed class WorkspaceSettingsStore(IFileSystem fileSystem, SettingsSchema schema) : IWorkspaceSettings
{
    /// <summary>
    /// The name of the file of a workspace's settings, at the root of the workspace.
    /// </summary>
    public const string FileName = "appsettings.json";

    /// <summary>
    /// The path of the file of a workspace's settings.
    /// </summary>
    /// <param name="workspaceFolder">The root folder of the workspace.</param>
    public static string PathIn(string workspaceFolder) => Path.Combine(workspaceFolder, FileName);

    /// <summary>
    /// The root folder of a workspace, as a command line or a setting names it (<see cref="SdkWorkspaceOptions.WorkspaceUri"/>).
    /// </summary>
    /// <param name="workspace">A <c>file:</c> URI, or a path, absolute or relative to the working directory.</param>
    /// <returns>
    /// The full path of the folder; <see langword="null"/> when <paramref name="workspace"/> is a URI of another scheme, which names no folder.
    /// </returns>
    public static string? FolderOf(string workspace)
    {
        if (Uri.TryCreate(workspace, UriKind.Absolute, out var uri))
        {
            return uri.IsFile ? uri.LocalPath : null;
        }

        return Path.GetFullPath(workspace);
    }

    /// <summary>
    /// Whether a workspace can set a setting: one a user can set (<see cref="SettingDescriptor.IsSettable"/>), and not a trust-sensitive one.
    /// </summary>
    /// <param name="setting">The setting.</param>
    public static bool CanSet(SettingDescriptor setting) => setting.IsSettable && !setting.IsTrustSensitive;

    /// <summary>
    /// The values a workspace sets, as its file says them now: the items of a list by their index, each under its setting's key.
    /// </summary>
    /// <param name="workspaceFolder">The root folder of the workspace.</param>
    /// <returns>
    /// The values, by their keys under <see cref="SettingsSchema.Root"/>, separated by <c>:</c> (<c>Analysis:DisabledDiagnostics:0</c>), as
    /// <see cref="ILiveSettings.Apply(IReadOnlyDictionary{string, string})"/> takes them; none when the file is not there or cannot be read.
    /// </returns>
    public IReadOnlyDictionary<string, string> Values(string workspaceFolder)
    {
        var read = ReadFile(fileSystem, PathIn(workspaceFolder), schema);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, value) in read.Values)
        {
            values[path[(SettingsSchema.Root.Length + 1)..]] = value;
        }

        foreach (var (path, items) in read.Lists)
        {
            for (var index = 0; index < items.Count; index++)
            {
                values[$"{path[(SettingsSchema.Root.Length + 1)..]}:{index.ToString(CultureInfo.InvariantCulture)}"] = items[index];
            }
        }

        return values;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Read(Uri workspaceRoot, string key)
    {
        if (!workspaceRoot.IsFile || !schema.TryFind(key.Trim(), out var setting) || !CanSet(setting))
        {
            return [];
        }

        return ReadFile(fileSystem, PathIn(workspaceRoot.LocalPath), schema).Lists.TryGetValue(SettingsSchema.ConfigurationPathOf(setting), out var items) ? items : [];
    }

    /// <inheritdoc/>
    public UserSettingChange Add(Uri workspaceRoot, string key, string value) => ChangeList(workspaceRoot, key, value, remove: false);

    /// <inheritdoc/>
    public UserSettingChange Remove(Uri workspaceRoot, string key, string value) => ChangeList(workspaceRoot, key, value, remove: true);

    private UserSettingChange ChangeList(Uri workspaceRoot, string key, string value, bool remove)
    {
        if (!schema.TryFind(key.Trim(), out var setting))
        {
            return new(UserSettingProblem.UnknownSetting);
        }

        if (!CanSet(setting))
        {
            return new(UserSettingProblem.NotSettable, setting);
        }

        if (!workspaceRoot.IsFile)
        {
            return new(UserSettingProblem.Unreadable, setting);
        }

        // the file is the user's store in its shape: a list is changed in the same way, and the rest of the file is kept as it is.
        var store = new UserSettingsStore(fileSystem, PathIn(workspaceRoot.LocalPath), schema);
        return remove ? store.Remove(key, value) : store.Add(key, value);
    }

    /// <summary>
    /// What a workspace's file sets: the values a workspace can set, by their paths in the configuration; and the items of each list, apart.
    /// </summary>
    /// <param name="Values">The values that are not lists, by their paths: <c>Configuration:Server:TraceLevel</c>.</param>
    /// <param name="Lists">The items of each list, by the path of the list, in their order and without duplicates.</param>
    internal sealed record class WorkspaceValues(IReadOnlyDictionary<string, string> Values, IReadOnlyDictionary<string, IReadOnlyList<string>> Lists);

    internal static WorkspaceValues ReadFile(IFileSystem fileSystem, string path, SettingsSchema schema)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lists = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!fileSystem.File.Exists(path))
        {
            return new(values, lists);
        }

        // a file written by hand that cannot be read sets nothing: it never keeps the platform from starting.
        IConfigurationRoot read;
        try
        {
            using var stream = fileSystem.File.OpenRead(path);
            read = new ConfigurationBuilder().AddJsonStream(stream).Build();
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return new(values, lists);
        }

        var items = new Dictionary<string, List<(int Index, string Item)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in read.AsEnumerable())
        {
            if (value is null || schema.Governing(key) is not { } setting || !CanSet(setting))
            {
                continue;
            }

            var settingPath = SettingsSchema.ConfigurationPathOf(setting);
            var isList = setting.Kind is SettingValueKind.TextList or SettingValueKind.DiagnosticCodeList;
            if (string.Equals(settingPath, key, StringComparison.OrdinalIgnoreCase))
            {
                if (!SettingsSchema.TryParse(setting, value, out var parsed))
                {
                    continue;
                }

                if (isList)
                {
                    // a list written as text, "i, j": its items, ahead of any the file gives one by one.
                    var list = ItemsOf(items, settingPath);
                    list.AddRange(((string[])parsed).Select(item => (-1, item)));
                }
                else
                {
                    values[key] = value;
                }

                continue;
            }

            // an item of a list is at an index right under the list; any text, or a code of a diagnostic that can be disabled.
            if (!isList || !int.TryParse(key[(settingPath.Length + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                continue;
            }

            var item = setting.Kind == SettingValueKind.DiagnosticCodeList ? SdkAnalysisOptions.DisablableCodeOf(value)?.ToString() : value;
            if (item is not null)
            {
                ItemsOf(items, settingPath).Add((index, item));
            }
        }

        foreach (var (settingPath, held) in items)
        {
            lists[settingPath] = [.. held.OrderBy(pair => pair.Index).Select(pair => pair.Item).Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        return new(values, lists);
    }

    private static List<(int Index, string Item)> ItemsOf(Dictionary<string, List<(int Index, string Item)>> items, string settingPath)
    {
        if (!items.TryGetValue(settingPath, out var list))
        {
            items[settingPath] = list = [];
        }

        return list;
    }
}

/// <summary>
/// Adds the settings of a workspace to a configuration (<see cref="AddWorkspaceSettings"/>).
/// </summary>
public static class WorkspaceSettingsConfiguration
{
    /// <summary>
    /// Adds the settings a workspace keeps for itself (<see cref="WorkspaceSettingsStore"/>) to a configuration, after the user's settings and before what
    /// the environment and the command line say: what a workspace sets takes the place of what its user set, and a process that is told otherwise when it is
    /// started is told so on purpose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list is the items of the layers below the workspace's, followed by the workspace's own that are not among them: a workspace adds to its user's list
    /// and takes nothing from it. Every index of the list is written again, so that no item of a layer below is left at an index the workspace's list does
    /// not reach. A value of any other kind takes the place of the one below it.
    /// </para>
    /// <para>
    /// Only the settings of <paramref name="schema"/> that a workspace can set (<see cref="WorkspaceSettingsStore.CanSet"/>), and values the setting can be,
    /// are read. A file that is not there sets nothing, and neither does one that cannot be read. A component reads the file when it starts, and not again.
    /// </para>
    /// </remarks>
    /// <param name="builder">The configuration.</param>
    /// <param name="workspaceFolder">The root folder of the workspace (<see cref="WorkspaceSettingsStore.FolderOf"/>).</param>
    /// <param name="schema">The settings of the application.</param>
    /// <param name="fileSystem">The file system the workspace is on; the machine's when <see langword="null"/>.</param>
    public static IConfigurationBuilder AddWorkspaceSettings(this IConfigurationBuilder builder, string workspaceFolder, SettingsSchema schema, IFileSystem? fileSystem = null)
    {
        var source = new WorkspaceSettingsConfigurationSource(fileSystem ?? new FileSystem(), WorkspaceSettingsStore.PathIn(workspaceFolder), schema);

        // above the user's settings, or else above the deployed appsettings.json, and below whatever comes after them.
        var below = builder.Sources
            .Select((candidate, index) => (Source: candidate, Index: index))
            .LastOrDefault(candidate => candidate.Source is UserSettingsConfiguration.UserSettingsConfigurationSource);
        if (below.Source is null)
        {
            below = builder.Sources
                .Select((candidate, index) => (Source: candidate, Index: index))
                .LastOrDefault(candidate => candidate.Source is JsonConfigurationSource { Path: { } json } && json.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase));
        }

        if (below.Source is null)
        {
            builder.Add(source);
        }
        else
        {
            builder.Sources.Insert(below.Index + 1, source);
        }

        return builder;
    }

    private sealed class WorkspaceSettingsConfigurationSource(IFileSystem fileSystem, string path, SettingsSchema schema) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            var below = builder.Sources.TakeWhile(source => !ReferenceEquals(source, this)).ToList();
            var properties = builder.Properties.ToList();
            return new WorkspaceSettingsConfigurationProvider(fileSystem, path, schema, () =>
            {
                var lower = new ConfigurationBuilder();
                foreach (var (key, value) in properties)
                {
                    lower.Properties[key] = value;
                }

                foreach (var source in below)
                {
                    lower.Add(source);
                }

                return lower.Build();
            });
        }
    }

    private sealed class WorkspaceSettingsConfigurationProvider(IFileSystem fileSystem, string path, SettingsSchema schema, Func<IConfigurationRoot> below)
        : ConfigurationProvider
    {
        public override void Load()
        {
            var read = WorkspaceSettingsStore.ReadFile(fileSystem, path, schema);
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in read.Values)
            {
                data[key] = value;
            }

            if (read.Lists.Count > 0)
            {
                // the lists of the layers below, as they are when the workspace's file is read: a plain overlay would put the workspace's items in the place
                // of theirs, index by index.
                var lower = below();
                try
                {
                    foreach (var (settingPath, items) in read.Lists)
                    {
                        var setting = schema.Governing(settingPath)!;
                        List<string> list = [.. ItemsBelow(lower, setting, settingPath)];
                        list.AddRange(items.Where(item => !list.Contains(item, StringComparer.OrdinalIgnoreCase)));
                        for (var index = 0; index < list.Count; index++)
                        {
                            data[$"{settingPath}:{index.ToString(CultureInfo.InvariantCulture)}"] = list[index];
                        }
                    }
                }
                finally
                {
                    (lower as IDisposable)?.Dispose();
                }
            }

            Data = data;
        }

        // the items of a list as the layers below say it: one by one, by their index; or as text, "i, j", when none is given one by one.
        private static IEnumerable<string> ItemsBelow(IConfiguration lower, SettingDescriptor setting, string settingPath)
        {
            var section = lower.GetSection(settingPath);
            var items = section.GetChildren()
                .Select(child => (Child: child, Index: int.TryParse(child.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index : -1))
                .Where(pair => pair.Index >= 0 && pair.Child.Value is not null)
                .OrderBy(pair => pair.Index)
                .Select(pair => pair.Child.Value!)
                .ToList();
            if (items.Count == 0 && section.Value is { Length: > 0 } text && SettingsSchema.TryParse(setting, text, out var parsed))
            {
                return (string[])parsed;
            }

            return items;
        }
    }
}
