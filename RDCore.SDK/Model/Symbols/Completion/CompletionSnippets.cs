using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Formatting;

namespace RDCore.SDK.Model.Symbols.Completion;

/// <summary>
/// The snippets a completion list offers for the keywords that start a block or a procedure, for a client that takes snippets (<strong>LSP 3.17</strong>
/// <c>snippetSupport</c>): the keyword is inserted as the whole block, with placeholders for what is written in it, and the cursor ends in its body.
/// </summary>
/// <remarks>
/// <para>
/// A block's snippet is written as <see cref="Formatting"/> indents the block: its body a level in from its first line, a level being
/// <see cref="CodeFormattingOptions.IndentSpaces"/> spaces; a procedure's body when <see cref="CodeFormattingOptions.IndentEntireProcedureBody"/> says so,
/// and the <c>Case</c> of a <c>Select Case</c> when <see cref="CodeFormattingOptions.IndentCase"/> does. Its lines after the first are indented relative to
/// the first. A client that adjusts the indentation of what it inserts (<strong>LSP 3.17</strong> <c>InsertTextMode.adjustIndentation</c>) adds the caret
/// line's indentation to each of them; for a client that does not, the snippet is written with that indentation in it (<see cref="LineIndentation"/>).
/// </para>
/// <para>
/// The blocks are <c>If</c>, <c>For</c>, <c>For Each</c> (its <c>Each</c>), <c>Do</c> (a <c>Do While</c> or <c>Do Until</c> loop),
/// <c>Select Case</c> (its <c>Select</c>) and <c>With</c>, where a statement starts in a procedure; and <c>Sub</c>, <c>Function</c> and <c>Property</c>
/// (a <c>Property Get</c>), where a declaration starts in the declarations section, after its modifiers when it has any (<see cref="CompletionRules.At"/>).
/// </para>
/// </remarks>
/// <param name="Formatting">The formatting options the snippets are indented by.</param>
/// <param name="LineEnd">The line end written between the lines of a snippet: the one the caret's line ends with.</param>
/// <param name="LineIndentation">
/// The indentation of the caret's line, written at the start of each line of a snippet after its first, for a client that does not adjust the indentation of
/// what it inserts; <see langword="null"/> for a client that does, whose items say <see cref="InsertTextMode.AdjustIndentation"/>.
/// </param>
public sealed record class CompletionSnippets(CodeFormattingOptions Formatting, string LineEnd = "\r\n", string? LineIndentation = null)
{
    /// <summary>
    /// The snippets a completion list at a line offers to a client, by what the client takes and what the user set.
    /// </summary>
    /// <param name="capability">
    /// What the client says of the completion items it takes (<strong>LSP 3.17</strong> <c>CompletionClientCapabilities.completionItem</c>).
    /// </param>
    /// <param name="formatting">The formatting options: none are offered when <see cref="CodeFormattingOptions.Snippets"/> is not set.</param>
    /// <param name="line">The text of the caret's line, whose indentation is written into a snippet for a client that does not adjust it.</param>
    /// <param name="lineEnd">
    /// The line end the caret's line ends with; empty for the last line of the text, which has none, and whose snippets are written with a carriage return and
    /// a line feed.
    /// </param>
    /// <returns>The snippets; <see langword="null"/> when the client takes no snippets, or the user set none.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="formatting"/>, <paramref name="line"/> or <paramref name="lineEnd"/> is <see langword="null"/>.
    /// </exception>
    public static CompletionSnippets? For(CompletionItemCapabilityOptions? capability, CodeFormattingOptions formatting, string line, string lineEnd)
    {
        ArgumentNullException.ThrowIfNull(formatting);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(lineEnd);
        if (!formatting.Snippets || capability is not { SnippetSupport: true })
        {
            return null;
        }

        var adjusts = capability.InsertTextModeSupport?.ValueSet?.Contains(InsertTextMode.AdjustIndentation) == true;
        return new CompletionSnippets(formatting, lineEnd.Length > 0 ? lineEnd : "\r\n",
            adjusts ? null : new string([.. line.TakeWhile(character => character is ' ' or '\t')]));
    }

    /// <summary>
    /// The snippet a keyword is inserted as, as <strong>LSP 3.17</strong> §Snippet Syntax writes it.
    /// </summary>
    /// <param name="keyword">The keyword, as <see cref="CompletionRules.At"/> offers it: <c>If</c>, <c>For</c>, <c>Each</c>, <c>Do</c>, <c>Select</c>,
    /// <c>With</c>, <c>Sub</c>, <c>Function</c> or <c>Property</c>, in any case.</param>
    /// <returns>The snippet, its lines after the first indented as <see cref="LineIndentation"/> says; <see langword="null"/> for any other keyword.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="keyword"/> is <see langword="null"/>.</exception>
    public string? TextOf(string keyword)
    {
        ArgumentNullException.ThrowIfNull(keyword);
        var level = new string(' ', Math.Max(0, Formatting.IndentSpaces));
        var body = Formatting.IndentEntireProcedureBody ? level : string.Empty;
        var clause = Formatting.IndentCase ? level : string.Empty;
        string[]? lines = keyword.ToUpperInvariant() switch
        {
            "IF" => ["If ${1:condition} Then", level + "$0", "End If"],
            "FOR" => ["For ${1:i} = ${2:1} To ${3:10}", level + "$0", "Next"],
            "EACH" => ["Each ${1:item} In ${2:collection}", level + "$0", "Next"],
            "DO" => ["Do ${1|While,Until|} ${2:condition}", level + "$0", "Loop"],
            "SELECT" => ["Select Case ${1:expression}", clause + "Case ${2:value}", clause + level + "$0", "End Select"],
            "WITH" => ["With ${1:object}", level + "$0", "End With"],
            "SUB" => ["Sub ${1:Name}($2)", body + "$0", "End Sub"],
            "FUNCTION" => ["Function ${1:Name}($2) As ${3:Variant}", body + "$0", "End Function"],
            "PROPERTY" => ["Property Get ${1:Name}() As ${2:Variant}", body + "$0", "End Property"],
            _ => null,
        };

        return lines is null ? null : string.Join(LineEnd + LineIndentation, lines);
    }

    // the keyword offered as its snippet, when it has one: what a client shows of it is its first line and its last.
    internal CompletionCandidate? Of(CompletionCandidate keyword)
    {
        if (TextOf(keyword.Label) is not { } text)
        {
            return null;
        }

        var shown = Snippet.Parse(text).Text.Split(LineEnd);
        return keyword with
        {
            InsertText = text,
            IsSnippet = true,
            InsertTextMode = LineIndentation is null ? OmniSharp.Extensions.LanguageServer.Protocol.Models.InsertTextMode.AdjustIndentation : null,
            Detail = $"{shown[0].Trim()} ... {shown[^1].Trim()}",
        };
    }
}
