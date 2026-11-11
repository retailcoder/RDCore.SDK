using System.Text;
using RDCore.SDK;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// A released platform's <c>release.json</c>, as RDCore's release workflow writes it, and what a client chooses a platform by in it.
/// </summary>
[TestClass]
public sealed class PlatformReleaseManifestTests
{
    private string _root = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "RDCore", "release", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(_root, recursive: true);

    // the shape tools/release/New-ReleaseMetadata.ps1 writes, its list of files cut short.
    private static string Release(string sdk = """{ "package": "RDCore.SDK", "version": "0.1.1-alpha" }""") => $$"""
        {
          "schema": 1,
          "name": "RDCore",
          "version": "0.1.0-alpha",
          "commit": "560ed513a",
          "rid": "linux-x64",
          "deployment": "framework-dependent",
          "sdk": {{sdk}},
          "runtime": { "name": "Microsoft.NETCore.App", "minimumVersion": "10.0.0" },
          "entryPoints": { "hostService": "RDCore.CLI/rdc", "langService": "RDCore.LanguageServer/RDCore.LanguageServer" },
          "launch": { "languageServer": { "path": "RDCore.LanguageServer/RDCore.LanguageServer", "arguments": ["--pipe-name", "{pipe}"] } },
          "source": { "repository": "https://github.com/rubberduck-vba/RDCore", "tag": null, "commit": "560ed513a" },
          "hashAlgorithm": "sha256",
          "files": [ { "path": "rdcore.json", "size": 300, "sha256": "00" } ]
        }
        """;

    private static PlatformReleaseManifest Parse(string json) => PlatformReleaseManifest.Parse(Encoding.UTF8.GetBytes(json));

    [TestMethod]
    public void AReleaseManifest_SaysWhatWasReleased_ForWhichSystem_AndWithWhatItWasBuilt()
    {
        var release = Parse(Release());

        Assert.AreEqual(1, release.Schema);
        Assert.AreEqual("RDCore", release.Name);
        Assert.AreEqual("0.1.0-alpha", release.Version);
        Assert.AreEqual("560ed513a", release.Commit);
        Assert.AreEqual("linux-x64", release.RuntimeIdentifier);
        Assert.AreEqual("framework-dependent", release.Deployment);
        Assert.AreEqual(new PlatformReleasePackage { Package = RDCoreSdk.PackageId, Version = "0.1.1-alpha" }, release.Sdk);
        Assert.AreEqual("10.0.0", release.Runtime?.MinimumVersion);
        Assert.AreEqual(PlatformVersion.Parse("0.1.1-alpha"), release.SdkVersion);
    }

    [TestMethod]
    [DataRow("0.1.0", true)]
    [DataRow("0.1.2-local.abc1234", true)]
    [DataRow("0.2.0-alpha", false)]
    public void APlatform_IsBuiltWithACompatibleSdk_ByTheRuleOfPlatformVersion(string client, bool built)
        => Assert.AreEqual(built, Parse(Release()).IsBuiltWith(PlatformVersion.Parse(client)));

    [TestMethod]
    [DataRow("null", DisplayName = "no sdk")]
    [DataRow("""{ "package": "Other.SDK", "version": "0.1.1-alpha" }""", DisplayName = "another package")]
    [DataRow("""{ "package": "rdcore.sdk", "version": "0.1.1-alpha" }""", DisplayName = "a package id in another case")]
    [DataRow("""{ "package": "RDCore.SDK", "version": "0.1" }""", DisplayName = "a version that isn't one")]
    public void AManifestThatDoesNotSayWhichSdk_IsBuiltWithNone(string sdk)
    {
        var release = Parse(Release(sdk));

        Assert.IsNull(release.SdkVersion);
        Assert.IsFalse(release.IsBuiltWith(PlatformVersion.Parse("0.1.1-alpha")));
    }

    [TestMethod]
    public void AManifestWithOnlyAnSdk_IsReadAsWell()
        => Assert.AreEqual("0.1.1-alpha", Parse("""{ "sdk": { "package": "RDCore.SDK", "version": "0.1.1-alpha" } }""").SdkVersion?.Text);

    [TestMethod]
    [DataRow("{ not json")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("""{ "schema": "one" }""")]
    public void AFileThatIsNotAReleaseManifest_IsSaid(string json)
        => Assert.ThrowsExactly<InvalidDataException>(() => Parse(json));

    [TestMethod]
    public void AManifestLongerThanTheMost_IsSaid_BeforeItIsRead()
    {
        var thrown = Assert.ThrowsExactly<InvalidDataException>(() => PlatformReleaseManifest.Parse(new byte[PlatformReleaseManifest.MaxBytes + 1]));
        StringAssert.Contains(thrown.Message, PlatformReleaseManifest.MaxBytes.ToString(System.Globalization.CultureInfo.CurrentCulture));

        using (var file = File.Create(Path.Combine(_root, PlatformReleaseManifest.FileName)))
        {
            file.SetLength(PlatformReleaseManifest.MaxBytes + 1L);
        }

        Assert.ThrowsExactly<InvalidDataException>(() => PlatformReleaseManifest.Read(_root));
    }

    [TestMethod]
    public void TheManifestOfAPlatformRoot_IsReadFromIt()
    {
        File.WriteAllText(Path.Combine(_root, PlatformReleaseManifest.FileName), Release());

        Assert.AreEqual("linux-x64", PlatformReleaseManifest.Read(_root)?.RuntimeIdentifier);
    }

    [TestMethod]
    public void ARootWithNoReleaseManifest_AsADevelopmentBuildIs_HasNone()
        => Assert.IsNull(PlatformReleaseManifest.Read(_root));

    [TestMethod]
    public void ARootWithAFileThatIsNotOne_IsSaid_WithItsPath()
    {
        File.WriteAllText(Path.Combine(_root, PlatformReleaseManifest.FileName), "{ not json");

        var thrown = Assert.ThrowsExactly<InvalidDataException>(() => PlatformReleaseManifest.Read(_root));
        StringAssert.Contains(thrown.Message, Path.Combine(_root, PlatformReleaseManifest.FileName));
    }
}
