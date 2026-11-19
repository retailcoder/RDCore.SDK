using RDCore.SDK.Platform;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Abstractions.TestingHelpers;

namespace RDCore.Tests.Platform;

/// <summary>
/// What opens a help file at a topic: the HTML Help viewer for a compiled help file on Windows, and the program a file's type is associated with for every
/// other; nothing for a file that is not there. Nothing is launched: the viewer is given a start that records what it was asked to start.
/// </summary>
[TestClass]
[TestCategory("Platform")]
public sealed class HelpViewerTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "rdcore-help");

    private static (ShellHelpViewer Viewer, List<ProcessStartInfo> Started) Viewer(bool windows, params string[] files)
    {
        var fileSystem = new MockFileSystem(files.ToDictionary(file => file, _ => new MockFileData(string.Empty)));
        var started = new List<ProcessStartInfo>();
        return (new ShellHelpViewer(fileSystem, windows, started.Add), started);
    }

    [TestMethod]
    public void ACompiledHelpFileNamedWithATopic_OpensAtItsTopic_InTheHtmlHelpViewerOnWindows()
    {
        var file = Path.Combine(Folder, "app.chm");
        var (viewer, started) = Viewer(windows: true, file);

        viewer.Open(file, 42);

        var start = started.Single();
        Assert.AreEqual("hh.exe", start.FileName);
        Assert.IsFalse(start.UseShellExecute);
        CollectionAssert.AreEqual(new[] { "-mapid", "42", file }, start.ArgumentList.ToArray());
    }

    [TestMethod]
    [DataRow("app.hlp", 42, true, DisplayName = "a WinHelp file")]
    [DataRow("APP.CHM", null, true, DisplayName = "a compiled help file named with no topic")]
    [DataRow("app.chm", 42, false, DisplayName = "a compiled help file elsewhere than on Windows")]
    [DataRow("app.html", 42, false, DisplayName = "a web page elsewhere than on Windows")]
    public void AnyOtherHelpFile_OpensWithTheProgramItsTypeIsAssociatedWith(string name, int? context, bool windows)
    {
        var file = Path.Combine(Folder, name);
        var (viewer, started) = Viewer(windows, file);

        viewer.Open(file, context);

        var start = started.Single();
        Assert.AreEqual(file, start.FileName);
        Assert.IsTrue(start.UseShellExecute);
        Assert.IsEmpty(start.ArgumentList);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "on Windows")]
    [DataRow(false, DisplayName = "elsewhere")]
    public void AHelpFileThatIsNotThere_OpensNothing(bool windows)
    {
        var (viewer, started) = Viewer(windows);

        viewer.Open(Path.Combine(Folder, "app.chm"), 42);

        Assert.IsNull(viewer.StartOf(Path.Combine(Folder, "app.chm"), 42));
        Assert.IsEmpty(started);
    }

    // a file with nothing to open it fails the start, and nothing else: the dialog that asked stays open.
    [TestMethod]
    public void AHelpFileNothingCanOpen_OpensNothing_AndFailsNothing()
    {
        var file = Path.Combine(Folder, "app.unknown");
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData> { [file] = new(string.Empty) });
        var viewer = new ShellHelpViewer(fileSystem, windows: true, _ => throw new Win32Exception(1155, "No application is associated with the file."));

        viewer.Open(file, null);
    }
}
