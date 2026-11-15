using RDCore.SDK.Client;
using RDCore.SDK.Model.Source;
using System.Text.Json;

namespace RDCore.Tests.Platform;

/// <summary>
/// A <see cref="Uri"/> travels as it is spelled and comes back the same Uri on every platform: a file's Uri made of a rooted path is read back as the absolute
/// Uri it is, which off Windows the default reading makes a relative one of.
/// </summary>
[TestClass]
public sealed class RDCorePlatformJsonUriTests
{
    private static IEnumerable<object[]> Uris() =>
    [
        [new Uri(Path.Combine(Path.GetTempPath(), "rdcore-uri", "src", "Module1.bas"))],
        [new Uri("file:///c:/workspace/Module1.bas")],
        [new Uri("file:///home/ws/Module1.bas")],
        [new Uri("vba://project/Module1/count")],
        [new Uri("src/Module1.bas", UriKind.Relative)],
    ];

    [TestMethod]
    [DynamicData(nameof(Uris))]
    public void AUri_IsWrittenAsItIsSpelled_AndReadBackTheSame(Uri uri)
    {
        var json = JsonSerializer.Serialize(uri, RDCorePlatformJson.Options);
        var read = JsonSerializer.Deserialize<Uri>(json, RDCorePlatformJson.Options);

        Assert.AreEqual(JsonSerializer.Serialize(uri.OriginalString), json);
        Assert.AreEqual(uri, read);
        Assert.AreEqual(uri.IsAbsoluteUri, read!.IsAbsoluteUri);
    }

    [TestMethod]
    public void AFileUriOfARootedPath_InALocation_IsReadBackAbsolute()
    {
        var location = new SourceLocation(new Uri(Path.Combine(Path.GetTempPath(), "rdcore-uri", "src", "Module1.bas")), new SourceRange(1, 8, 1, 13));

        var read = JsonSerializer.Deserialize<SourceLocation>(JsonSerializer.Serialize(location, RDCorePlatformJson.Options), RDCorePlatformJson.Options);

        Assert.AreEqual(location, read);
        Assert.IsTrue(read!.Uri.IsAbsoluteUri);
        Assert.IsTrue(read.Uri.IsFile);
    }

    [TestMethod]
    public void ANullUri_IsWrittenNull_AndReadBackNull()
    {
        Assert.AreEqual("null", JsonSerializer.Serialize<Uri?>(null, RDCorePlatformJson.Options));
        Assert.IsNull(JsonSerializer.Deserialize<Uri?>("null", RDCorePlatformJson.Options));
    }
}
