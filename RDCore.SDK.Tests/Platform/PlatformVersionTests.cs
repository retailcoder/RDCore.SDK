using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// A platform version, and the rule a client and a platform built with two RDCore.SDK versions work together by.
/// </summary>
[TestClass]
public sealed class PlatformVersionTests
{
    [TestMethod]
    [DataRow("0.1.0", "0.1.0")]
    [DataRow("v0.2.0-alpha.3", "0.2.0-alpha.3")]
    [DataRow("V0.1.0-RC.1+build.5", "0.1.0-rc.1")]
    [DataRow(" 1.2.3 ", "1.2.3")]
    [DataRow("0.1.1-alpha+2062f9857560619aec056b37ac0181c84c0c6e6a", "0.1.1-alpha")]
    public void AVersion_IsReadInLowercase_WithoutItsVOrBuildMetadata(string text, string expected)
    {
        Assert.IsTrue(PlatformVersion.TryParse(text, out var version));
        Assert.AreEqual(expected, version.Text);
        Assert.AreEqual(expected, version.ToString());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("0.1")]
    [DataRow("0.1.0.0")]
    [DataRow("01.0.0")]
    [DataRow("0.1.0-")]
    [DataRow("0.1.0-alpha..1")]
    [DataRow("0.1.0-alpha.01")]
    [DataRow("0.1.0-al pha")]
    [DataRow("a.b.c")]
    public void ATextThatIsNotAVersion_IsNone(string? text)
    {
        Assert.IsFalse(PlatformVersion.TryParse(text, out var version));
        Assert.IsNull(version);
    }

    [TestMethod]
    public void ParsingATextThatIsNotAVersion_SaysWhichText()
    {
        var thrown = Assert.ThrowsExactly<FormatException>(() => PlatformVersion.Parse("0.1"));
        StringAssert.Contains(thrown.Message, "'0.1'");
    }

    // Semantic Versioning 2.0, §11.
    [TestMethod]
    public void Versions_AreOrderedAsSemanticVersioningOrdersThem()
    {
        string[] ordered = ["0.1.0-alpha", "0.1.0-alpha.1", "0.1.0-alpha.beta", "0.1.0-beta", "0.1.0-beta.2", "0.1.0-beta.11", "0.1.0-rc.1", "0.1.0", "0.1.1", "0.2.0", "1.0.0"];
        var versions = ordered.Select(PlatformVersion.Parse).ToArray();

        for (var i = 1; i < versions.Length; i++)
        {
            Assert.IsLessThan(0, versions[i - 1].CompareTo(versions[i]), $"{versions[i - 1]} < {versions[i]}");
            Assert.IsTrue(versions[i - 1] < versions[i]);
            Assert.IsTrue(versions[i] > versions[i - 1]);
        }

        CollectionAssert.AreEqual(versions, versions.Reverse().Order().ToArray());
    }

    [TestMethod]
    public void TwoTextsOfOneVersion_AreEqual_AndHashAlike()
    {
        var a = PlatformVersion.Parse("v0.1.0-RC.1+one");
        var b = PlatformVersion.Parse("0.1.0-rc.1+two");

        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.IsTrue(a <= b && a >= b);
    }

    [TestMethod]
    [DataRow("0.1.2-alpha", "0.1.1-alpha", true, DisplayName = "an add-in on 0.1.2-alpha and a platform on 0.1.1-alpha")]
    [DataRow("0.1.1-alpha", "0.1.0", true, DisplayName = "the same minor version before 1.0")]
    [DataRow("0.1.0", "0.1.9-rc.1", true, DisplayName = "prereleases and patches aside")]
    [DataRow("0.1.1-alpha", "0.2.0-alpha", false, DisplayName = "another minor version before 1.0")]
    [DataRow("0.2.0", "1.0.0", false, DisplayName = "0.x and 1.0")]
    [DataRow("1.0.0", "1.4.2", true, DisplayName = "the same major version from 1.0")]
    [DataRow("1.9.0", "2.0.0", false, DisplayName = "another major version from 1.0")]
    public void TwoSdkVersions_WorkTogether_ByTheirMajorAndMinorBefore1_AndTheirMajorFrom1(string a, string b, bool compatible)
    {
        Assert.AreEqual(compatible, PlatformVersion.Parse(a).IsCompatibleWith(PlatformVersion.Parse(b)));
        Assert.AreEqual(compatible, PlatformVersion.Parse(b).IsCompatibleWith(PlatformVersion.Parse(a)), "the rule is symmetric");
    }

    [TestMethod]
    [DataRow("0.1.1-alpha", "0.1.x")]
    [DataRow("0.12.0", "0.12.x")]
    [DataRow("1.4.2", "1.x")]
    public void TheCompatibleRange_IsWrittenAsAPersonReadsIt(string version, string range)
        => Assert.AreEqual(range, PlatformVersion.Parse(version).CompatibleRange);
}
