using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Platform;

/// <summary>
/// The platform manifest, <c>rdcore.json</c> at the platform root: where each component of a deployed platform is.
/// </summary>
/// <remarks>
/// Written when the platform is deployed (<c>PlatformPublish.ps1</c>), in <c>camelCase</c>, and read regardless of case. The paths are relative to the platform root, with
/// either slash style.
/// </remarks>
public record class PlatformManifest
{
    /// <summary>The name of the manifest file at the platform root.</summary>
    public const string FileName = "rdcore.json";

    /// <summary>The version of the deployed platform.</summary>
    public string PlatformVersion { get; init; } = string.Empty;

    /// <summary>
    /// The runtime identifier the platform was built for, such as <c>win-x64</c> or <c>osx-arm64</c>: the manifest's <c>rid</c>. Empty when
    /// the manifest does not name one.
    /// </summary>
    [JsonPropertyName("rid")]
    public string RuntimeIdentifier { get; init; } = string.Empty;

    /// <summary>
    /// The runtime identifier this process runs as, in the form <see cref="RuntimeIdentifier"/> takes: the operating system (<c>win</c>, <c>osx</c>,
    /// <c>linux</c> or <c>freebsd</c>), a hyphen, and the architecture of the process in lower case (<c>x64</c>, <c>arm64</c>), as in <c>win-x64</c>. On
    /// any other operating system, the identifier .NET gives (<see cref="RuntimeInformation.RuntimeIdentifier"/>).
    /// </summary>
    /// <remarks>
    /// A platform whose manifest names this identifier was built for this operating system and this process's architecture. The value is the portable
    /// identifier, whatever the distribution of Linux: <see cref="RuntimeInformation.RuntimeIdentifier"/> can name one (<c>ubuntu.24.04-x64</c>), and a
    /// manifest does not. The C library is not part of it: on a Linux that uses musl, the value is <c>linux-x64</c>, as for one that uses glibc, and a
    /// platform built for one does not run on the other. The architecture is the process's (<see cref="RuntimeInformation.ProcessArchitecture"/>): an
    /// x64 process that runs emulated on an Arm64 machine has an <c>x64</c> identifier. The value is neither read from a manifest nor written to one.
    /// </remarks>
    public static string MachineRuntimeIdentifier { get; } = OperatingSystemOfTheIdentifier() is { } system
        ? $"{system}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}"
        : RuntimeInformation.RuntimeIdentifier;

    private static string? OperatingSystemOfTheIdentifier()
        => OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "osx"
            : OperatingSystem.IsLinux() ? "linux"
            : OperatingSystem.IsFreeBSD() ? "freebsd"
            : null;

    /// <summary>When the manifest was written, in UTC.</summary>
    public DateTime GeneratedUtc { get; init; } = default;

    /// <summary>The folder of the platform's extensions: one subfolder each, named for the extension.</summary>
    public string ExtensionsDirectory { get; init; } = string.Empty;

    /// <summary>The executable of the environment host: <c>rdc.exe</c>, which runs in host mode for it.</summary>
    public string HostService { get; init; } = string.Empty;

    /// <summary>The executable of the language server, which a client starts and connects to.</summary>
    public string LangService { get; init; } = string.Empty;

    /// <summary>The executable of the parse server.</summary>
    public string ParseServer { get; init; } = string.Empty;

    /// <summary>
    /// The extension the platform bundles: the name of its folder under <see cref="ExtensionsDirectory"/>.
    /// </summary>
    /// <remarks>
    /// The core extension reports the platform's first-party diagnostics. The prefixes of their codes (<c>RDC</c>, <c>RDX</c>) are its own: no other extension
    /// that declares one is started (<strong>RD-VBAL §1.1.4</strong>). A manifest that does not name it bundles <see cref="DefaultCoreExtension"/>.
    /// </remarks>
    public string CoreExtension { get; init; } = DefaultCoreExtension;

    /// <summary>
    /// The extension a platform bundles when its manifest does not say: <c>RDCore.Diagnostics</c>.
    /// </summary>
    public const string DefaultCoreExtension = "RDCore.Diagnostics";
}
