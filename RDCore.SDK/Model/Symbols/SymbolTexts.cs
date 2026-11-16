using System.Globalization;
using System.Resources;

namespace RDCore.SDK.Model.Symbols;

/// <summary>
/// What the platform says of a symbol where an editor shows it to the reader (a hover, or the documentation of a completion item), in the language of the
/// reader. Each text is formatted with the reader's culture; a culture the platform has no texts for gets the English texts.
/// </summary>
public static class SymbolTexts
{
    private static readonly ResourceManager Messages = new("RDCore.SDK.Semantics.Refactoring.RefactoringMessages", typeof(SymbolTexts).Assembly);

    /// <summary>
    /// What a hover says declares a symbol: its module, procedure, <c>Enum</c> or <c>Type</c>, as in <c>Member of VBA.Interaction</c>.
    /// </summary>
    /// <param name="container">The name of the module, procedure, <c>Enum</c> or <c>Type</c> that declares the symbol, as the hover shows it.</param>
    /// <param name="culture">The language of the reader; the current UI culture by default.</param>
    /// <returns>The text, formatted with <paramref name="culture"/>.</returns>
    public static string MemberOf(string container, CultureInfo? culture = null) => Say("Hover_MemberOf", culture, container);

    /// <summary>
    /// What a hover says a Visual Basic 6.0 control array is (several controls of a form that share one name and are told apart by their <c>Index</c>):
    /// <c>Control array of VB.CommandButton</c>.
    /// </summary>
    /// <param name="controlClass">The qualified name of the class of its controls.</param>
    /// <param name="culture">The language of the reader; the current UI culture by default.</param>
    /// <returns>The text, formatted with <paramref name="culture"/>.</returns>
    public static string ControlArrayOf(string controlClass, CultureInfo? culture = null) => Say("Hover_ControlArrayOf", culture, controlClass);

    /// <summary>
    /// What a hover or a completion item (ahead of its documentation) says of a member RD-VBA adds to a library
    /// (<see cref="Abstract.SymbolProperties.LibraryAddition"/>): <c>RD-VBA addition: the type library of VB does not declare it.</c>
    /// </summary>
    /// <param name="library">The name of the library the member is added to.</param>
    /// <param name="culture">The language of the reader; the current UI culture by default.</param>
    /// <returns>The text, formatted with <paramref name="culture"/>.</returns>
    public static string LibraryAddition(string library, CultureInfo? culture = null) => Say("Symbol_LibraryAddition", culture, library);

    private static string Say(string key, CultureInfo? culture, params object[] arguments)
    {
        culture ??= CultureInfo.CurrentUICulture;
        return string.Format(culture, Messages.GetString(key, culture) ?? key, arguments);
    }
}
