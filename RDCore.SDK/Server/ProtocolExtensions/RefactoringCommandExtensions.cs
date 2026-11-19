using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Server.ProtocolExtensions;

/// <summary>
/// Writes and reads each of the <see cref="RefactoringCommands"/> as an OmniSharp <see cref="Command"/>: a server writes the command with its arguments,
/// and an OmniSharp client reads them back. The arguments are the JSON that <see cref="RenameCommandArguments.TryRead"/>,
/// <see cref="ShowReferencesCommandArguments.TryRead"/> and <see cref="GoToLocationCommandArguments.TryRead"/> read, so a client of another library
/// reads them too.
/// </summary>
public static class RefactoringCommandExtensions
{
    /// <summary>
    /// The <see cref="RefactoringCommands.Rename"/> command, with its arguments.
    /// </summary>
    /// <param name="arguments">The URI of the document and the position of the name.</param>
    /// <param name="title">The command's title, which a client shows.</param>
    /// <returns>The command.</returns>
    public static Command ToCommand(this RenameCommandArguments arguments, string title)
        => Written(RefactoringCommands.Rename, title, arguments.ToArguments());

    /// <summary>
    /// The <see cref="RefactoringCommands.ShowReferences"/> command, with its arguments.
    /// </summary>
    /// <param name="arguments">The URI of the document, the position of the member's name, and the places to show.</param>
    /// <param name="title">The command's title, which a client shows: the lens's count, for a code lens.</param>
    /// <returns>The command.</returns>
    public static Command ToCommand(this ShowReferencesCommandArguments arguments, string title)
        => Written(RefactoringCommands.ShowReferences, title, arguments.ToArguments());

    /// <summary>
    /// The <see cref="RefactoringCommands.GoToLocation"/> command, with its arguments.
    /// </summary>
    /// <param name="arguments">The URI of the document and the range to go to.</param>
    /// <param name="title">The command's title, which a client shows.</param>
    /// <returns>The command.</returns>
    public static Command ToCommand(this GoToLocationCommandArguments arguments, string title)
        => Written(RefactoringCommands.GoToLocation, title, arguments.ToArguments());

    /// <summary>
    /// A location as the arguments of the commands give it: the URI exactly as the protocol writes it, and the range.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns>The location of the arguments.</returns>
    public static CommandLocation ToCommandLocation(this Location location) => new(location.Uri.ToString(), location.Range.ToSourceRange());

    /// <summary>
    /// Reads the arguments of a <see cref="RefactoringCommands.Rename"/> command that OmniSharp carries, as <see cref="RenameCommandArguments.TryRead"/> does.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="read">The arguments, when they are read.</param>
    /// <param name="failure">When they cannot be read, what is wrong, in English, for a log; a command of another name is not read.</param>
    /// <returns><see langword="true"/> when the arguments are read.</returns>
    public static bool TryReadRename(this Command command, [NotNullWhen(true)] out RenameCommandArguments? read, [NotNullWhen(false)] out string? failure)
    {
        read = null;
        return IsNamed(command, RefactoringCommands.Rename, out failure) && RenameCommandArguments.TryRead(ElementsOf(command), out read, out failure);
    }

    /// <summary>
    /// Reads the arguments of a <see cref="RefactoringCommands.ShowReferences"/> command that OmniSharp carries, as
    /// <see cref="ShowReferencesCommandArguments.TryRead"/> does.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="read">The arguments, when they are read.</param>
    /// <param name="failure">When they cannot be read, what is wrong, in English, for a log; a command of another name is not read.</param>
    /// <returns><see langword="true"/> when the arguments are read.</returns>
    public static bool TryReadShowReferences(this Command command, [NotNullWhen(true)] out ShowReferencesCommandArguments? read,
        [NotNullWhen(false)] out string? failure)
    {
        read = null;
        return IsNamed(command, RefactoringCommands.ShowReferences, out failure)
            && ShowReferencesCommandArguments.TryRead(ElementsOf(command), out read, out failure);
    }

    /// <summary>
    /// Reads the arguments of a <see cref="RefactoringCommands.GoToLocation"/> command that OmniSharp carries, as
    /// <see cref="GoToLocationCommandArguments.TryRead"/> does.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="read">The arguments, when they are read.</param>
    /// <param name="failure">When they cannot be read, what is wrong, in English, for a log; a command of another name is not read.</param>
    /// <returns><see langword="true"/> when the arguments are read.</returns>
    public static bool TryReadGoToLocation(this Command command, [NotNullWhen(true)] out GoToLocationCommandArguments? read,
        [NotNullWhen(false)] out string? failure)
    {
        read = null;
        return IsNamed(command, RefactoringCommands.GoToLocation, out failure)
            && GoToLocationCommandArguments.TryRead(ElementsOf(command), out read, out failure);
    }

    private static bool IsNamed(Command command, string name, [NotNullWhen(false)] out string? failure)
    {
        failure = command.Name == name ? null : $"{command.Name} is not {name}.";
        return failure is null;
    }

    private static JsonElement[] ElementsOf(Command command)
    {
        using var document = JsonDocument.Parse(command.Arguments?.ToString(Newtonsoft.Json.Formatting.None) ?? "[]");
        return [.. document.RootElement.EnumerateArray().Select(argument => argument.Clone())];
    }

    // the arguments as OmniSharp carries them, read with no date parsing: Newtonsoft only carries the JSON the protocol assembly wrote.
    private static Command Written(string name, string title, IEnumerable<JsonElement> arguments) => new()
    {
        Name = name,
        Title = title,
        Arguments = new JArray(arguments.Select(argument =>
        {
            using var reader = new JsonTextReader(new StringReader(argument.GetRawText())) { DateParseHandling = DateParseHandling.None };
            return JToken.ReadFrom(reader);
        })),
    };
}
