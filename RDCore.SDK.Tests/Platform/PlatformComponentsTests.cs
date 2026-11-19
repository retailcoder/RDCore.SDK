using System.Reflection;
using RDCore.SDK;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// The version of a component of the platform, read from its assembly file without loading it, as every process of an installation reads it: the
/// environment host and the external host from the platform's folder, which they share, and a built program from its own.
/// </summary>
[TestClass]
public sealed class PlatformComponentsTests
{
    // assemblies of known informational versions, standing for the components: their files are copied under the components' names. They are of two
    // versions, so that one component is not read as another.
    private static readonly Assembly Runtime = typeof(PlatformComponentsTests).Assembly;
    private static readonly Assembly Forms = typeof(Assert).Assembly;

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "RDCore", "components", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void Place(string folder, Assembly assembly, string file) => File.Copy(assembly.Location, Path.Combine(folder, file));

    private static void AssertNotLoaded(string folder)
        => Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().Any(assembly => !assembly.IsDynamic
                && assembly.Location.StartsWith(folder, StringComparison.OrdinalIgnoreCase)),
            "no component's file was loaded");

    [TestMethod]
    public void TheEnvironmentHostAndTheExternalHost_ReadEachComponentsVersion_FromThePlatformsFolder()
    {
        var platform = NewFolder();
        try
        {
            Place(platform, Runtime, "RDCore.Runtime.dll");
            Place(platform, Forms, "RDCore.Forms.dll");

            Assert.AreEqual(AssemblyDisplayVersion.Get(Runtime), PlatformComponents.VersionOf(PlatformComponents.Platform, platform));
            Assert.AreEqual(AssemblyDisplayVersion.Get(Forms), PlatformComponents.VersionOf(PlatformComponents.Forms, platform));
            Assert.AreEqual(AssemblyDisplayVersion.Get(typeof(PlatformComponents).Assembly), PlatformComponents.VersionOf(PlatformComponents.Sdk, platform));
            AssertNotLoaded(platform);
        }
        finally
        {
            Directory.Delete(platform, recursive: true);
        }
    }

    [TestMethod]
    public void ABuiltProgramWithForms_ReadsEachComponentsVersion_FromItsOwnFolder()
    {
        var program = NewFolder();
        try
        {
            Place(program, Runtime, "RDCore.Runtime.dll");
            Place(program, Forms, "RDCore.Forms.dll");

            Assert.AreEqual(AssemblyDisplayVersion.Get(Runtime), PlatformComponents.VersionOf(PlatformComponents.Platform, program));
            Assert.AreEqual(AssemblyDisplayVersion.Get(Forms), PlatformComponents.VersionOf(PlatformComponents.Forms, program));
            AssertNotLoaded(program);
        }
        finally
        {
            Directory.Delete(program, recursive: true);
        }
    }

    // a program with no form is built without the forms package: RDCore.Forms is not part of what it runs on.
    [TestMethod]
    public void ABuiltProgramWithNoForms_HasNoVersionOfTheFormsPackage()
    {
        var program = NewFolder();
        try
        {
            Place(program, Runtime, "RDCore.Runtime.dll");

            Assert.AreEqual(AssemblyDisplayVersion.Get(Runtime), PlatformComponents.VersionOf(PlatformComponents.Platform, program));
            Assert.IsNull(PlatformComponents.VersionOf(PlatformComponents.Forms, program));
        }
        finally
        {
            Directory.Delete(program, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("rdcore", DisplayName = "RDCore, in lowercase")]
    [DataRow("RDCORE.FORMS", DisplayName = "RDCore.Forms, in uppercase")]
    public void AComponentsName_IsReadInAnyCase(string name)
    {
        var platform = NewFolder();
        try
        {
            Place(platform, Runtime, "RDCore.Runtime.dll");
            Place(platform, Forms, "RDCore.Forms.dll");

            Assert.IsNotNull(PlatformComponents.VersionOf(name, platform));
        }
        finally
        {
            Directory.Delete(platform, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("RDCore.Runtime", DisplayName = "an assembly of the platform, not a component")]
    [DataRow("", DisplayName = "an empty name")]
    [DataRow("Avalonia", DisplayName = "a library a component uses")]
    public void ANameThatIsNoComponent_HasNoVersion(string name)
        => Assert.IsNull(PlatformComponents.VersionOf(name));

    [TestMethod]
    public void AComponentsFileThatIsNoAssembly_HasNoVersion()
    {
        var platform = NewFolder();
        try
        {
            File.WriteAllText(Path.Combine(platform, "RDCore.Runtime.dll"), "not an assembly");

            Assert.IsNull(PlatformComponents.VersionOf(PlatformComponents.Platform, platform));
        }
        finally
        {
            Directory.Delete(platform, recursive: true);
        }
    }

    [TestMethod]
    public void TheSdksVersion_IsTheSplashs()
        => Assert.AreEqual(PlatformSplash.SdkVersion, PlatformComponents.VersionOf("RDCore.SDK"));

    [TestMethod]
    public void TheComponents_AreThePlatformTheSdkAndTheFormsPackage()
        => CollectionAssert.AreEqual(new[] { "RDCore", "RDCore.SDK", "RDCore.Forms" }, PlatformComponents.Names.ToArray());
}
