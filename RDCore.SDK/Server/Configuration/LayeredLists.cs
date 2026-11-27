using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace RDCore.SDK.Server.Configuration;

// a list a layer of settings states is the items of the layers below it, followed by its own that are not among them: a configuration merges a list index by
// index, which would put the layer's items in the place of theirs. The user's settings and a workspace's are such layers.
internal static class LayeredLists
{
    // the sources of a configuration that come before one of them, as a configuration of their own, built when it is read.
    public static Func<IConfigurationRoot> Below(IConfigurationBuilder builder, IConfigurationSource layer)
    {
        var below = builder.Sources.TakeWhile(source => !ReferenceEquals(source, layer)).ToList();
        var properties = builder.Properties.ToList();
        return () =>
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
        };
    }

    // writes each list of a layer into its data: every index of the list is written again, so that no item of a layer below is left at an index the layer's
    // own list does not reach.
    public static void Write(IDictionary<string, string?> data, IReadOnlyDictionary<string, IReadOnlyList<string>> lists, SettingsSchema schema,
        Func<IConfigurationRoot> below)
    {
        if (lists.Count == 0)
        {
            return;
        }

        var lower = below();
        try
        {
            foreach (var (settingPath, items) in lists)
            {
                var setting = schema.Governing(settingPath)!;
                List<string> list = [.. ItemsOf(lower, setting, settingPath)];
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

    // the items of a list as a configuration says it: one by one, by their index; or as text, "i, j", when none is given one by one.
    public static IEnumerable<string> ItemsOf(IConfiguration configuration, SettingDescriptor setting, string settingPath)
    {
        var section = configuration.GetSection(settingPath);
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
