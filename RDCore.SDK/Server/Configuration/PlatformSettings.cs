using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace RDCore.SDK.Server.Configuration;

/// <summary>
/// Adds the platform's own settings file, <c>platform-settings.json</c> at the platform root (<see cref="Platform.IPlatformEnvironment.SettingsPath"/>), to
/// the configuration of a platform application: what every component of one platform is deployed with.
/// </summary>
/// <remarks>
/// <para>
/// The file has the shape of an application's <c>appsettings.json</c>: its settings are under <see cref="SettingsSchema.Root"/>, at the same paths. It is read
/// twice, at two places of the configuration:
/// </para>
/// <list type="bullet">
/// <item><description>first, below every other source: its settings are the defaults every component shares, which a component's own
/// <c>appsettings.json</c>, a user's settings, a workspace's, the environment and the command line set otherwise;</description></item>
/// <item><description>last, above every other source: a setting only the platform sets (<see cref="SettingReach.Platform"/>) is the file's, or its default
/// when the file does not say it, whatever any other source says of it.</description></item>
/// </list>
/// <para>
/// A file that is not there sets nothing, and neither does one that cannot be read, such as a file that is not valid JSON: every setting only the platform
/// sets is then its default. A process started from a build folder has no platform root, and so no file. A component reads the file when it starts, and
/// not again: a change takes effect the next time each component starts.
/// </para>
/// </remarks>
public static class PlatformSettingsConfiguration
{
    /// <summary>
    /// Adds the platform's settings file to a configuration: below its other sources, and above them for the settings only the platform sets.
    /// </summary>
    /// <remarks>
    /// Add it after the sources whose values it is to override: a source added to the end of the configuration after it is above it, for the settings only
    /// the platform sets as well. The user's settings (<see cref="UserSettingsConfiguration.AddUserSettings"/>), wherever they are
    /// added, never set a setting only the platform sets (<see cref="SettingDescriptor.IsSettable"/>).
    /// </remarks>
    /// <param name="builder">The configuration.</param>
    /// <param name="path">The path of the file (<see cref="Platform.IPlatformEnvironment.SettingsPath"/>).</param>
    /// <param name="schema">The settings of the application.</param>
    public static IConfigurationBuilder AddPlatformSettings(this IConfigurationBuilder builder, string path, SettingsSchema schema)
    {
        var defaults = new JsonConfigurationSource
        {
            Path = path,
            Optional = true,
            ReloadOnChange = false,
            OnLoadException = context => context.Ignore = true,
        };
        defaults.ResolveFileProvider();

        // below the application's own appsettings.json, the first source a value of a setting is read from.
        var deployed = builder.Sources
            .Select((candidate, index) => (Source: candidate, Index: index))
            .FirstOrDefault(candidate => candidate.Source is JsonConfigurationSource { Path: { } json }
                && json.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase));
        builder.Sources.Insert(deployed.Source is null ? 0 : deployed.Index, defaults);

        var locked = new PlatformSettingsLockSource(schema)
        {
            Path = path,
            Optional = true,
            ReloadOnChange = false,
            OnLoadException = context => context.Ignore = true,
        };
        locked.ResolveFileProvider();
        builder.Add(locked);

        return builder;
    }

    private sealed class PlatformSettingsLockSource(SettingsSchema schema) : JsonConfigurationSource
    {
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new PlatformSettingsLockProvider(this, schema);
        }
    }

    // answers for every path of a setting only the platform sets, and for nothing else: the file's value, or else the setting's default. A configuration that
    // answers no value at all binds the setting to none, which is not its default.
    private sealed class PlatformSettingsLockProvider(JsonConfigurationSource source, SettingsSchema schema) : JsonConfigurationProvider(source)
    {
        private readonly Dictionary<string, string?> _defaults = schema.Settings
            .Where(setting => setting.Reach == SettingReach.Platform)
            .ToDictionary(SettingsSchema.ConfigurationPathOf, schema.DefaultTextOf, StringComparer.OrdinalIgnoreCase);

        public override void Load(Stream stream)
        {
            base.Load(stream);
            foreach (var key in Data.Keys.Where(key => !IsLocked(key)).ToList())
            {
                Data.Remove(key);
            }
        }

        public override bool TryGet(string key, out string? value)
        {
            if (!IsLocked(key))
            {
                value = null;
                return false;
            }

            if (!Data.TryGetValue(key, out value))
            {
                _defaults.TryGetValue(key, out value);
            }

            return true;
        }

        // the items of a list only the platform sets are the file's: an item another source adds is not one of them.
        public override IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath)
            => base.GetChildKeys(parentPath is not null && IsLocked(parentPath) ? [] : earlierKeys, parentPath);

        private bool IsLocked(string path)
            => _defaults.Keys.Any(locked => path.Equals(locked, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(locked + ConfigurationPath.KeyDelimiter, StringComparison.OrdinalIgnoreCase));
    }
}
