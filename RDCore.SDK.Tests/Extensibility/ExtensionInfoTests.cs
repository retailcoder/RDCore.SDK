using System.Text.Json;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Extensibility.Annotations;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The extension manifest file (<c>extension.manifest.json</c>) is what <c>rdc describe-ext</c> writes, an extension package carries, and the language server
/// reads at platform assembly: every member is written under its own name, in one shape on both sides.
/// </summary>
[TestClass]
public sealed class ExtensionInfoTests
{
    private static readonly ExtensionInfo Manifest = new()
    {
        Name = "Ext.exe",
        Title = "Ext",
        Version = new Version(1, 2, 3),
        Publisher = "Publisher",
        PublisherWebUrl = "https://example.com",
        Description = "An extension.",
        FileHash = "aGFzaA==",
        Capabilities = [new PlatformExtensionServerCapability("Diagnostics")],
        DiagnosticPrefix = "EXT",
        IsCoreExtension = true,
        SdkVersion = PlatformVersion.Parse("0.2.1-alpha"),
        RuntimeIdentifier = "linux-x64",
        Annotations =
        [
            new AnnotationDeclaration
            {
                Name = "Fixture",
                Targets = AnnotationTargets.Member,
                Parameters = [new AnnotationParameter { Name = "Label", Takes = AnnotationArgumentKind.Text }],
            },
        ],
        AnnotationDescriptions = new()
        {
            ["en"] = new() { ["Fixture"] = "Marks a fixture.", ["Fixture_Label"] = "The label." },
            ["fr"] = new() { ["Fixture"] = "Marque un montage." },
        },
    };

    // IsCoreExtension is the platform's to say, and is never written. The annotation's JSON is the declaration's own (AnnotationDeclarationTests).
    // one line of JSON, written here on several.
    private static readonly string Golden = """
        {"Name":"Ext.exe","Title":"Ext","Id":"Ext","Version":"1.2.3","Publisher":"Publisher","PublisherWebUrl":"https://example.com",
        "Description":"An extension.","FileHash":"aGFzaA==","Capabilities":[{"Name":"Diagnostics","IsSupported":true}],"DiagnosticPrefix":"EXT",
        "SdkVersion":"0.2.1-alpha","RuntimeIdentifier":"linux-x64","Settings":[],"SettingDescriptions":{},
        "Annotations":[{"Name":"Fixture","Aliases":[],"Targets":"Member","ModuleKinds":"All","Repeats":false,
        "Parameters":[{"Name":"Label","Takes":"Text","IsOptional":false,"Repeats":false}],"IsDeprecated":false,"ReplacedBy":null}],
        "AnnotationDescriptions":{"en":{"Fixture":"Marks a fixture.","Fixture_Label":"The label."},"fr":{"Fixture":"Marque un montage."}}}
        """.ReplaceLineEndings(string.Empty);

    [TestMethod]
    public void AManifest_IsWrittenAsItsGolden()
        => Assert.AreEqual(Golden, JsonSerializer.Serialize(Manifest));

    [TestMethod]
    public void TheGolden_IsReadBackAsTheManifest()
    {
        var read = JsonSerializer.Deserialize<ExtensionInfo>(Golden)!;

        Assert.AreEqual(Golden, JsonSerializer.Serialize(read));
        Assert.IsFalse(read.IsCoreExtension);
        Assert.AreEqual("0.2.1-alpha", read.SdkVersion?.Text);
        Assert.AreEqual("linux-x64", read.RuntimeIdentifier);
        Assert.AreEqual("Label", Assert.ContainsSingle(Assert.ContainsSingle(read.Annotations).Parameters).Name);
        Assert.AreEqual("Marque un montage.", read.AnnotationDescriptions["fr"]["Fixture"]);
    }
}
