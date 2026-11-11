using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Platform;

/// <summary>
/// The release manifest, <c>release.json</c> at the root of a released platform: what the release is, and what it was built with.
/// </summary>
/// <remarks>
/// RDCore's release workflow writes it into the platform, and publishes a copy beside the release's archive, as
/// <c>rdcore-&lt;version&gt;-&lt;rid&gt;.release.json</c>. A development build has none. It is written in <c>camelCase</c>, and read
/// regardless of case. This record holds what a client chooses a platform by; the manifest's other members, such as the list of its files,
/// are not read.
/// </remarks>
public sealed record class PlatformReleaseManifest
{
    /// <summary>The name of the release manifest at the platform root.</summary>
    public const string FileName = "release.json";

    /// <summary>
    /// The largest release manifest read, in bytes: 4 MiB, many times what a platform's list of files takes.
    /// </summary>
    public const int MaxBytes = 4 << 20;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The version of the manifest's format: 1.</summary>
    public int Schema { get; init; }

    /// <summary>The product released: <c>RDCore</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The version of the platform released, as its <c>rdcore.json</c>'s <c>platformVersion</c> names it.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>The commit the platform was built from.</summary>
    public string? Commit { get; init; }

    /// <summary>The runtime identifier the platform was built for, such as <c>win-x64</c> or <c>osx-arm64</c>: the manifest's <c>rid</c>.</summary>
    [JsonPropertyName("rid")]
    public string? RuntimeIdentifier { get; init; }

    /// <summary>How the platform is deployed: <c>framework-dependent</c>, which needs <see cref="Runtime"/> on the machine.</summary>
    public string? Deployment { get; init; }

    /// <summary>The RDCore.SDK package the platform was built with.</summary>
    public PlatformReleasePackage? Sdk { get; init; }

    /// <summary>The .NET runtime the platform runs on.</summary>
    public PlatformReleaseRuntime? Runtime { get; init; }

    /// <summary>
    /// The RDCore.SDK version the platform was built with, from <see cref="Sdk"/>; null when it names another package, or a version that
    /// isn't one.
    /// </summary>
    [JsonIgnore]
    public PlatformVersion? SdkVersion =>
        Sdk is { } sdk && string.Equals(sdk.Package, RDCoreSdk.PackageId, StringComparison.Ordinal)
            && PlatformVersion.TryParse(sdk.Version, out var version)
            ? version
            : null;

    /// <summary>
    /// Whether the platform was built with an RDCore.SDK compatible with <paramref name="sdk"/>, by
    /// <see cref="PlatformVersion.IsCompatibleWith"/>; false when the manifest doesn't say which one it was built with.
    /// </summary>
    /// <param name="sdk">The RDCore.SDK version a client was built with, such as <see cref="RDCoreSdk.Version"/>.</param>
    public bool IsBuiltWith(PlatformVersion sdk)
    {
        ArgumentNullException.ThrowIfNull(sdk);
        return SdkVersion is { } built && built.IsCompatibleWith(sdk);
    }

    /// <summary>Reads a release manifest from its content.</summary>
    /// <param name="utf8Json">The manifest's content, in UTF-8.</param>
    /// <exception cref="InvalidDataException">It is longer than <see cref="MaxBytes"/>, or not a release manifest.</exception>
    public static PlatformReleaseManifest Parse(ReadOnlySpan<byte> utf8Json) => Parse(utf8Json, FileName);

    /// <summary>
    /// Reads the release manifest of a platform root; null when it has none, as a development build has none.
    /// </summary>
    /// <param name="platformRoot">The platform root.</param>
    /// <exception cref="InvalidDataException">The manifest is longer than <see cref="MaxBytes"/>, or not a release manifest.</exception>
    /// <exception cref="IOException">The manifest can't be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The manifest can't be read.</exception>
    public static PlatformReleaseManifest? Read(string platformRoot)
    {
        var file = new FileInfo(Path.Combine(platformRoot, FileName));
        if (!file.Exists)
        {
            return null;
        }

        // the size first, so that no file is read whole for being named release.json.
        return file.Length > MaxBytes
            ? throw TooLarge(file.FullName, file.Length)
            : Parse(File.ReadAllBytes(file.FullName), file.FullName);
    }

    private static PlatformReleaseManifest Parse(ReadOnlySpan<byte> utf8Json, string path)
    {
        if (utf8Json.Length > MaxBytes)
        {
            throw TooLarge(path, utf8Json.Length);
        }

        try
        {
            return JsonSerializer.Deserialize<PlatformReleaseManifest>(utf8Json, Options)
                ?? throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, PlatformMessages.ReleaseManifestIsNotOne, path, "null"));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, PlatformMessages.ReleaseManifestIsNotOne, path, exception.Message), exception);
        }
    }

    private static InvalidDataException TooLarge(string path, long size) =>
        new(string.Format(CultureInfo.CurrentCulture, PlatformMessages.ReleaseManifestIsTooLarge, path, size, MaxBytes));
}

/// <summary>A package a released platform was built with: the <c>sdk</c> of <see cref="PlatformReleaseManifest"/>.</summary>
public sealed record class PlatformReleasePackage
{
    /// <summary>The package's id: <c>RDCore.SDK</c>.</summary>
    public string Package { get; init; } = string.Empty;

    /// <summary>The package's version, such as <c>0.1.1-alpha</c>.</summary>
    public string Version { get; init; } = string.Empty;
}

/// <summary>The .NET runtime a released platform runs on: the <c>runtime</c> of <see cref="PlatformReleaseManifest"/>.</summary>
public sealed record class PlatformReleaseRuntime
{
    /// <summary>The shared framework's name: <c>Microsoft.NETCore.App</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The oldest version of it the platform runs on, such as <c>10.0.0</c>.</summary>
    public string MinimumVersion { get; init; } = string.Empty;
}
