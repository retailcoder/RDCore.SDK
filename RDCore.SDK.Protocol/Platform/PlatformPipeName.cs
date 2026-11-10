using RDCore.SDK.Client;
using System.Security.Cryptography;

namespace RDCore.SDK.Platform;

/// <summary>
/// Names the pipes that the processes of the platform talk through: a new name for each pipe, short enough to be a pipe on every
/// supported system.
/// </summary>
/// <remarks>
/// <para>
/// On Windows a pipe is <c>\.\pipe\&lt;name&gt;</c>. On Linux and macOS, .NET makes it a Unix domain socket at
/// <c><see cref="Path.GetTempPath"/>CoreFxPipe_&lt;name&gt;</c>, and the system caps that path: 108 bytes on Linux, 104 on macOS. A
/// Mac's temporary folder (<c>$TMPDIR</c>, <c>/var/folders/&lt;2&gt;/&lt;30&gt;/T/</c>) takes 49 of them, which leaves 44 for the name. A
/// longer name cannot be opened as a pipe there, and the process that is to listen on it, or connect to it, fails to start.
/// </para>
/// <para>
/// A name is at most <see cref="MaxLength"/> characters. That leaves room for a temporary folder longer than a Mac's, and for a suffix
/// that a component appends to name a second pipe of its own.
/// </para>
/// </remarks>
public static class PlatformPipeName
{
    /// <summary>
    /// The longest name <see cref="New"/> makes, in characters: 24.
    /// </summary>
    public const int MaxLength = 24;

    private const string Prefix = "rdc-";
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

    // 36^12 is about 2^62: two instances of a component all but never draw the same name.
    private const int RandomLength = 12;

    /// <summary>
    /// A new pipe name for a component of the platform, one that no other instance has: <c>rdc-&lt;component&gt;-&lt;random&gt;</c>, at
    /// most <see cref="MaxLength"/> characters, in lowercase letters, digits and hyphens.
    /// </summary>
    /// <param name="component">
    /// The component that listens on the pipe, whichever process connects to it: <see cref="CoreServerComponent.LanguageServer"/> for
    /// the language server.
    /// </param>
    /// <returns>The name, for the pipe's server and its client to open.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="component"/> is not a defined <see cref="CoreServerComponent"/> value.
    /// </exception>
    public static string New(CoreServerComponent component)
    {
        var tag = component switch
        {
            CoreServerComponent.EnvironmentHost => "host",
            CoreServerComponent.LanguageServer => "ls",
            CoreServerComponent.ParsingServer => "parse",
            CoreServerComponent.Extension => "ext",
            CoreServerComponent.ExternalHost => "xhost",
            CoreServerComponent.ClientApp => "client",
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
        };

        return $"{Prefix}{tag}-{RandomNumberGenerator.GetString(Alphabet, RandomLength)}";
    }
}
