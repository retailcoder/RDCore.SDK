using System.Globalization;
using System.Reflection;
using System.Resources;

namespace RDCore.SDK.Platform;

/// <summary>
/// The splash of the platform: its logo, its slogan, the versions that run and its copyright notice, as a console component of the platform shows them when
/// it starts.
/// </summary>
/// <remarks>
/// <para>
/// This is what the splash says and where each part of it is, not how it looks: a component renders the lines (<see cref="Lines"/>) as its console does, in
/// the colours of its theme or in none (<see cref="WriteTo"/>). A component may put a title of its own between the logo and the slogan
/// (<see cref="SplashTitle"/>); the logo, the slogan and the notice are the same in every component. It is shown when a component starts, and never after
/// what a program writes.
/// </para>
/// <para>
/// The version line (<see cref="VersionLine"/>) names the application and its version, and the version of the SDK it runs on. A component that renders the
/// lines keeps it, or leaves out the lines of its part (<see cref="SplashPart.Version"/>).
/// </para>
/// <para>
/// An application shows it as it starts (<see cref="RDCore.SDK.Server.AppHost{TApp}.ShowSplash"/>), in plain text unless it overrides that method to show a splash of
/// its own, drawn with these lines, or none at all.
/// </para>
/// <para>
/// The version line and the copyright notice are in the language of the user interface; the logo and the slogan are in none. The year and the company
/// the notice names are those of the SDK's own assembly (<see cref="CopyrightYear"/>, <see cref="Company"/>), which its package's copyright says too.
/// </para>
/// </remarks>
public static class PlatformSplash
{
    private static readonly ResourceManager Resources = new("RDCore.SDK.Platform.PlatformSplash", typeof(PlatformSplash).Assembly);

    /// <summary>
    /// The heart of the slogan (<see cref="Slogan"/>): the one character of it that a console that has colours shows in red.
    /// </summary>
    public const string Heart = "♥";

    /// <summary>
    /// The logo: a rubber duck, in lines of ASCII art separated by <c>\n</c>, with no indentation.
    /// </summary>
    public static string Logo => Text("Logo");

    /// <summary>
    /// The slogan, <c>V I V A T ♥ C U C U M I S ™</c>: "long live the cucumber", in Latin, which is in no language of the user interface.
    /// </summary>
    public static string Slogan => Text("Slogan");

    /// <summary>
    /// The company that holds the copyright of the platform: the company of the SDK's assembly (<see cref="AssemblyCompanyAttribute"/>).
    /// </summary>
    public static string Company => typeof(PlatformSplash).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;

    /// <summary>
    /// The year of the copyright of the platform: the one the SDK's assembly says (<see cref="AssemblyMetadataAttribute"/> <c>CopyrightYear</c>), which is
    /// the year its package's copyright says.
    /// </summary>
    public static string CopyrightYear => typeof(PlatformSplash).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(metadata => metadata.Key == nameof(CopyrightYear))?.Value ?? string.Empty;

    /// <summary>
    /// The copyright notice of the platform, in a language: <c>Copyright © 2026 9562-7303 Québec inc., all rights reserved.</c>
    /// </summary>
    /// <param name="culture">The language of the user interface; the current one (<see cref="CultureInfo.CurrentUICulture"/>) when it is not said.</param>
    /// <returns>The notice in that language, or in English when the platform has none in it.</returns>
    public static string CopyrightNotice(CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return string.Format(culture, Resources.GetString(nameof(CopyrightNotice), culture) ?? string.Empty, CopyrightYear, Company);
    }

    /// <summary>
    /// The version of the SDK the application runs on: the version of the SDK as a component of the platform
    /// (<see cref="PlatformComponents.VersionOf(string)"/>), the informational version of the SDK's own assembly.
    /// </summary>
    public static string SdkVersion => PlatformComponents.VersionOf(PlatformComponents.Sdk)!;

    /// <summary>
    /// The line of the splash that says which versions run, in a language: <c>RDCore 0.1.0+3cab50a, built on RDCore.SDK 0.1.5-alpha+30d43d6</c>.
    /// </summary>
    /// <remarks>
    /// The application is named by the product of its assembly (<see cref="AssemblyProductAttribute"/>), or by the assembly's name when it says none, and its
    /// version is the assembly's informational version (<see cref="AssemblyDisplayVersion"/>). The build metadata of a version (the commit it was built
    /// from) is shown by its first 7 characters.
    /// </remarks>
    /// <param name="application">
    /// The application's assembly; the process's entry assembly (<see cref="Assembly.GetEntryAssembly"/>) when it is not said.
    /// </param>
    /// <param name="culture">The language of the user interface; the current one (<see cref="CultureInfo.CurrentUICulture"/>) when it is not said.</param>
    /// <returns>
    /// The line in that language, or in English when the platform has none in it.
    /// </returns>
    public static string VersionLine(Assembly? application = null, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        application ??= Assembly.GetEntryAssembly();
        var product = application?.GetCustomAttribute<AssemblyProductAttribute>()?.Product is { Length: > 0 } named ? named : application?.GetName().Name ?? string.Empty;
        return string.Format(culture, Resources.GetString(nameof(VersionLine), culture) ?? string.Empty,
            product, Shown(AssemblyDisplayVersion.Get(application)), Shown(SdkVersion));
    }

    /// <summary>
    /// The splash, line by line, as it is shown: the logo, the component's title when it has one, a blank line, the slogan centred under what is above it, a
    /// blank line, then the version line (<see cref="VersionLine"/>) of the process's entry assembly and the copyright notice, neither of them indented.
    /// </summary>
    /// <param name="culture">The language of the version line and the copyright notice; the current one of the user interface when it is not said.</param>
    /// <param name="title">The component's own title, under the logo; none when it is not said.</param>
    /// <returns>Each line, with the part of the splash it is of: a renderer styles each part as its console does.</returns>
    public static IReadOnlyList<SplashLine> Lines(CultureInfo? culture = null, SplashTitle? title = null)
    {
        var logo = LinesOf(Logo).ToArray();
        var logoIndent = new string(' ', title?.LogoIndent ?? 0);
        string[] titleLines = title is null ? [] : [.. title.Lines.Select(line => line.TrimEnd()).Reverse().SkipWhile(line => line.Length == 0).Reverse()];

        SplashLine[] art =
        [
            .. logo.Select(line => new SplashLine(SplashPart.Logo, line.Length == 0 ? line : logoIndent + line)),
            .. titleLines.Select(line => new SplashLine(SplashPart.Title, line)),
        ];

        // the slogan is centred under the art itself: from its leftmost character to its rightmost, whatever space is before them.
        var drawn = art.Select(line => line.Text).Where(text => text.Length > 0).ToArray();
        var left = drawn.Min(text => text.Length - text.TrimStart().Length);
        var right = drawn.Max(text => text.Length);
        var sloganIndent = new string(' ', left + Math.Max(0, (right - left - Slogan.Length) / 2));
        return
        [
            .. art,
            new SplashLine(SplashPart.Space, string.Empty),
            new SplashLine(SplashPart.Slogan, sloganIndent + Slogan),
            new SplashLine(SplashPart.Space, string.Empty),
            new SplashLine(SplashPart.Version, VersionLine(culture: culture)),
            new SplashLine(SplashPart.CopyrightNotice, CopyrightNotice(culture)),
        ];
    }

    /// <summary>
    /// Writes the splash (<see cref="Lines"/>) as plain text, a line at a time, in no colour: what a component that renders it in none of its own shows.
    /// </summary>
    /// <param name="writer">What the splash is written to: the component's standard output, say.</param>
    /// <param name="culture">The language of the version line and the copyright notice; the current one of the user interface when it is not said.</param>
    /// <param name="title">The component's own title, under the logo; none when it is not said.</param>
    public static void WriteTo(TextWriter writer, CultureInfo? culture = null, SplashTitle? title = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (var line in Lines(culture, title))
        {
            writer.WriteLine(line.Text);
        }
    }

    // the art is in no language: it is read from the neutral resources, whatever the language of the user interface.
    private static string Text(string name) => (Resources.GetString(name, CultureInfo.InvariantCulture) ?? string.Empty).Replace("\r\n", "\n");

    private static IEnumerable<string> LinesOf(string text) => text.Split('\n').Select(line => line.TrimEnd());

    // a version as the splash shows it: its build metadata, a commit, by its first 7 characters.
    private static string Shown(string version) => version.IndexOf('+') is >= 0 and var plus ? version[..Math.Min(version.Length, plus + 8)] : version;
}

/// <summary>
/// A component's own title in the splash of the platform (<see cref="PlatformSplash.Lines"/>): lines of art shown under the logo, over the slogan.
/// </summary>
/// <param name="Lines">The lines of the title, with no line break in any of them; blank lines at its end are not shown.</param>
/// <param name="LogoIndent">How far the logo is indented over the title, in characters, so that the two sit together as the title's art means them to.</param>
public sealed record SplashTitle(IReadOnlyList<string> Lines, int LogoIndent);

/// <summary>
/// A part of the splash of the platform (<see cref="PlatformSplash"/>), which a renderer may style as it styles none of the others.
/// </summary>
public enum SplashPart
{
    /// <summary>
    /// A line of the logo (<see cref="PlatformSplash.Logo"/>).
    /// </summary>
    Logo,

    /// <summary>
    /// A line of the component's own title (<see cref="SplashTitle"/>).
    /// </summary>
    Title,

    /// <summary>
    /// The slogan (<see cref="PlatformSplash.Slogan"/>).
    /// </summary>
    Slogan,

    /// <summary>
    /// A blank line between two parts.
    /// </summary>
    Space,

    /// <summary>
    /// The copyright notice (<see cref="PlatformSplash.CopyrightNotice"/>).
    /// </summary>
    CopyrightNotice,

    /// <summary>
    /// The versions that run (<see cref="PlatformSplash.VersionLine"/>): the application's, the informational version of the process's entry assembly, named
    /// by its product; and the SDK's, the informational version of the SDK's own assembly (<see cref="PlatformSplash.SdkVersion"/>). A host that draws a
    /// splash of its own from <see cref="PlatformSplash.Lines"/> leaves it out by skipping the lines of this part.
    /// </summary>
    Version,
}

/// <summary>
/// A line of the splash of the platform (<see cref="PlatformSplash.Lines"/>).
/// </summary>
/// <param name="Part">The part of the splash the line is of.</param>
/// <param name="Text">The text of the line, indented as it is shown, with no line break.</param>
public readonly record struct SplashLine(SplashPart Part, string Text);
