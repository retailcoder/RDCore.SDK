using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace RDCore.SDK.Workspace;

/// <summary>
/// The LSP document selectors of the languages the platform serves (<see cref="SupportedLanguages"/>), as a language server registers its
/// features for them.
/// </summary>
public static class SupportedLanguageSelectors
{
    /// <summary>
    /// The selector of the documents of every language the platform serves, by their files: a client names a document's language its own way, so what selects
    /// a document is the file it is.
    /// </summary>
    public static TextDocumentSelector DocumentSelector { get; } = new(SupportedLanguages.All
        .SelectMany(language => language.FileTypes).Distinct()
        .Select(fileType => new TextDocumentFilter { Pattern = $"**/{fileType}" }));

    extension(SupportedLanguage language)
    {
        /// <summary>
        /// The selector of the documents written in the language: its identifier, and the patterns of its files
        /// (<see cref="SupportedLanguage.FilterString"/>).
        /// </summary>
        /// <returns>A selector with one filter.</returns>
        public TextDocumentSelector ToTextDocumentSelector() => new(
            new TextDocumentFilter
            {
                Language = language.Id,
                Pattern = language.FilterString,
            });
    }
}
