using System.Text.Json;

namespace RDCore.SDK.Workspace;

/// <summary>
/// Where a <see cref="JsonException"/> places what is wrong in the text it was thrown for, in the lines and columns an editor shows.
/// </summary>
public static class JsonErrorPlace
{
    /// <summary>
    /// The line and the column of what <paramref name="exception"/> found wrong in <paramref name="text"/>, each counted from 1. The column counts UTF-16
    /// characters, as an editor does, where <see cref="JsonException.BytePositionInLine"/> counts the UTF-8 bytes of the line: an accented letter is one
    /// column, and two bytes. Lines end at <c>\n</c>, as the reader ends them.
    /// </summary>
    /// <param name="exception">The exception the JSON reader or serializer threw.</param>
    /// <param name="text">
    /// The text that was read, without its byte order mark; <see langword="null"/> when it is not known. When it is <see langword="null"/>, or has no line
    /// the exception names, the column is the reader's count of bytes, counted from 1.
    /// </param>
    /// <returns>
    /// The line and the column; <see langword="null"/> when <paramref name="exception"/> places nothing (its <see cref="JsonException.LineNumber"/> is
    /// <see langword="null"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static (long Line, long Column)? PlaceIn(this JsonException exception, string? text)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception.LineNumber is not { } line)
        {
            return null;
        }

        var bytes = exception.BytePositionInLine ?? 0;
        return (line + 1, CharactersBefore(text, line, bytes) + 1);
    }

    // the characters before a position given in UTF-8 bytes from the start of a line counted from 0; the bytes themselves where the text has no such line.
    private static long CharactersBefore(string? text, long line, long bytes)
    {
        if (text is null)
        {
            return bytes;
        }

        var start = 0;
        for (var at = 0L; at < line; at++)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                return bytes;
            }

            start = end + 1;
        }

        var stop = text.IndexOf('\n', start);
        var counted = 0L;
        var characters = 0L;
        foreach (var rune in text.AsSpan(start, (stop < 0 ? text.Length : stop) - start).EnumerateRunes())
        {
            if (counted >= bytes)
            {
                break;
            }

            counted += rune.Utf8SequenceLength;
            characters += rune.Utf16SequenceLength;
        }

        return characters;
    }
}
