using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Platform;
using RDCore.SDK.Server.Configuration;
using System.IO.Abstractions;
using System.Security.Cryptography;

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
}
