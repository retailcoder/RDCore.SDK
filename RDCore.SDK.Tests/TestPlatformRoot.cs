using RDCore.SDK.Platform;

namespace RDCore.Tests;

// the platform root of the test process. A process is given its root and never derives one, so the assembly names one: a folder of its own, which no
// other run shares. A class that names a root of its own puts this one back when it is done.
[TestClass]
public sealed class TestPlatformRoot
{
    private static string _folder = null!;

    public static string Path { get; private set; } = null!;

    [AssemblyInitialize]
    public static void Name(TestContext context)
    {
        _folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RDCore", "sdk-tests", Guid.NewGuid().ToString("N"));
        Path = System.IO.Path.Combine(_folder, "platform");
        Directory.CreateDirectory(Path);
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, Path);
    }

    [AssemblyCleanup]
    public static void Delete()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // a child a test started may still hold a file there; the folder is the temp folder's to clear.
        }
    }
}
