using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace RDCore.SDK.Platform;

/// <summary>
/// A version of the RDCore platform, or of the RDCore.SDK package that a part of it was built with, as Semantic Versioning 2.0 writes one:
/// <c>0.1.0</c>, <c>0.2.0-alpha.3</c>.
/// </summary>
/// <remarks>
/// It is the version that <c>rdcore.json</c>'s <c>platformVersion</c>, a release tag without its leading <c>v</c>, and
/// <c>release.json</c>'s <c>sdk</c> name. Versions are ordered as Semantic Versioning 2.0 orders them: a prerelease comes before its release, and build
/// metadata is ignored. Every program that deploys a platform reads, orders and names a version this way, so that a version deployed by one
/// is found by the others, in the same folder.
/// </remarks>
public sealed record PlatformVersion : IComparable<PlatformVersion>
{
    private PlatformVersion(Version core, string[] prerelease, string text)
    {
        Core = core;
        Prerelease = prerelease;
        Text = text;
    }

    /// <summary>
    /// The major, minor and patch numbers.
    /// </summary>
    public Version Core { get; }

    /// <summary>
    /// The prerelease identifiers, such as <c>alpha</c> and <c>3</c>; empty for a release.
    /// </summary>
    public IReadOnlyList<string> Prerelease { get; }

    /// <summary>
    /// Whether this is a prerelease.
    /// </summary>
    public bool IsPrerelease => Prerelease.Count > 0;

    /// <summary>
    /// The version in lowercase, without a leading <c>v</c> or build metadata: <c>V0.1.0-RC.1+build.5</c> is <c>0.1.0-rc.1</c>.
    /// </summary>
    /// <remarks>A deployed platform's folder is named by it.</remarks>
    public string Text { get; }

    /// <summary>
    /// Whether a part built with this RDCore.SDK version and a part built with <paramref name="other"/> work together: the same major and
    /// minor versions before 1.0, and the same major version from 1.0.
    /// </summary>
    /// <remarks>
    /// The rule is symmetric, and ignores the patch number and the prerelease identifiers: <c>0.1.1-alpha</c> works with <c>0.1.0</c>, and
    /// not with <c>0.2.0</c>; <c>1.2.0</c> works with <c>1.0.0</c>, and not with <c>2.0.0</c>.
    /// </remarks>
    /// <param name="other">The other part's RDCore.SDK version.</param>
    public bool IsCompatibleWith(PlatformVersion other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Core.Major == other.Core.Major && (Core.Major > 0 || Core.Minor == other.Core.Minor);
    }

    /// <summary>
    /// The range of versions this one is compatible with, as a person reads it: <c>0.1.x</c> before 1.0, and <c>1.x</c> from 1.0.
    /// </summary>
    public string CompatibleRange => Core.Major > 0
        ? string.Create(CultureInfo.InvariantCulture, $"{Core.Major}.x")
        : string.Create(CultureInfo.InvariantCulture, $"0.{Core.Minor}.x");

    /// <summary>
    /// Reads a version, with or without a leading <c>v</c>; false when it is not one.
    /// </summary>
    /// <param name="text">The version's text, such as <c>0.2.0-alpha.3</c>, <c>v0.2.0</c> or <c>0.1.1-alpha+2062f98</c>.</param>
    /// <param name="version">The version read; null when it is not one.</param>
    public static bool TryParse(string? text, [NotNullWhen(true)] out PlatformVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var plus = value.IndexOf('+');
        if (plus >= 0)
        {
            value = value[..plus];
        }

        // a deployed version's folder is named by it, and Windows does not tell RC from rc: a version is case-insensitive, and written in lowercase.
        value = value.ToLowerInvariant();
        var dash = value.IndexOf('-');
        var core = dash >= 0 ? value[..dash] : value;
        var prerelease = dash >= 0 ? value[(dash + 1)..].Split('.') : [];
        if (prerelease.Any(identifier => identifier.Length == 0 || !identifier.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            || HasLeadingZero(identifier)))
        {
            return false;
        }

        var numbers = core.Split('.');
        if (numbers.Length != 3 || !numbers.All(n => n.Length > 0 && n.All(char.IsAsciiDigit) && !HasLeadingZero(n)))
        {
            return false;
        }

        if (!Version.TryParse(core, out var parsed))
        {
            return false;
        }

        version = new PlatformVersion(parsed, prerelease, value);
        return true;
    }

    // Semantic Versioning forbids it in a number, so 01.0.0 and 1.0.0 cannot be two versions, or one with two folders.
    private static bool HasLeadingZero(string identifier) =>
        identifier.Length > 1 && identifier[0] == '0' && identifier.All(char.IsAsciiDigit);

    /// <summary>
    /// Reads a version, with or without a leading <c>v</c>.
    /// </summary>
    /// <param name="text">The version's text, such as <c>0.2.0-alpha.3</c>.</param>
    /// <exception cref="FormatException"><paramref name="text"/> is not a version.</exception>
    public static PlatformVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new FormatException(string.Format(CultureInfo.CurrentCulture, PlatformMessages.NotAVersion, text));

    /// <inheritdoc/>
    public int CompareTo(PlatformVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byCore = Core.CompareTo(other.Core);
        if (byCore != 0)
        {
            return byCore;
        }

        if (IsPrerelease != other.IsPrerelease)
        {
            return IsPrerelease ? -1 : 1;
        }

        for (var i = 0; i < Math.Min(Prerelease.Count, other.Prerelease.Count); i++)
        {
            var byIdentifier = CompareIdentifiers(Prerelease[i], other.Prerelease[i]);
            if (byIdentifier != 0)
            {
                return byIdentifier;
            }
        }

        return Prerelease.Count.CompareTo(other.Prerelease.Count);
    }

    // numeric identifiers compare as numbers and come before alphanumeric ones, which compare in ASCII order.
    private static int CompareIdentifiers(string a, string b)
    {
        var aNumeric = a.All(char.IsAsciiDigit);
        var bNumeric = b.All(char.IsAsciiDigit);
        if (aNumeric && bNumeric)
        {
            return a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
        }

        if (aNumeric != bNumeric)
        {
            return aNumeric ? -1 : 1;
        }

        return string.CompareOrdinal(a, b);
    }

    /// <summary>
    /// Whether both are the same version, by <see cref="CompareTo"/>.
    /// </summary>
    /// <param name="other">The other version.</param>
    public bool Equals(PlatformVersion? other) => other is not null && CompareTo(other) == 0;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Core, string.Join('.', Prerelease));

    /// <summary>
    /// Whether <paramref name="left"/> comes before <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    public static bool operator <(PlatformVersion left, PlatformVersion right) => Compare(left, right) < 0;

    /// <summary>
    /// Whether <paramref name="left"/> comes after <paramref name="right"/>.
    /// </summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    public static bool operator >(PlatformVersion left, PlatformVersion right) => Compare(left, right) > 0;

    /// <summary>
    /// Whether <paramref name="left"/> comes before <paramref name="right"/>, or is the same version.
    /// </summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    public static bool operator <=(PlatformVersion left, PlatformVersion right) => Compare(left, right) <= 0;

    /// <summary>
    /// Whether <paramref name="left"/> comes after <paramref name="right"/>, or is the same version.
    /// </summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    public static bool operator >=(PlatformVersion left, PlatformVersion right) => Compare(left, right) >= 0;

    private static int Compare(PlatformVersion? left, PlatformVersion? right) =>
        left is null ? (right is null ? 0 : -1) : left.CompareTo(right);

    /// <summary>
    /// The version's <see cref="Text"/>.
    /// </summary>
    public override string ToString() => Text;
}
