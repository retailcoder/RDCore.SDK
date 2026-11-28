using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RDCore.SDK.Client;
using RDCore.SDK.Platform;
using RDCore.SDK.Server.Configuration;
using System.IO.Abstractions;

namespace RDCore.Tests.Client;

/// <summary>
/// A child of the platform is told the platform root its parent resolved (<see cref="RDCoreLanguageServerLaunch.PlatformRootEnvironmentVariable"/>), and not
/// left to inherit what the parent's own environment says or to derive a root from its own folder.
/// </summary>
// mutates the RDCORE_PLATFORM_ROOT process environment variable.
[TestClass]
[DoNotParallelize]
public sealed class RDCoreServerProcessEnvironmentTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private string _folder = null!;
    private string _root = null!;
    private string? _inherited;

    [TestInitialize]
    public void CreateRoot()
    {
        _folder = Path.Combine(Path.GetTempPath(), "RDCore", "child-environment", Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_folder, "platform");
        Directory.CreateDirectory(Path.Combine(_root, "Extensions", "Title"));

        // what this process's own environment names is another folder: the child is to be told the root, not to inherit this.
        _inherited = Environment.GetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable);
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, Path.Combine(_folder, "elsewhere"));
    }

    [TestCleanup]
    public void DeleteRoot()
    {
        Environment.SetEnvironmentVariable(PlatformEnvironment.RootEnvironmentVariable, _inherited);
        Directory.Delete(_folder, recursive: true);
    }

    // a child under the root that waits for the file go beside it, then writes the root its environment names to root.txt beside it: it is still running when
    // the start checks that it did not exit at once.
    private string Child()
    {
        var folder = Path.Combine(_root, "Extensions", "Title");
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(Path.Combine(folder, "Server.cmd"), string.Join("\r\n",
                "@echo off",
                ":wait",
                "if exist \"%~dp0go\" goto done",
                "ping -n 2 127.0.0.1 >nul",
                "goto wait",
                ":done",
                "echo %RDCORE_PLATFORM_ROOT%> \"%~dp0root.txt\"",
                string.Empty));
            return "Extensions/Title/Server.cmd";
        }

        var script = Path.Combine(folder, "Server");
        File.WriteAllText(script, string.Join("\n",
            "#!/bin/sh",
            "here=$(dirname \"$0\")",
            "while [ ! -f \"$here/go\" ]; do sleep 0.1; done",
            "printf '%s' \"$RDCORE_PLATFORM_ROOT\" > \"$here/root.txt\"",
            string.Empty));
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return "Extensions/Title/Server";
    }

    [TestMethod]
    public async Task AChild_IsToldThePlatformRootThisProcessResolved()
    {
        var environment = Substitute.For<IPlatformEnvironment>();
        environment.Root.Returns(_root);
        environment.Resolve(Arg.Any<string>()).Returns(call => Path.Combine(_root, call.Arg<string>().Replace('/', Path.DirectorySeparatorChar)));
        var child = Child();
        using var sut = new RDCoreServerProcess(new FileSystem(), environment,
            Options.Create(new SdkAppOptions()), Substitute.For<ILogger<RDCoreServerProcess>>());
        using var tokenSource = new CancellationTokenSource();

        await sut.StartAsync(child, RDCoreLanguageServerLaunch.NewPipeName(), tokenSource);
        File.WriteAllText(Path.Combine(_root, "Extensions", "Title", "go"), string.Empty);
        await sut.WaitForExitAsync().WaitAsync(Patience);

        Assert.AreEqual(_root, File.ReadAllText(Path.Combine(_root, "Extensions", "Title", "root.txt")).Trim());
    }
}
