namespace RDCore.SDK.Runtime.Abstract.Execution;

/// <summary>
/// The operating system an environment runs on: which of the built-in <c>Win32</c>, <c>Mac</c> and <c>Linux</c> conditional-compilation constants is
/// <c>1</c>, and with the bitness, whether <c>Win64</c> is.
/// </summary>
/// <remarks>
/// Each of the three is <c>1</c> only on its own operating system, and defined <c>0</c> on every other, so that <c>#If Linux Then</c> says the same thing
/// wherever it is compiled. A project <c>#Const</c> or a command-line define still overrides any of them.
/// </remarks>
public enum VBOperatingSystem
{
    /// <summary>
    /// Windows: <c>Win32</c> is <c>1</c>, and <c>Win64</c> too in a 64-bit environment.
    /// </summary>
    /// <remarks>
    /// The default: what MS-VBA runs on, and what a profile that does not say is.
    /// </remarks>
    Windows = 0,

    /// <summary>
    /// macOS: <c>Mac</c> is <c>1</c>, and <c>Win64</c> too in a 64-bit environment, as in Excel for Mac, unless
    /// <see cref="IRuntimeEnvironmentProfile.Win64OnlySetOnWindows"/> says otherwise.
    /// </summary>
    MacOS = 1,

    /// <summary>
    /// Linux: <c>Linux</c> is <c>1</c>.
    /// </summary>
    /// <remarks>
    /// An RD-VBA addition: MS-VBA does not run on Linux, and has no <c>Linux</c> constant. <c>Win64</c> is <c>0</c> there, whatever the bitness.
    /// </remarks>
    Linux = 2,

    /// <summary>
    /// Any other operating system: every operating-system constant is <c>0</c>.
    /// </summary>
    Other = 3,
}
