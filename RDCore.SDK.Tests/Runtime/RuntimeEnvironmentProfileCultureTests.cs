using System.Globalization;
using RDCore.SDK.Runtime;

namespace RDCore.Tests.Runtime;

[TestClass]
public sealed class RuntimeEnvironmentProfileCultureTests
{
    private static RuntimeEnvironmentProfile ProfileOf(int lcid) => new(Is64Bit: true, Lcid: lcid, AnsiCodePage: 1252, SupportsOptionCompareDatabase: false);

    [TestMethod]
    [DataRow(1033, "en-US")]
    [DataRow(1036, "fr-FR")]
    [DataRow(3084, "fr-CA")]
    public void Culture_IsTheCultureOfTheLcid(int lcid, string name)
    {
        var profile = ProfileOf(lcid);
        var culture = profile.Culture;

        Assert.AreEqual(name, culture.Name);
        Assert.AreSame(culture, profile.Culture);
    }

    [TestMethod]
    public void Culture_OfLcidZero_IsTheInvariantCulture()
        => Assert.AreSame(CultureInfo.InvariantCulture, ProfileOf(0).Culture);

    // a with copy shares what the record holds: the copy's culture is its own Lcid's, and reading it leaves the original's as it was.
    [TestMethod]
    public void Culture_OfACopyWithAnotherLcid_IsTheCopysLcids()
    {
        var english = ProfileOf(1033);
        Assert.AreEqual("en-US", english.Culture.Name);

        var french = english with { Lcid = 1036 };

        Assert.AreEqual("fr-FR", french.Culture.Name);
        Assert.AreEqual("en-US", english.Culture.Name);
        Assert.AreEqual("fr-FR", french.Culture.Name);
    }

    [TestMethod]
    public void Culture_OfACopyWithTheSameLcid_IsTheOriginals()
    {
        var english = ProfileOf(1033);
        var culture = english.Culture;

        var copy = english with { AnsiCodePage = 1250 };

        Assert.AreSame(culture, copy.Culture);
    }

    // what is held takes no part in the record's equality: a profile whose culture was read equals one whose culture was not.
    [TestMethod]
    public void Equality_IsTheSame_WhetherTheCultureWasReadOrNot()
    {
        var read = ProfileOf(1036);
        _ = read.Culture;
        var unread = ProfileOf(1036);

        Assert.AreEqual(unread, read);
        Assert.AreEqual(unread.GetHashCode(), read.GetHashCode());
    }

    [TestMethod]
    public void Equality_StillTellsLcidsApart()
    {
        var english = ProfileOf(1033);
        var french = ProfileOf(1036);
        _ = english.Culture;
        _ = french.Culture;

        Assert.AreNotEqual(english, french);
    }

    // nothing is held for an Lcid that names no culture: each read throws as CultureInfo.GetCultureInfo does.
    [TestMethod]
    public void Culture_OfAnLcidOfNoCulture_ThrowsOnEachRead()
    {
        var profile = ProfileOf(-1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => profile.Culture);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => profile.Culture);
    }
}
