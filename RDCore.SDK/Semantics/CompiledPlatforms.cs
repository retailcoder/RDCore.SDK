using RDCore.SDK.Runtime;
using RDCore.SDK.Runtime.Abstract.Execution;

namespace RDCore.SDK.Semantics;

/// <summary>
/// The platforms that code is compiled on: those whose predefined conditional-compilation constants make live the <c>#If</c> branch it is written in
/// (<strong>MS-VBAL §3.4.2</strong>), whichever environment the session that analyzed it runs in.
/// </summary>
/// <remarks>
/// <para>
/// A platform is an operating system and a bitness, the two things that decide the values of <c>Win32</c>, <c>Win64</c>, <c>Mac</c>, <c>Linux</c> and
/// <c>Linux64</c> (<see cref="PredefinedConditionalConstants.Of(IRuntimeEnvironmentProfile)"/>). Code outside any <c>#If</c> block is compiled on
/// <see cref="All"/> of them; code in a branch is compiled on those where the conditions before it are false and its own is true. A constant that no
/// platform decides (a project <c>#Const</c>, a module's own, <c>DEBUG</c>, <c>VBA7</c>) has the value it has in the session, on every platform; one
/// that overrides a predefined constant overrides it on every platform too.
/// </para>
/// <para>
/// <c>Linux</c> and <c>Linux64</c> are RD-VBA additions, and so are the platforms they decide: MS-VBA runs on Windows and macOS only.
/// </para>
/// </remarks>
[Flags]
public enum CompiledPlatforms
{
    /// <summary>
    /// No platform: the branch is never live, whatever the platform.
    /// </summary>
    None = 0,

    /// <summary>
    /// 32-bit Windows: <c>Win32</c> is <c>1</c>, <c>Win64</c> is <c>0</c>.
    /// </summary>
    Windows32 = 1 << 0,

    /// <summary>
    /// 64-bit Windows: <c>Win32</c> and <c>Win64</c> are <c>1</c>.
    /// </summary>
    Windows64 = 1 << 1,

    /// <summary>
    /// macOS: <c>Mac</c> is <c>1</c>.
    /// </summary>
    MacOS = 1 << 2,

    /// <summary>
    /// 32-bit Linux: <c>Linux</c> is <c>1</c>, <c>Linux64</c> is <c>0</c>. An RD-VBA addition.
    /// </summary>
    Linux32 = 1 << 3,

    /// <summary>
    /// 64-bit Linux: <c>Linux</c> and <c>Linux64</c> are <c>1</c>. An RD-VBA addition.
    /// </summary>
    Linux64 = 1 << 4,

    /// <summary>
    /// Every platform: what code outside any <c>#If</c> block is compiled on.
    /// </summary>
    All = Windows32 | Windows64 | MacOS | Linux32 | Linux64,
}

/// <summary>
/// The platforms of <see cref="CompiledPlatforms"/>, and the environment each one is.
/// </summary>
public static class CompiledPlatformProfiles
{
    /// <summary>
    /// Each platform, with the operating system and the bitness whose predefined constants it evaluates a <c>#If</c> condition with
    /// (<see cref="PredefinedConditionalConstants.Of(IRuntimeEnvironmentProfile)"/>), in the order of the flags.
    /// </summary>
    /// <remarks>
    /// A host that states <see cref="DeclarationFact.CompiledOn"/> or <see cref="ExpressionFact.NativeCall"/> evaluates the module's <c>#If</c> conditions once
    /// for each of these, with the session's constants and the predefined ones of the platform in their place.
    /// </remarks>
    public static IReadOnlyList<(CompiledPlatforms Platform, VBOperatingSystem OperatingSystem, bool Is64Bit)> Each { get; } =
    [
        (CompiledPlatforms.Windows32, VBOperatingSystem.Windows, false),
        (CompiledPlatforms.Windows64, VBOperatingSystem.Windows, true),
        (CompiledPlatforms.MacOS, VBOperatingSystem.MacOS, true),
        (CompiledPlatforms.Linux32, VBOperatingSystem.Linux, false),
        (CompiledPlatforms.Linux64, VBOperatingSystem.Linux, true),
    ];
}

/// <summary>
/// The platforms of a call to a procedure declared by a <c>Declare</c> statement (<strong>MS-VBAL §5.2.3.5</strong>): where the call is compiled, and where
/// the procedure it calls is.
/// </summary>
/// <remarks>
/// A call that is compiled on a platform its procedure is not declared on refers to nothing there; one compiled on every platform, of a procedure declared
/// on every platform, calls the same library whatever the platform, and no library is on every platform.
/// </remarks>
/// <param name="Call">The platforms the call is compiled on: those that make live the <c>#If</c> branch the call is written in.</param>
/// <param name="Declared">The platforms the procedure is declared on (<see cref="DeclarationFact.CompiledOn"/> of its <c>Declare</c> statement).</param>
public readonly record struct NativeCallPlatforms(CompiledPlatforms Call, CompiledPlatforms Declared);
