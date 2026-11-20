using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// A place in a document, as the arguments of <see cref="RefactoringCommands"/> give it (<strong>LSP 3.17</strong> <c>Location</c>): the document's URI and
/// the range.
/// </summary>
/// <param name="DocumentUri">The URI of the document, exactly as the server wrote it, which is how the server knows the document again.</param>
/// <param name="Range">The range in the document, zero-based (<strong>LSP 3.17</strong> <c>Range</c>).</param>
public readonly record struct CommandLocation(string DocumentUri, SourceRange Range);

/// <summary>
/// The arguments of <see cref="RefactoringCommands.Rename"/>: the URI of the document and the position of the name to rename.
/// </summary>
/// <param name="DocumentUri">The URI of the document, exactly as the server wrote it.</param>
/// <param name="Position">The position of the name, zero-based (<strong>LSP 3.17</strong> <c>Position</c>).</param>
public sealed record class RenameCommandArguments(string DocumentUri, SourcePosition Position)
{
    /// <summary>
    /// Reads the arguments of a <see cref="RefactoringCommands.Rename"/> command: a string and a position. Arguments after these are ignored.
    /// </summary>
    /// <param name="arguments">The command's arguments (<strong>LSP 3.17</strong> <c>Command.arguments</c>), or <see langword="null"/> for none.</param>
    /// <param name="read">The arguments, when they are read.</param>
    /// <param name="failure">
    /// When they cannot be read, what is wrong, in English, for a log: which argument, and which of its properties is missing or invalid.
    /// </param>
    /// <returns><see langword="true"/> when the arguments are read.</returns>
    public static bool TryRead(IReadOnlyList<JsonElement>? arguments, [NotNullWhen(true)] out RenameCommandArguments? read,
        [NotNullWhen(false)] out string? failure)
    {
        read = null;
        if (!CommandArgumentJson.TryCount(RefactoringCommands.Rename, arguments, 2, "the URI of the document and a position", out failure)
            || !CommandArgumentJson.TryReadUri(RefactoringCommands.Rename, arguments, 0, out var uri, out failure)
            || !CommandArgumentJson.TryReadPosition(RefactoringCommands.Rename, arguments[1], "argument 2", out var position, out failure))
        {
            return false;
        }

        read = new RenameCommandArguments(uri, position);
        return true;
    }

    /// <summary>
    /// Writes the arguments as the command carries them: the URI, then the position (<c>{ "line", "character" }</c>).
    /// </summary>
    /// <returns>The arguments, in their order.</returns>
    public JsonElement[] ToArguments() => CommandArgumentJson.Elements(DocumentUri, CommandArgumentJson.Write(Position));
}

/// <summary>
/// The arguments of <see cref="RefactoringCommands.ShowReferences"/>: the URI of the document, the position of the member's name, and the places to show.
/// They are the arguments of VS Code's <c>editor.action.showReferences</c>.
/// </summary>
/// <param name="DocumentUri">The URI of the document, exactly as the server wrote it.</param>
/// <param name="Position">The position of the member's name, zero-based (<strong>LSP 3.17</strong> <c>Position</c>).</param>
/// <param name="Locations">The places to show, in their order, which may be none (<strong>LSP 3.17</strong> <c>Location[]</c>).</param>
public sealed record class ShowReferencesCommandArguments(string DocumentUri, SourcePosition Position, IReadOnlyList<CommandLocation> Locations)
{
    /// <summary>
    /// Reads the arguments of a <see cref="RefactoringCommands.ShowReferences"/> command: a string, a position and an array of locations. Arguments after
    /// these are ignored.
    /// </summary>
    /// <param name="arguments">The command's arguments (<strong>LSP 3.17</strong> <c>Command.arguments</c>), or <see langword="null"/> for none.</param>
    /// <param name="read">The arguments, when they are read.</param>
    /// <param name="failure">
    /// When they cannot be read, what is wrong, in English, for a log: which argument (and which location of the array), and which of its properties is
    /// missing or invalid.
    /// </param>
    /// <returns><see langword="true"/> when the arguments are read.</returns>
    public static bool TryRead(IReadOnlyList<JsonElement>? arguments, [NotNullWhen(true)] out ShowReferencesCommandArguments? read,
        [NotNullWhen(false)] out string? failure)
    {
        const string command = RefactoringCommands.ShowReferences;
        read = null;
        if (!CommandArgumentJson.TryCount(command, arguments, 3, "the URI of the document, a position and the locations", out failure)
            || !CommandArgumentJson.TryReadUri(command, arguments, 0, out var uri, out failure)
            || !CommandArgumentJson.TryReadPosition(command, arguments[1], "argument 2", out var position, out failure))
        {
            return false;
        }

        if (arguments[2].ValueKind != JsonValueKind.Array)
        {
            failure = $"{command}: argument 3 is not an array of locations, but {CommandArgumentJson.KindOf(arguments[2])}.";
            return false;
        }

        var locations = new List<CommandLocation>();
        foreach (var item in arguments[2].EnumerateArray())
        {
            if (!CommandArgumentJson.TryReadLocation(command, item, $"argument 3, location {locations.Count + 1}", out var location, out failure))
            {
                return false;
            }

            locations.Add(location);
        }

        read = new ShowReferencesCommandArguments(uri, position, locations);
        return true;
    }

    /// <summary>
    /// Writes the arguments as the command carries them: the URI, the position (<c>{ "line", "character" }</c>), then the array of locations
    /// (<c>{ "uri", "range": { "start", "end" } }</c>).
    /// </summary>
    /// <returns>The arguments, in their order.</returns>
    public JsonElement[] ToArguments()
        => CommandArgumentJson.Elements(DocumentUri, CommandArgumentJson.Write(Position), new JsonArray([.. Locations.Select(CommandArgumentJson.Write)]));
}

/// <summary>
/// The arguments of <see cref="RefactoringCommands.GoToLocation"/>: the URI of the document and the range to go to.
/// </summary>
/// <param name="DocumentUri">The URI of the document, exactly as the server wrote it.</param>
/// <param name="Range">The range to select, zero-based (<strong>LSP 3.17</strong> <c>Range</c>).</param>
public sealed record class GoToLocationCommandArguments(string DocumentUri, SourceRange Range)
{
    /// <summary>
    /// Reads the arguments of a <see cref="RefactoringCommands.GoToLocation"/> command: a string and a range. Arguments after these are ignored.
    /// </summary>
    /// <param name="arguments">The command's arguments (<strong>LSP 3.17</strong> <c>Command.arguments</c>), or <see langword="null"/> for none.</param>
    /// <param name="read">The arguments, when they are read.</param>
    /// <param name="failure">
    /// When they cannot be read, what is wrong, in English, for a log: which argument, and which of its properties is missing or invalid.
    /// </param>
    /// <returns><see langword="true"/> when the arguments are read.</returns>
    public static bool TryRead(IReadOnlyList<JsonElement>? arguments, [NotNullWhen(true)] out GoToLocationCommandArguments? read,
        [NotNullWhen(false)] out string? failure)
    {
        read = null;
        if (!CommandArgumentJson.TryCount(RefactoringCommands.GoToLocation, arguments, 2, "the URI of the document and a range", out failure)
            || !CommandArgumentJson.TryReadUri(RefactoringCommands.GoToLocation, arguments, 0, out var uri, out failure)
            || !CommandArgumentJson.TryReadRange(RefactoringCommands.GoToLocation, arguments[1], "argument 2", out var range, out failure))
        {
            return false;
        }

        read = new GoToLocationCommandArguments(uri, range);
        return true;
    }

    /// <summary>
    /// Writes the arguments as the command carries them: the URI, then the range (<c>{ "start", "end" }</c>, each <c>{ "line", "character" }</c>).
    /// </summary>
    /// <returns>The arguments, in their order.</returns>
    public JsonElement[] ToArguments() => CommandArgumentJson.Elements(DocumentUri, CommandArgumentJson.Write(Range));
}

// the JSON of the arguments of the refactoring commands: LSP's camelCase positions, ranges and locations.
internal static class CommandArgumentJson
{
    public static bool TryCount(string command, [NotNullWhen(true)] IReadOnlyList<JsonElement>? arguments, int count, string expected,
        [NotNullWhen(false)] out string? failure)
    {
        var given = arguments?.Count ?? 0;
        failure = given >= count ? null : $"{command} takes {count} arguments ({expected}), and was given {given}.";
        return failure is null;
    }

    public static bool TryReadUri(string command, IReadOnlyList<JsonElement> arguments, int index, [NotNullWhen(true)] out string? uri,
        [NotNullWhen(false)] out string? failure)
    {
        uri = arguments[index].ValueKind == JsonValueKind.String ? arguments[index].GetString() : null;
        failure = string.IsNullOrEmpty(uri)
            ? $"{command}: argument {index + 1} is not the URI of a document, but {KindOf(arguments[index])}."
            : null;
        return failure is null;
    }

    public static bool TryReadPosition(string command, JsonElement element, string path, out SourcePosition position, [NotNullWhen(false)] out string? failure)
    {
        position = default;
        if (!TryReadCount(command, element, path, "line", out var line, out failure)
            || !TryReadCount(command, element, path, "character", out var character, out failure))
        {
            return false;
        }

        position = new SourcePosition(line, character);
        return true;
    }

    public static bool TryReadRange(string command, JsonElement element, string path, out SourceRange range, [NotNullWhen(false)] out string? failure)
    {
        range = default;
        if (!TryReadProperty(command, element, path, "start", out var start, out failure)
            || !TryReadPosition(command, start, $"{path}, start", out var from, out failure)
            || !TryReadProperty(command, element, path, "end", out var end, out failure)
            || !TryReadPosition(command, end, $"{path}, end", out var to, out failure))
        {
            return false;
        }

        range = new SourceRange(from, to);
        return true;
    }

    public static bool TryReadLocation(string command, JsonElement element, string path, out CommandLocation location,
        [NotNullWhen(false)] out string? failure)
    {
        location = default;
        if (!TryReadProperty(command, element, path, "uri", out var uri, out failure)
            || !TryReadProperty(command, element, path, "range", out var range, out failure)
            || !TryReadRange(command, range, $"{path}, range", out var read, out failure))
        {
            return false;
        }

        if (uri.ValueKind != JsonValueKind.String || uri.GetString() is not { Length: > 0 } text)
        {
            failure = $"{command}: {path}: 'uri' is not the URI of a document, but {KindOf(uri)}.";
            return false;
        }

        location = new CommandLocation(text, read);
        return true;
    }

    public static string KindOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => element.GetString() is { Length: > 0 } ? "a string" : "an empty string",
        JsonValueKind.Number => $"the number {element.GetRawText()}",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        _ => "nothing",
    };

    public static JsonObject Write(SourcePosition position) => new() { ["line"] = position.Line, ["character"] = position.Character };

    public static JsonObject Write(SourceRange range) => new() { ["start"] = Write(range.Start), ["end"] = Write(range.End) };

    public static JsonObject Write(CommandLocation location) => new() { ["uri"] = location.DocumentUri, ["range"] = Write(location.Range) };

    // the arguments as one JSON array, read back as its elements: each outlives the document it was parsed from.
    public static JsonElement[] Elements(params JsonNode?[] arguments)
    {
        using var document = JsonDocument.Parse(new JsonArray(arguments).ToJsonString());
        return [.. document.RootElement.EnumerateArray().Select(argument => argument.Clone())];
    }

    private static bool TryReadProperty(string command, JsonElement element, string path, string name, out JsonElement property,
        [NotNullWhen(false)] out string? failure)
    {
        property = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            failure = $"{command}: {path} is not an object, but {KindOf(element)}.";
            return false;
        }

        failure = element.TryGetProperty(name, out property) ? null : $"{command}: {path} has no '{name}'.";
        return failure is null;
    }

    // a line or a character: LSP's uinteger, which a position of a document keeps within int.
    private static bool TryReadCount(string command, JsonElement element, string path, string name, out int value, [NotNullWhen(false)] out string? failure)
    {
        value = 0;
        if (!TryReadProperty(command, element, path, name, out var property, out failure))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out value) || value < 0)
        {
            failure = $"{command}: {path}: '{name}' is not an integer of zero or more, but {KindOf(property)}.";
            return false;
        }

        return true;
    }
}
