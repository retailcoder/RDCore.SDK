using System.Globalization;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// The text of an <see cref="AnalyzerFinding.Message"/> or a <see cref="RelatedFinding.Message"/>: a localized message with the particulars of one
/// occurrence in it.
/// </summary>
public static class AnalyzerMessages
{
    /// <summary>
    /// A message with the particulars of one occurrence in it, formatted in the culture of the user interface (<see cref="CultureInfo.CurrentUICulture"/>),
    /// the culture its text was looked up in: a number or a date in the message is written as the language of the message writes it.
    /// </summary>
    /// <param name="message">The localized text of the message, with <c>{0}</c>, <c>{1}</c> and so on for the particulars.</param>
    /// <param name="particulars">What the occurrence is about, in the order of their placeholders: usually names.</param>
    /// <returns>The message, its placeholders replaced by the particulars.</returns>
    /// <exception cref="FormatException">The message is not a format string, or has a placeholder with no particular for it.</exception>
    public static string Format(string message, params ReadOnlySpan<object?> particulars)
        => string.Format(CultureInfo.CurrentUICulture, message, particulars);
}
