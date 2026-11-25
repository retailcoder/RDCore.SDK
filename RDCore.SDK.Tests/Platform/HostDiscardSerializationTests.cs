using System.Text.Json;
using RDCore.SDK.Platform.Components;

namespace RDCore.Tests.Platform;

/// <summary>
/// <see cref="HostDiscardParams"/> crosses the language-server → environment-host boundary as JSON: the project root it names says which session the
/// module leaves.
/// </summary>
[TestClass]
public sealed class HostDiscardSerializationTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static HostDiscardParams RoundTripped(HostDiscardParams original)
        => JsonSerializer.Deserialize<HostDiscardParams>(JsonSerializer.Serialize(original, Options), Options)!;

    [TestMethod]
    public void TheRootOfAnAnalyzedProject_IsReadBackAsItWasWritten()
    {
        var original = new HostDiscardParams
        {
            WorkspaceRoot = new Uri("file:///c:/ws/lib/"),
            ModuleUri = new Uri("file:///c:/ws/lib/#Helpers"),
            ModuleName = "Helpers",
        };

        Assert.AreEqual(original, RoundTripped(original));
    }

    // a discard that names no project is the runtime session's.
    [TestMethod]
    public void ADiscardThatNamesNoProject_IsReadBackWithNoRoot()
    {
        var read = JsonSerializer.Deserialize<HostDiscardParams>("""{"moduleUri":"file:///c:/ws/#Helpers","moduleName":"Helpers"}""", Options)!;

        Assert.IsNull(read.WorkspaceRoot);
        Assert.AreEqual("Helpers", read.ModuleName);
    }
}
