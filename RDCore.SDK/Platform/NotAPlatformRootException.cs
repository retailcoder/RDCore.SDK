using System.Globalization;
using RDCore.SDK.Client;

namespace RDCore.SDK.Platform;

/// <summary>
/// This exception is thrown when a process that sits at the platform root, <c>rdc</c>, is started from a folder that holds no platform
/// manifest (<c>rdcore.json</c>) and its command line names no root with <c>--platform-root</c>
/// (<see cref="PlatformEnvironment.ApplyOwnFolderAsRoot(string[])"/>).
/// </summary>
/// <remarks>
/// No other folder is tried: <c>rdc</c> is at the root of its platform, beside <c>rdcore.json</c>.
/// </remarks>
/// <param name="folder">The folder the process was started from, as a full path.</param>
public sealed class NotAPlatformRootException(string folder)
    : BadConfigurationException(
        string.Format(CultureInfo.CurrentCulture, Exceptions.NotAPlatformRootException_Message, folder),
        Exceptions.NotAPlatformRootException_Verbose)
{
    /// <summary>
    /// The folder the process was started from, as a full path: the folder that holds no <c>rdcore.json</c>.
    /// </summary>
    public string Folder { get; } = folder;
}
