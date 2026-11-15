using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Semantics.Refactoring;
using RDCore.SDK.Server.Configuration;
using System.Globalization;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// The inlay hint settings: one for each kind of place a hint is shown, each on unless it is turned off, live, and described in both languages; and a hint as
/// a client is given it.
/// </summary>
[TestClass]
public sealed class InlayHintSettingsTests
{
    private static readonly string[] Names = ["Arguments", "BlockEnds", "NextVariables", "ImplicitModifiers", "ImplicitTypes"];

    private static IEnumerable<SettingDescriptor> Settings()
        => SettingsSchema.Platform.Settings.Where(setting => setting.Key.StartsWith("InlayHints.", StringComparison.Ordinal));

    [TestMethod]
    public void TheSchema_HasASettingForEachKindOfPlace_AndNothingElse()
    {
        CollectionAssert.AreEquivalent(Names, Settings().Select(setting => setting.Key["InlayHints.".Length..]).ToArray());
        CollectionAssert.AreEquivalent(Names, Enum.GetNames<InlayHintSite>(), "a setting for each kind");
    }

    [TestMethod]
    public void TheSettings_AreLiveSwitches_AndNoneIsTrustSensitive()
    {
        foreach (var setting in Settings())
        {
            Assert.AreEqual(SettingValueKind.Boolean, setting.Kind, setting.Key);
            Assert.AreEqual(SettingReach.Live, setting.Reach, setting.Key);
            Assert.IsFalse(setting.IsTrustSensitive, setting.Key);
        }
    }

    [TestMethod]
    [DataRow("en")]
    [DataRow("fr")]
    public void EverySetting_IsDescribed_InBothLanguages(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            foreach (var setting in Settings())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(setting.Description), $"{setting.Key} in {language}");
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [TestMethod]
    public void EveryKind_IsShownByDefault_AndEachSettingTurnsItsOwnOff()
    {
        Assert.IsTrue(Enum.GetValues<InlayHintSite>().All(new SdkInlayHintOptions().Shows));

        var off = new SdkInlayHintOptions { NextVariables = false };
        CollectionAssert.AreEqual(new[] { InlayHintSite.NextVariables }, Enum.GetValues<InlayHintSite>().Where(site => !off.Shows(site)).ToArray());
    }

    [TestMethod]
    [DataRow(InlayHintSite.Arguments, InlayHintKind.Parameter)]
    [DataRow(InlayHintSite.ImplicitTypes, InlayHintKind.Type)]
    public void AHintOfAnArgumentOrOfAType_IsOfItsKind(InlayHintSite site, InlayHintKind kind)
        => Assert.AreEqual(kind, new SourceInlayHint(new SourcePosition(0, 0), "[x]", site).ToInlayHint(new Position(0, 0)).Kind);

    [TestMethod]
    public void AnyOtherHint_IsOfNoKind_AndIsSentAsItIs_AtThePositionItIsGiven()
    {
        var hint = new SourceInlayHint(new SourcePosition(3, 7), "[DoSomething]", InlayHintSite.BlockEnds, PaddingLeft: true).ToInlayHint(new Position(2, 7));

        Assert.IsNull(hint.Kind);
        Assert.AreEqual(("[DoSomething]", new Position(2, 7), true, false), (hint.Label.String, hint.Position, hint.PaddingLeft == true, hint.PaddingRight == true));
    }
}
