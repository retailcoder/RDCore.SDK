using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace RDCore.SDK.Platform;

/// <summary>
/// The components of the platform whose versions a program or a client reads by name: RDCore, RDCore.SDK and RDCore.Forms.
/// </summary>
/// <remarks>
/// <para>
/// A component's version is the informational version its build stamped into its assembly (<see cref="AssemblyInformationalVersionAttribute"/>), such as
/// <c>0.1.6-alpha+30d43d6…</c>, prerelease suffix and build metadata included, as <see cref="AssemblyDisplayVersion"/> reports it.
/// </para>
/// <para>
/// It is read from the component's assembly file in the folder the SDK's own assembly is in, without loading it: the platform's folder that the
/// environment host and the external host both run from, or a built program's folder, which holds the assemblies the program runs on. Every process of
/// one installation reads the same version, whether or not it has loaded the component, and a component whose file is not there is not part of it.
/// </para>
/// </remarks>
public static class PlatformComponents
{
    /// <summary>
    /// The platform itself: <c>RDCore</c>, whose version is that of its runtime, <c>RDCore.Runtime.dll</c>, which every RD-VBA program runs on.
    /// </summary>
    public const string Platform = "RDCore";

    /// <summary>
    /// The SDK: <c>RDCore.SDK</c>, whose version is that of the SDK's own assembly.
    /// </summary>
    public const string Sdk = RDCoreSdk.PackageId;

    /// <summary>
    /// The forms package: <c>RDCore.Forms</c>, whose version is that of <c>RDCore.Forms.dll</c>.
    /// </summary>
    public const string Forms = "RDCore.Forms";

    /// <summary>
    /// The names of the components, <see cref="Platform"/>, <see cref="Sdk"/> and <see cref="Forms"/>, in that order.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = [Platform, Sdk, Forms];

    // the assembly file of each component other than the SDK, by the component's name.
    private static readonly Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase)
    {
        [Platform] = "RDCore.Runtime.dll",
        [Forms] = "RDCore.Forms.dll",
    };

    /// <summary>
    /// The version of a component of the platform: the informational version of its assembly file, beside the SDK's.
    /// </summary>
    /// <param name="component">The component's name, one of <see cref="Names"/>, in any case.</param>
    /// <returns>
    /// The version, such as <c>0.1.6-alpha+30d43d6…</c>; the assembly's numeric version, as <c>major.minor.build</c>, when its build stamped none.
    /// <see langword="null"/> when <paramref name="component"/> names no component, or when the component's file is not beside the SDK's, or cannot be
    /// read as an assembly.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> is <see langword="null"/>.</exception>
    public static string? VersionOf(string component) => VersionOf(component, Folder);

    internal static string? VersionOf(string component, string folder)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (string.Equals(component, Sdk, StringComparison.OrdinalIgnoreCase))
        {
            return AssemblyDisplayVersion.Get(typeof(PlatformComponents).Assembly);
        }

        return Files.TryGetValue(component, out var file) ? VersionOfFile(Path.Combine(folder, file)) : null;
    }

    // the folder the SDK's assembly was loaded from; the application's own when the assembly has no file of its own (bundled in a single-file executable).
    private static string Folder
        => Path.GetDirectoryName(typeof(PlatformComponents).Assembly.Location) is { Length: > 0 } folder ? folder : AppContext.BaseDirectory;

    // the version an assembly file says it is, read from its metadata: the file is not loaded.
    internal static string? VersionOfFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var image = new PEReader(stream);
            if (!image.HasMetadata)
            {
                return null;
            }

            var metadata = image.GetMetadataReader();
            if (!metadata.IsAssembly)
            {
                return null;
            }

            var assembly = metadata.GetAssemblyDefinition();
            return AssemblyDisplayVersion.Get(InformationalVersionOf(metadata, assembly), assembly.Version);
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? InformationalVersionOf(MetadataReader metadata, AssemblyDefinition assembly)
    {
        foreach (var handle in assembly.GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (!IsInformationalVersion(metadata, attribute.Constructor))
            {
                continue;
            }

            // ECMA-335 §II.23.3: the prolog 0x0001, then the constructor's one string argument.
            var value = metadata.GetBlobReader(attribute.Value);
            return value.ReadUInt16() == 1 ? value.ReadSerializedString() : null;
        }

        return null;
    }

    private static bool IsInformationalVersion(MetadataReader metadata, EntityHandle constructor)
    {
        if (constructor.Kind is HandleKind.MemberReference
            && metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent is { Kind: HandleKind.TypeReference } parent)
        {
            var type = metadata.GetTypeReference((TypeReferenceHandle)parent);
            return Is(metadata, type.Namespace, type.Name);
        }

        if (constructor.Kind is HandleKind.MethodDefinition)
        {
            var type = metadata.GetTypeDefinition(metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType());
            return Is(metadata, type.Namespace, type.Name);
        }

        return false;
    }

    private static bool Is(MetadataReader metadata, StringHandle @namespace, StringHandle name)
        => metadata.StringComparer.Equals(@namespace, typeof(AssemblyInformationalVersionAttribute).Namespace!)
            && metadata.StringComparer.Equals(name, nameof(AssemblyInformationalVersionAttribute));
}
