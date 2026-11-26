using RDCore.SDK.Server.Configuration;
using System.Globalization;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// The descriptions of the platform's settings: every setting a client can change is described in each language the platform describes its settings in, and a
/// description in another language than the first is a translation, not the first language's again.
/// </summary>
[TestClass]
public sealed class SettingsSchemaDescriptionsTests
{
    public static IEnumerable<object[]> Languages => SettingsSchema.Languages.Select(language => new object[] { language });

    public static IEnumerable<object[]> Translations => SettingsSchema.Languages.Skip(1).Select(language => new object[] { language });

    // the description of each setting a client can change, by its key, in a language.
    private static Dictionary<string, string?> DescriptionsIn(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            return SettingsSchema.Platform.Settings.Where(setting => setting.IsSettable).ToDictionary(setting => setting.Key, setting => setting.Description);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [TestMethod]
    [DynamicData(nameof(Languages))]
    public void EverySettingAClientCanChange_IsDescribed(string language)
    {
        var descriptions = DescriptionsIn(language);
        Assert.IsNotEmpty(descriptions, "the platform has settings a client can change");

        var undescribed = descriptions.Where(each => string.IsNullOrWhiteSpace(each.Value)).Select(each => each.Key).ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), undescribed, $"not described in {language}: {string.Join(", ", undescribed)}");
    }

    // a key the translation has no entry of is described in the first language, which the test above takes for a description.
    [TestMethod]
    [DynamicData(nameof(Translations))]
    public void ATranslation_IsNotTheFirstLanguagesDescription(string language)
    {
        var first = DescriptionsIn(SettingsSchema.Languages[0]);
        var translated = DescriptionsIn(language);
        Assert.IsNotEmpty(first, "the platform has settings a client can change");

        var untranslated = first.Keys.Where(key => first[key] is not null && first[key] == translated[key]).ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), untranslated, $"not translated to {language}: {string.Join(", ", untranslated)}");
    }
}
