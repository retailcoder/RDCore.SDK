using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Extensibility.Annotations;
using RDCore.SDK.Platform;
using RDCore.SDK.Server.Configuration;
using System.Globalization;
using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Text.Json;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// An extension is described (<c>rdc describe-ext</c>) from inside its own folder: its executable is hashed, and its assembly, the <c>.dll</c> of the same name
/// beside it, is reflected. The executable is <c>My.Extension.exe</c> on Windows and <c>My.Extension</c> elsewhere; the assembly is <c>My.Extension.dll</c> on
/// both.
/// </summary>
// mutates the RDCORE_PLATFORM_ROOT process environment variable and the working directory.
[TestClass]
[DoNotParallelize]
public sealed class ExtensionsClientDescribeTests
{
    private const string Title = "My.Extension";

    private string _root = null!;
    private string _folder = null!;
    private string _workingDirectory = null!;

    [TestInitialize]
    public void CreateExtensionFolder()
    {
        _root = Path.Combine(Path.GetTempPath(), "RDCore", "describe", Guid.NewGuid().ToString("N"));
        _folder = Path.Combine(_root, "Extensions", Title);
        Directory.CreateDirectory(_folder);
        // any managed assembly will do as the extension's: this one.
        File.Copy(typeof(ExtensionsClientDescribeTests).Assembly.Location, Path.Combine(_folder, $"{Title}.dll"));
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, _root);
        _workingDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_folder);
    }

    [TestCleanup]
    public void DeleteExtensionFolder()
    {
        Directory.SetCurrentDirectory(_workingDirectory);
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, null);
        Directory.Delete(_root, recursive: true);
    }

    private static ExtensionsClient CreateSut() => new(
        Options.Create(new SdkAppOptions()),
        Substitute.For<IExtensionManifestValidationService>(),
        new FileSystem(),
        new PlatformEnvironment(new FileSystem()),
        Substitute.For<ILogger<ExtensionsClient>>());

    [TestMethod]
    [DataRow("My.Extension.exe", DisplayName = "an executable named as on Windows")]
    [DataRow("My.Extension", DisplayName = "an executable named as elsewhere")]
    public void AnExtension_IsDescribedFromTheAssemblyBesideItsExecutable(string executable)
    {
        // the apphost's bytes are only hashed, never run.
        byte[] apphost = [0x7f, 0x45, 0x4c, 0x46, 1, 2, 3];
        File.WriteAllBytes(Path.Combine(_folder, executable), apphost);

        var info = CreateSut().Describe(executable, "described");

        Assert.IsNotNull(info);
        Assert.AreEqual(executable, info.Name);
        Assert.AreEqual(Title, info.Title);
        Assert.AreEqual(Convert.ToBase64String(SHA512.HashData(apphost)), info.FileHash);
        Assert.AreEqual(typeof(ExtensionsClientDescribeTests).Assembly.GetName().Version, info.Version);
    }

    // this test assembly, the extension's here, references the SDK it runs with: its manifest says an SDK the platform can start it with.
    [TestMethod]
    public void AnExtensionsSdkVersion_IsTheSdkItsAssemblyReferences()
    {
        File.WriteAllBytes(Path.Combine(_folder, $"{Title}.exe"), [1, 2, 3]);
        var referenced = typeof(ExtensionInfo).Assembly.GetName().Version!;

        var info = CreateSut().Describe($"{Title}.exe", "described");

        Assert.IsNotNull(info?.SdkVersion);
        Assert.AreEqual(new Version(referenced.Major, referenced.Minor, referenced.Build), info.SdkVersion.Core);
        var platform = ExtensionManifestValidationService.SdkVersion;
        Assert.IsTrue(info.SdkVersion.IsCompatibleWith(platform), $"{info.SdkVersion.Text} works with {platform.Text}");
    }

    // this test assembly declares one annotation, Fixture(Label), described in English, and in French without its parameter (AnnotationFixtures).
    // The language server reads them back from the manifest file without starting the extension.
    [TestMethod]
    public void AnExtensionsAnnotations_AreWrittenIntoItsManifest_DescribedInEachLanguage()
    {
        File.WriteAllBytes(Path.Combine(_folder, $"{Title}.exe"), [1, 2, 3]);

        var described = CreateSut().Describe($"{Title}.exe", "described");
        Assert.IsNotNull(described);
        var info = JsonSerializer.Deserialize<ExtensionInfo>(JsonSerializer.Serialize(described))!;

        var fixture = Assert.ContainsSingle(info.Annotations);
        Assert.AreEqual("Fixture", fixture.Name);
        Assert.AreEqual(AnnotationTargets.Member, fixture.Targets);
        Assert.AreEqual("Label", Assert.ContainsSingle(fixture.Parameters).Name);
        Assert.AreEqual("Marks a fixture.", info.AnnotationDescriptions["en"]["Fixture"]);
        Assert.AreEqual("The fixture's label.", info.AnnotationDescriptions["en"]["Fixture_Label"]);
        Assert.AreEqual("Marque un montage.", info.AnnotationDescriptions["fr"]["Fixture"]);
        Assert.AreEqual("Marque un montage.", new AnnotationCatalogue([info.AnnotationSet]).DescriptionOf(fixture, CultureInfo.GetCultureInfo("fr-CA")));
    }

    // the manifest describe writes is the one an extension package carries: installing reads its runtime identifier from it.
    [TestMethod]
    [DataRow("linux-arm64", DisplayName = "published for a runtime")]
    [DataRow(null, DisplayName = "published for none")]
    public void ADescribedExtensionsRuntimeIdentifier_IsTheOneItsPackageIsInstalledBy(string? runtimeIdentifier)
    {
        File.WriteAllBytes(Path.Combine(_folder, $"{Title}.exe"), [1, 2, 3]);

        var info = CreateSut().Describe($"{Title}.exe", "described", runtimeIdentifier);

        Assert.IsNotNull(info);
        Assert.AreEqual(runtimeIdentifier, info.RuntimeIdentifier);
        var packaged = ExtensionPackageManifest.Parse(JsonSerializer.SerializeToUtf8Bytes(info));
        Assert.AreEqual(runtimeIdentifier, packaged.RuntimeIdentifier);
        Assert.AreEqual(Title, packaged.Title);
    }
}
