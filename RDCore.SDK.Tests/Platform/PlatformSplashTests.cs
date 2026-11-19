using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using RDCore.SDK;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// The splash of the platform: what it says, where each part of it is, with a component's title and without one.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class PlatformSplashTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-CA");
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-CA");

    [TestMethod]
    public void TheCopyrightNotice_NamesTheYearAndTheCompanyOfTheSdksAssembly()
    {
        var assembly = typeof(PlatformSplash).Assembly;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
        var year = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(metadata => metadata.Key == "CopyrightYear").Value;

        Assert.AreEqual(company, PlatformSplash.Company);
        Assert.AreEqual(year, PlatformSplash.CopyrightYear);
        Assert.IsTrue(int.TryParse(year, out var number) && number >= 2026, $"the year of the copyright is a year: '{year}'");
        Assert.AreEqual($"Copyright © {year} {company}, all rights reserved.", PlatformSplash.CopyrightNotice(English));
    }

    [TestMethod]
    public void TheCopyrightNotice_IsInTheLanguageOfTheUserInterface()
    {
        Assert.AreEqual($"Copyright © {PlatformSplash.CopyrightYear} {PlatformSplash.Company}, tous droits réservés.", PlatformSplash.CopyrightNotice(French));
    }

    [TestMethod]
    public void TheCopyrightNotice_IsInTheCurrentLanguageOfTheUserInterface_WhenNoneIsSaid()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = French;
            Assert.AreEqual(PlatformSplash.CopyrightNotice(French), PlatformSplash.CopyrightNotice());
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [TestMethod]
    public void TheSplash_IsTheLogo_ThenTheSloganCentredUnderIt_ThenTheNoticeUnindented()
    {
        var lines = PlatformSplash.Lines(English);

        CollectionAssert.AreEqual(new[] { SplashPart.Logo, SplashPart.Space, SplashPart.Slogan, SplashPart.Space, SplashPart.Version, SplashPart.CopyrightNotice },
            Parts(lines), "the parts, in the order they are shown");
        var logo = lines.Where(line => line.Part == SplashPart.Logo).Select(line => line.Text).ToArray();
        Assert.AreEqual(19, logo.Length);
        CollectionAssert.AreEqual(PlatformSplash.Logo.Split('\n').Select(line => line.TrimEnd()).ToArray(), logo, "the logo is not indented");

        var drawn = logo.Where(line => line.Length > 0).ToArray();
        var left = drawn.Min(line => line.Length - line.TrimStart().Length);
        var sloganIndent = left + (drawn.Max(line => line.Length) - left - PlatformSplash.Slogan.Length) / 2;
        Assert.AreEqual(new string(' ', sloganIndent) + PlatformSplash.Slogan, lines.Single(line => line.Part == SplashPart.Slogan).Text,
            "the slogan is centred under the logo: under its leftmost character to its rightmost");
        Assert.AreEqual(PlatformSplash.VersionLine(culture: English), lines[^2].Text, "the version line is over the notice, and it is not indented");
        Assert.AreEqual(PlatformSplash.CopyrightNotice(English), lines[^1].Text, "the notice is the last line, and it is not indented");
        AssertEachIsOneLine(lines);
    }

    [TestMethod]
    public void AComponentsTitle_IsUnderTheLogo_AndTheSloganIsCentredUnderTheWiderOfTheTwo()
    {
        var title = new SplashTitle([new string('=', 100), new string('=', 100) + "   ", "   "], LogoIndent: 15);

        var lines = PlatformSplash.Lines(English, title);

        CollectionAssert.AreEqual(new[] { SplashPart.Logo, SplashPart.Title, SplashPart.Space, SplashPart.Slogan, SplashPart.Space, SplashPart.Version, SplashPart.CopyrightNotice },
            Parts(lines), "the parts, in the order they are shown");
        var logo = PlatformSplash.Lines(English).Where(line => line.Part == SplashPart.Logo).Select(line => line.Text.Length == 0 ? line.Text : new string(' ', 15) + line.Text);
        CollectionAssert.AreEqual(logo.ToArray(), lines.Where(line => line.Part == SplashPart.Logo).Select(line => line.Text).ToArray(),
            "the logo is indented over the title as the title says");
        CollectionAssert.AreEqual(new[] { new string('=', 100), new string('=', 100) }, lines.Where(line => line.Part == SplashPart.Title).Select(line => line.Text).ToArray(),
            "the title, with nothing after the last character of a line and no blank line at its end");
        Assert.AreEqual(new string(' ', (100 - PlatformSplash.Slogan.Length) / 2) + PlatformSplash.Slogan, lines.Single(line => line.Part == SplashPart.Slogan).Text,
            "the slogan is centred under the title, the wider of the two");
        AssertEachIsOneLine(lines);
    }

    [TestMethod]
    public void TheLogoAndTheSlogan_AreInNoLanguage()
    {
        string[] Art(CultureInfo culture) => [.. PlatformSplash.Lines(culture).Where(line => line.Part is not (SplashPart.CopyrightNotice or SplashPart.Version)).Select(line => line.Text)];

        CollectionAssert.AreEqual(Art(English), Art(French));
        Assert.AreEqual("V I V A T  ♥  C U C U M I S ™", PlatformSplash.Slogan);
        StringAssert.Contains(PlatformSplash.Slogan, PlatformSplash.Heart);
    }

    [TestMethod]
    public void TheSplashInPlainText_IsEachOfItsLines()
    {
        var writer = new StringWriter { NewLine = "\n" };

        PlatformSplash.WriteTo(writer, French);

        Assert.AreEqual(string.Concat(PlatformSplash.Lines(French).Select(line => line.Text + "\n")), writer.ToString());
    }

    [TestMethod]
    public void TheVersionLine_NamesTheApplicationsProductAndVersion_AndTheSdksVersion()
    {
        var sdk = typeof(PlatformSplash).Assembly;
        var version = Shown(AssemblyDisplayVersion.Get(sdk));

        Assert.AreEqual(AssemblyDisplayVersion.Get(sdk), PlatformSplash.SdkVersion);
        Assert.AreEqual($"RDCore {version}, built on RDCore.SDK {version}", PlatformSplash.VersionLine(sdk, English), "the SDK's own assembly, as an application");
        Assert.AreEqual($"RDCore {version}, bâti sur RDCore.SDK {version}", PlatformSplash.VersionLine(sdk, French));
        StringAssert.Matches(version, new Regex(@"^[^+]+(\+[0-9A-Za-z.-]{1,7})?$"), "the commit of a version, by its first 7 characters");
    }

    [TestMethod]
    public void TheVersionLine_IsTheEntryAssemblys_WhenNoApplicationIsSaid()
    {
        Assert.AreEqual(PlatformSplash.VersionLine(Assembly.GetEntryAssembly(), English), PlatformSplash.VersionLine(culture: English));
    }

    // a version by the first 7 characters of its build metadata.
    private static string Shown(string version) => version.Contains('+') ? version[..Math.Min(version.Length, version.IndexOf('+') + 8)] : version;

    private static SplashPart[] Parts(IReadOnlyList<SplashLine> lines)
        => [.. lines.Select(line => line.Part).Where((part, index) => index == 0 || part != lines[index - 1].Part)];

    private static void AssertEachIsOneLine(IReadOnlyList<SplashLine> lines)
        => Assert.IsTrue(lines.All(line => !line.Text.Contains('\n') && !line.Text.Contains('\r') && line.Text == line.Text.TrimEnd()),
            "a line is one line, with nothing after its last character");
}
