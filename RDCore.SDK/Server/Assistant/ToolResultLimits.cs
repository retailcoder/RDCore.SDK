using System.Globalization;

namespace RDCore.SDK.Server.Assistant;

/// <summary>
/// How much a call of a tool answers at most: the cap of each kind of list, and of the text. A result cut at a cap says so, and says how many there are.
/// </summary>
/// <remarks>
/// A list beyond its cap is answered with its first items, its total, and <c>truncated: true</c>; its text ends with a line that says how many are shown
/// (<see cref="TruncatedLine{T}"/>). An assistant narrows the call (a path, a query) to see the others.
/// </remarks>
public static class ToolResultLimits
{
    /// <summary>
    /// The most diagnostics a call answers.
    /// </summary>
    public const int Diagnostics = 200;

    /// <summary>
    /// The most symbols a call answers.
    /// </summary>
    public const int Symbols = 500;

    /// <summary>
    /// The most places a call answers: references, definitions.
    /// </summary>
    public const int References = 500;

    /// <summary>
    /// The most entries an outline answers.
    /// </summary>
    public const int OutlineEntries = 1_000;

    /// <summary>
    /// The most characters the text of a result has (<see cref="CapText"/>): 32,768.
    /// </summary>
    public const int TextLength = 32 * 1024;

    /// <summary>
    /// The first items of a list, at most <paramref name="cap"/> of them, and how many there are.
    /// </summary>
    /// <typeparam name="T">The items.</typeparam>
    /// <param name="items">The list, in the order a result has it.</param>
    /// <param name="cap">The most items the result answers.</param>
    /// <returns>The items kept, and the total.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cap"/> is negative.</exception>
    public static CappedList<T> Cap<T>(IEnumerable<T> items, int cap)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegative(cap);

        var kept = new List<T>();
        var total = 0;
        foreach (var item in items)
        {
            if (total++ < cap)
            {
                kept.Add(item);
            }
        }

        return new(kept, total);
    }

    /// <summary>
    /// The line a text ends with when its list is cut at its cap: how many are shown, of how many, and how to see the others.
    /// </summary>
    /// <typeparam name="T">The items.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="culture">The language of the client's user interface.</param>
    /// <returns>The line; <see langword="null"/> for a list that is not cut.</returns>
    public static string? TruncatedLine<T>(CappedList<T> list, CultureInfo culture)
        => list.Truncated ? string.Format(culture, AssistantMessages.ResourceManager.GetString(nameof(AssistantMessages.ListTruncated), culture)!, list.Items.Count, list.Total) : null;

    /// <summary>
    /// A text cut at <see cref="TextLength"/> characters, at the end of its last whole line, with a line that says where it is cut; the text itself when it
    /// is not longer.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="culture">The language of the client's user interface.</param>
    /// <returns>The text, cut.</returns>
    public static string CapText(string text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= TextLength)
        {
            return text;
        }

        var cut = text.LastIndexOf('\n', TextLength - 1);
        var kept = cut > 0 ? text[..cut].TrimEnd('\r') : text[..TextLength];
        var line = string.Format(culture, AssistantMessages.ResourceManager.GetString(nameof(AssistantMessages.TextTruncated), culture)!, kept.Length, text.Length);
        return kept + "\n" + line;
    }
}

/// <summary>
/// The first items of a list, cut at a cap (<see cref="ToolResultLimits.Cap{T}"/>), and how many there are.
/// </summary>
/// <typeparam name="T">The items.</typeparam>
/// <param name="Items">The items kept.</param>
/// <param name="Total">How many there are, those beyond the cap included.</param>
public sealed record class CappedList<T>(IReadOnlyList<T> Items, int Total)
{
    /// <summary>
    /// Whether some items are left out.
    /// </summary>
    public bool Truncated => Items.Count < Total;
}
