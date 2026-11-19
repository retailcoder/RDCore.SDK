using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;

namespace RDCore.SDK.Platform;

/// <summary>
/// Opens a help file at a topic: what a program's dialog does when its <em>Help</em> button is clicked or <kbd>F1</kbd> is pressed, with the help file and
/// context number the program names (the <c>HelpFile</c> and <c>Context</c> arguments of <c>InputBox</c> and <c>MsgBox</c>, MS-VBAL §6.1.2.8.1.12 and
/// §6.1.2.8.1.13), and what a form does for <kbd>F1</kbd> on a control with a <c>HelpContextID</c>.
/// </summary>
public interface IHelpViewer
{
    /// <summary>
    /// Opens a help file, at a topic when one is given. A file that does not exist opens nothing, and so does one that nothing can open: the caller's dialog
    /// or form stays as it was.
    /// </summary>
    /// <param name="file">The full path of the help file.</param>
    /// <param name="context">The context number of the topic; <see langword="null"/> when none is given.</param>
    void Open(string file, int? context);
}

/// <summary>
/// Opens help files as the desktop does. On Windows a compiled help file (<c>.chm</c>) named with a topic opens at that topic in the HTML Help viewer
/// (<c>hh.exe -mapid</c>). Any other file, a compiled help file named with no topic, and every file on another operating system opens with the program its
/// type is associated with, at no topic.
/// </summary>
/// <remarks>
/// <see cref="Open"/> gives what <see cref="StartOf"/> returns to the start the viewer was made with. A caller that must launch nothing, such as a test,
/// makes the viewer with a start of its own and checks what it is given.
/// </remarks>
public sealed class ShellHelpViewer : IHelpViewer
{
    /// <summary>
    /// The viewer of this machine: its files, its operating system, and processes started for real.
    /// </summary>
    public static ShellHelpViewer Instance { get; } = new(new FileSystem(), OperatingSystem.IsWindows(), Launch);

    private readonly IFileSystem _fileSystem;
    private readonly bool _windows;
    private readonly Action<ProcessStartInfo> _start;

    /// <summary>
    /// Creates a viewer.
    /// </summary>
    /// <param name="fileSystem">Where help files are looked for: one that is not there opens nothing.</param>
    /// <param name="windows">Whether the files are opened as on Windows, whose HTML Help viewer opens a compiled help file at a topic.</param>
    /// <param name="start">Starts what opens a file. A start that fails opens nothing, and does not fail <see cref="Open"/>.</param>
    public ShellHelpViewer(IFileSystem fileSystem, bool windows, Action<ProcessStartInfo> start)
    {
        _fileSystem = fileSystem;
        _windows = windows;
        _start = start;
    }

    /// <inheritdoc/>
    public void Open(string file, int? context)
    {
        if (StartOf(file, context) is not { } start)
        {
            return;
        }

        try
        {
            _start(start);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // nothing can open the file: the dialog stays open, as it does for a file that is not there.
        }
    }

    /// <summary>
    /// What <see cref="Open"/> starts to open a help file at a topic.
    /// </summary>
    /// <param name="file">The full path of the help file.</param>
    /// <param name="context">The context number of the topic; <see langword="null"/> when none is given.</param>
    /// <returns>
    /// <c>hh.exe -mapid</c> <paramref name="context"/> <paramref name="file"/> for a compiled help file named with a topic on Windows; the file itself, opened
    /// through its association (<see cref="ProcessStartInfo.UseShellExecute"/>), otherwise; <see langword="null"/> when the file does not exist.
    /// </returns>
    public ProcessStartInfo? StartOf(string file, int? context)
    {
        if (!_fileSystem.File.Exists(file))
        {
            return null;
        }

        if (_windows && context is { } topic && string.Equals(_fileSystem.Path.GetExtension(file), ".chm", StringComparison.OrdinalIgnoreCase))
        {
            var viewer = new ProcessStartInfo("hh.exe") { UseShellExecute = false };
            viewer.ArgumentList.Add("-mapid");
            viewer.ArgumentList.Add(topic.ToString(CultureInfo.InvariantCulture));
            viewer.ArgumentList.Add(file);
            return viewer;
        }

        return new ProcessStartInfo(file) { UseShellExecute = true };
    }

    private static void Launch(ProcessStartInfo start)
    {
        using var _ = Process.Start(start);
    }
}
