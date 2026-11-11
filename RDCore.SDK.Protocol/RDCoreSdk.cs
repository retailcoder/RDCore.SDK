using System.Reflection;
using RDCore.SDK.Platform;

namespace RDCore.SDK;

/// <summary>
/// The RDCore.SDK package itself: its id, and the version a client or a platform part was built with.
/// </summary>
public static class RDCoreSdk
{
    /// <summary>The package's id: <c>RDCore.SDK</c>.</summary>
    public const string PackageId = "RDCore.SDK";

    /// <summary>
    /// The version of the RDCore.SDK this program runs with, such as <c>0.1.1-alpha</c>: the package's version, without build metadata.
    /// </summary>
    /// <remarks>
    /// The SDK's build stamps it into its assemblies' <see cref="AssemblyInformationalVersionAttribute"/>, from the version the package is
    /// packed as. An assembly built without it reports its numeric <see cref="AssemblyName.Version"/>.
    /// </remarks>
    public static PlatformVersion Version { get; } = Of(typeof(RDCoreSdk).Assembly);

    internal static PlatformVersion Of(Assembly assembly) =>
        Of(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion, assembly.GetName().Version);

    internal static PlatformVersion Of(string? informationalVersion, Version? version) =>
        PlatformVersion.TryParse(informationalVersion, out var stamped) ? stamped : PlatformVersion.Parse(Numeric(version));

    private static string Numeric(Version? version) =>
        version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
}
