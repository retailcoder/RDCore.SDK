using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.Diagnostics;

namespace RDCore.Tests.Model.Diagnostics;

/// <summary>
/// The page a diagnostic of an analyzer links to (<see cref="DiagnosticFactory.FromAnalyzerFinding"/>): a code of the platform's own prefixes links to the
/// platform's documentation, and a code of the prefix an extension declares links to the extension's pages
/// (<see cref="DiagnosticPrefixAttribute.HelpBaseUri"/>).
/// </summary>
[TestClass]
public sealed class DiagnosticHelpLinkTests
{
    private static AnalyzerFinding Finding(string prefix, int number)
        => new(new RDCoreDiagnosticCode(prefix, number), default, DiagnosticSeverity.Warning, "x");

    private static readonly DiagnosticFactory Extension = new()
    {
        DeclaredPrefix = new DiagnosticPrefixAttribute("XYZ") { HelpBaseUri = "https://example.org/xyz/docs/diagnostics/" },
    };

    [TestMethod]
    public void ACodeOfTheDeclaredPrefix_LinksToTheExtensionsPageOfIt()
        => Assert.AreEqual("https://example.org/xyz/docs/diagnostics/xyz00042.html",
            Extension.FromAnalyzerFinding(Finding("XYZ", 42)).CodeDescription?.Href.ToString());

    [TestMethod]
    public void ACodeOfThePlatformsOwn_LinksToThePlatformsDocumentation_WhateverTheExtensionDeclares()
    {
        var href = Extension.FromAnalyzerFinding(Finding(RDCoreDiagnosticCode.CorePrefix, 42)).CodeDescription?.Href.ToString();

        Assert.AreEqual(new DiagnosticFactory().FromAnalyzerFinding(Finding(RDCoreDiagnosticCode.CorePrefix, 42)).CodeDescription?.Href.ToString(), href);
        Assert.IsNotNull(href);
    }

    [TestMethod]
    public void ACodeOfAnotherPrefix_OrOfAnExtensionThatPublishesNoPages_LinksNowhere()
    {
        Assert.IsNull(Extension.FromAnalyzerFinding(Finding("ABC", 42)).CodeDescription);
        Assert.IsNull(new DiagnosticFactory { DeclaredPrefix = new DiagnosticPrefixAttribute("XYZ") }.FromAnalyzerFinding(Finding("XYZ", 42)).CodeDescription);
        Assert.IsNull(new DiagnosticFactory().FromAnalyzerFinding(Finding("XYZ", 42)).CodeDescription);
    }

    [TestMethod]
    [DataRow("docs/diagnostics")]
    [DataRow("file:///c:/docs")]
    [DataRow("ftp://example.org/docs")]
    public void AHelpBaseUriThatIsNotAWebAddress_IsRefused(string value)
        => Assert.ThrowsExactly<ArgumentException>(() => new DiagnosticPrefixAttribute("XYZ") { HelpBaseUri = value });
}
