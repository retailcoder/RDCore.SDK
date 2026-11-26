using RDCore.SDK.Extensibility.Diagnostics;

namespace RDCore.SDK.Extensibility;

/// <summary>
/// Declares the prefix of the codes of the diagnostics an extension reports (<see cref="RDCoreDiagnosticCode.Prefix"/>): three letters <c>A</c> to <c>Z</c>, as in
/// <c>XYZ00042</c> (<strong>RD-VBAL §1.1.4</strong>).
/// </summary>
/// <remarks>
/// <para>
/// The prefix is written into the extension's manifest when the extension is described (<see cref="IExtensionsProvider.Describe"/>), as
/// <see cref="ExtensionInfo.DiagnosticPrefix"/>. The language server starts an extension only when its prefix is its own
/// (<see cref="IExtensionsProvider.Discover"/>): <c>RDC</c> belongs to the core extension, the language's prefixes (<c>VBC</c>, <c>VBR</c>, <c>VBA</c>)
/// belong to no extension, and two extensions that declare the same prefix are both left unstarted. <c>RDX</c>, the first-party extensions' prefix, is the
/// one that several extensions may declare.
/// </para>
/// <para>
/// A finding the extension reports under another prefix is dropped. A finding under its own prefix is reported under the extension's name.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class DiagnosticPrefixAttribute : Attribute
{
    /// <summary>
    /// Declares the prefix of the codes of the extension's diagnostics.
    /// </summary>
    /// <param name="prefix">Three letters, <c>A</c> to <c>Z</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="prefix"/> is not three letters <c>A</c> to <c>Z</c>.</exception>
    public DiagnosticPrefixAttribute(string prefix)
    {
        if (!RDCoreDiagnosticCode.IsPrefix(prefix))
        {
            throw new ArgumentException($"A diagnostic code prefix is three letters A to Z: '{prefix}' is not.", nameof(prefix));
        }

        Prefix = prefix;
    }

    /// <summary>
    /// The prefix of the codes of the extension's diagnostics.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// The address of the pages that document the extension's diagnostics, such as <c>https://example.org/docs/diagnostics</c>, where the page of
    /// <c>XYZ00042</c> is <c>xyz00042.html</c>. <see langword="null"/> when the extension publishes none.
    /// </summary>
    /// <remarks>
    /// A diagnostic of the extension's prefix links to its page (LSP <c>codeDescription</c>) when a <see cref="Model.Diagnostics.DiagnosticFactory"/> that is
    /// given the extension's declared prefix (<see cref="Model.Diagnostics.DiagnosticFactory.DeclaredPrefix"/>) makes it. A code of one of the platform's own
    /// prefixes links to the platform's documentation instead.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is not an absolute <c>http</c> or <c>https</c> address.</exception>
    public string? HelpBaseUri
    {
        get => _helpBaseUri;
        set => _helpBaseUri = value is null || IsWebAddress(value)
            ? value
            : throw new ArgumentException($"The address of an extension's diagnostics pages is an absolute http or https address: '{value}' is not.",
                nameof(value));
    }

    private string? _helpBaseUri;

    private static bool IsWebAddress(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
