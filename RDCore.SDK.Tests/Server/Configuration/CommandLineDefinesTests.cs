using Microsoft.Extensions.Configuration;
using RDCore.SDK.Server.Configuration;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// The conditional-compilation constants of the command line (<c>--define</c>): a setting of the configuration, and constants of the project as every
/// component composes it, over the project's own.
/// </summary>
[TestClass]
public sealed class CommandLineDefinesTests
{
    private static SdkWorkspaceOptions Bound(params string[] arguments)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(SdkCommandLine.Parse(arguments).ToConfigurationOverrides()).Build();
        return configuration.GetSection("Configuration").Get<SdkAppOptions>()!.Workspace;
    }

    [TestMethod]
    public void TheConstantsTheCommandLineDefines_AreTheConfigurations()
    {
        var workspace = Bound("-w", "C:/ws", "--define", "DEV=1", "MODE=\"debug\"");

        Assert.AreEqual("1", workspace.Defines["dev"]);
        Assert.AreEqual("\"debug\"", workspace.Defines["MODE"]);
    }

    [TestMethod]
    public void AConstantOfTheCommandLine_WinsOverTheProjects_AndTheProjectFileKeepsItsOwn()
    {
        var project = new RDCoreProject { Name = "Project1", PrecompilerConstants = new() { ["DEV"] = "0", ["Win64"] = "1" } };

        var composed = Bound("-w", "C:/ws", "--define", "dev=1", "TRACE=1").WithDefines(project);

        Assert.HasCount(3, composed.PrecompilerConstants);
        Assert.AreEqual("1", composed.PrecompilerConstants["DEV"]);
        Assert.AreEqual("1", composed.PrecompilerConstants["Win64"]);
        Assert.AreEqual("1", composed.PrecompilerConstants["Trace"]);
        Assert.AreEqual("0", project.PrecompilerConstants["DEV"]);
    }

    [TestMethod]
    public void WithNoConstantOnTheCommandLine_TheProjectIsAsItsFileSays()
    {
        var project = new RDCoreProject { Name = "Project1", PrecompilerConstants = new() { ["DEV"] = "0" } };

        Assert.AreSame(project, Bound("-w", "C:/ws").WithDefines(project));
    }
}
