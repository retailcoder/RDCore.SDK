using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NSubstitute;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Extensibility.Annotations;
using RDCore.SDK.Platform;
using RDCore.SDK.Server.Configuration;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// An extension manifest is valid when the extension it describes was built against an RDCore.SDK compatible with the one the platform runs
/// with: the same major and minor versions before 1.0, the same major version from 1.0. The manifest writes that version as a platform version.
/// Its annotations are valid when no two share a name or an alias, each one's parameters are in order, and their descriptions describe only them.
/// </summary>
[TestClass]
public sealed class ExtensionManifestValidationServiceTests
{
    private const string Title = "RDCore.Diagnostics";
    private static readonly byte[] Executable = [1, 2, 3];
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateExtensionsFolder()
    {
        _root = Path.Combine(Path.GetTempPath(), "RDCore", $"validation-{Guid.NewGuid():N}");
        var extension = Directory.CreateDirectory(Path.Combine(_root, "Extensions", Title));
        File.WriteAllBytes(Path.Combine(extension.FullName, $"{Title}.exe"), Executable);
    }

    [TestCleanup]
    public void DeleteExtensionsFolder()
        => Directory.Delete(_root, recursive: true);

    private ExtensionManifestValidationService Sut()
    {
        var environment = Substitute.For<IPlatformEnvironment>();
        environment.Resolve("Extensions").Returns(Path.Combine(_root, "Extensions"));
        return new ExtensionManifestValidationService(Options.Create(new SdkAppOptions()), new System.IO.Abstractions.FileSystem(), environment);
    }

    private static ExtensionInfo Manifest(PlatformVersion? sdk) => new()
    {
        Name = $"{Title}.exe",
        Title = Title,
        Version = new Version(1, 0),
        Publisher = "",
        PublisherWebUrl = "",
        Description = "",
        FileHash = Convert.ToBase64String(SHA512.HashData(Executable)),
        Capabilities = [],
        SdkVersion = sdk,
    };

    private static PlatformVersion Sdk => ExtensionManifestValidationService.SdkVersion;

    // an extension references RDCore.SDK, whose version the Protocols package's need not be: RDCore.SDK 0.2.1 runs on Protocols 0.2.0.
    [TestMethod]
    public void ThePlatformsSdk_IsRDCoreSdksOwnVersion()
    {
        var sdk = typeof(ExtensionInfo).Assembly.GetName().Version!;

        Assert.AreEqual(new Version(sdk.Major, sdk.Minor, sdk.Build), Sdk.Core);
    }

    [TestMethod]
    public void AnExtensionBuiltAgainstThePlatformsSdk_IsValid()
        => Assert.AreEqual(ExtensionValidationFlags.NoFlags, Sut().Validate(Manifest(Sdk)));

    // a later patch, as a release, works with the platform's SDK, whatever its own patch and prerelease.
    [TestMethod]
    public void AnExtensionBuiltAgainstAnotherPatchOfThePlatformsSdk_IsValid()
        => Assert.AreEqual(ExtensionValidationFlags.NoFlags,
            Sut().Validate(Manifest(PlatformVersion.Parse($"{Sdk.Core.Major}.{Sdk.Core.Minor}.{Sdk.Core.Build + 1}"))));

    // another major version is incompatible before 1.0 and after it.
    [TestMethod]
    public void AnExtensionBuiltAgainstAnotherMajorVersionOfTheSdk_IsAnIncompatibleSdk()
        => Assert.AreEqual(ExtensionValidationFlags.IncompatibleSdk,
            Sut().Validate(Manifest(PlatformVersion.Parse($"{Sdk.Core.Major + 1}.{Sdk.Core.Minor}.{Sdk.Core.Build}"))));

    [TestMethod]
    public void AManifestThatDoesNotSayWhichSdk_IsAnIncompatibleSdk()
        => Assert.AreEqual(ExtensionValidationFlags.IncompatibleSdk, Sut().Validate(Manifest(null)));

    private static string ManifestJson(string sdkVersion) =>
        $$"""
        {"Name":"x.exe","Title":"x","Version":"1.0","Publisher":"","PublisherWebUrl":"","Description":"","FileHash":"","Capabilities":[]{{sdkVersion}}}
        """;

    [TestMethod]
    [DataRow(",\"SdkVersion\":\"0.2.1-alpha\"", "0.2.1-alpha", DisplayName = "a platform version")]
    [DataRow(",\"SdkVersion\":\"0.2.1.0\"", "0.2.1", DisplayName = "the four-part version manifests wrote before")]
    [DataRow(",\"SdkVersion\":\"not a version\"", null, DisplayName = "not a version")]
    [DataRow(",\"SdkVersion\":null", null, DisplayName = "null")]
    [DataRow("", null, DisplayName = "no SdkVersion")]
    public void AManifestsSdkVersion_IsReadAsAPlatformVersion(string property, string? expected)
        => Assert.AreEqual(expected, JsonSerializer.Deserialize<ExtensionInfo>(ManifestJson(property))!.SdkVersion?.Text);

    [TestMethod]
    public void AManifestsSdkVersion_IsWrittenAsAPlatformVersion()
    {
        var json = JsonSerializer.Serialize(Manifest(PlatformVersion.Parse("0.2.1-alpha")));

        StringAssert.Contains(json, "\"SdkVersion\":\"0.2.1-alpha\"");
    }

    private static AnnotationParameter Parameter(string name, bool optional = false, bool repeats = false)
        => new() { Name = name, Takes = AnnotationArgumentKind.Text, IsOptional = optional, Repeats = repeats };

    private static AnnotationDeclaration Annotation(string name, string[]? aliases = null, params AnnotationParameter[] parameters)
        => new() { Name = name, Aliases = aliases ?? [], Targets = AnnotationTargets.Member, Parameters = parameters };

    // each language describes what it describes of the annotations and their parameters, and nothing else.
    private static ExtensionInfo Annotated(AnnotationDeclaration[] annotations, params (string Language, string Key)[] described)
        => Manifest(Sdk) with
        {
            Annotations = annotations,
            AnnotationDescriptions = described.GroupBy(text => text.Language)
                .ToDictionary(language => language.Key, language => language.ToDictionary(text => text.Key, text => $"{text.Key} ({language.Key})")),
        };

    [TestMethod]
    public void AManifestsAnnotations_DescribedByTheirKeys_AreValid()
        => Assert.AreEqual(ExtensionValidationFlags.NoFlags, Sut().Validate(Annotated(
            [Annotation("Description", ["MemberDescription"], Parameter("Text")), Annotation("Folder", null, Parameter("Path", optional: true))],
            ("en", "Description"), ("en", "Description_Text"), ("fr", "Description"), ("en", "Folder_Path"))));

    [TestMethod]
    [DataRow("two annotations of one name")]
    [DataRow("an annotation named as another's alias, in another case")]
    [DataRow("a required parameter after an optional one")]
    [DataRow("a repeated parameter before the last")]
    [DataRow("a description of an annotation none declares")]
    [DataRow("a description of a parameter its annotation does not declare")]
    public void AManifestsAnnotationsThatDisagree_AreInvalidAnnotations(string disagreement)
    {
        var manifest = disagreement switch
        {
            "two annotations of one name" => Annotated([Annotation("Folder"), Annotation("Folder")]),
            "an annotation named as another's alias, in another case"
                => Annotated([Annotation("Description", ["MemberDescription"]), Annotation("memberdescription")]),
            "a required parameter after an optional one"
                => Annotated([Annotation("Description", null, Parameter("Text", optional: true), Parameter("Culture"))]),
            "a repeated parameter before the last"
                => Annotated([Annotation("Description", null, Parameter("Text", repeats: true), Parameter("Culture", optional: true))]),
            "a description of an annotation none declares" => Annotated([Annotation("Folder")], ("fr", "Folder"), ("fr", "Description")),
            "a description of a parameter its annotation does not declare"
                => Annotated([Annotation("Folder", null, Parameter("Path"))], ("en", "Folder_Path"), ("en", "Folder_Name")),
            _ => throw new ArgumentOutOfRangeException(nameof(disagreement)),
        };

        Assert.AreEqual(ExtensionValidationFlags.InvalidAnnotations, Sut().Validate(manifest));
    }
}
