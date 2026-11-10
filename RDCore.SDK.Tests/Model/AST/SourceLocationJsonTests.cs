using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.Tests.Model.AST;

/// <summary>
/// A location's <see cref="Uri"/> crosses the wire as it was spelled, a path or a URI, and is read back as the same one on every platform: off Windows, a
/// rooted path is the file it names, not a relative URI.
/// </summary>
[TestClass]
public sealed class SourceLocationJsonTests
{
    private static readonly SourceRange Range = new(1, 2, 3, 4);

    private static Uri UriOf(string kind) => kind switch
    {
        "rooted path" => new Uri(Path.Combine(Path.GetTempPath(), "ws", "Module1.bas")),
        "file URI" => new Uri("file:///c:/ws/Module1.bas"),
        "relative" => new Uri("Module1.bas", UriKind.Relative),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [TestMethod]
    [DataRow("rooted path")]
    [DataRow("file URI")]
    [DataRow("relative")]
    public void ALocation_IsReadBack_WithTheUriItWasWrittenWith(string kind)
    {
        var written = new SourceLocation(UriOf(kind), Range);

        var read = PlatformJson.Deserialize<SourceLocation>(PlatformJson.Serialize(written));

        Assert.AreEqual(written.Uri.IsAbsoluteUri, read.Uri.IsAbsoluteUri, read.Uri.OriginalString);
        Assert.AreEqual(written.Uri, read.Uri);
        Assert.AreEqual(written.Range, read.Range);
        if (written.Uri.IsAbsoluteUri)
        {
            Assert.AreEqual(written.Uri.LocalPath, read.Uri.LocalPath);
        }
    }
}
