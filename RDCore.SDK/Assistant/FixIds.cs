using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace RDCore.SDK.Assistant;

/// <summary>
/// What a fix is, as its <see cref="ToolFix.FixId"/> says it: enough for the language server to find the same code action again.
/// </summary>
/// <param name="Path">The document the fix was listed for, as the tool's <c>path</c> said it.</param>
/// <param name="Version">
/// The version of the document the fix was listed at; <see langword="null"/> for a document no client has open, which is its file as it is on disk.
/// </param>
/// <param name="Range">The range the code action was asked for, 1-based: the diagnostic's, or the range the tool was given.</param>
/// <param name="Kind">The kind of the code action (<strong>LSP 3.17</strong> <c>CodeActionKind</c>).</param>
/// <param name="Title">The code action's title.</param>
public sealed record class FixIdParts(string Path, int? Version, TextRange Range, string Kind, string Title);

/// <summary>
/// The ids of the fixes the tools list (<see cref="ToolFix.FixId"/>): what the fix is, written in the id, and read back from it.
/// </summary>
/// <remarks>
/// <para>
/// An id holds the fix's document, the document's version, the range, the kind and the title, as base64url (RFC 4648 §5) of a small JSON object. The
/// language server keeps nothing between the call that lists a fix and the call that previews or applies it: it reads the id, asks for the code actions of
/// that range again, and takes the one of that kind and title.
/// </para>
/// <para>
/// When the document's version differs from the id's, or no code action matches, the fix is not found: the document changed after the fixes were listed,
/// and they have to be listed again. An id is not a secret or a capability: it names a fix, which anyone who can list the fixes can list.
/// </para>
/// </remarks>
public static class FixIds
{
    // the names are short: an id is written in every result, and read by a model.
    private sealed record class Written(string P, int? V, int[] R, string K, string T);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The id of a fix.
    /// </summary>
    /// <param name="fix">What the fix is.</param>
    /// <returns>The id: base64url, with no padding.</returns>
    public static string Of(FixIdParts fix)
    {
        ArgumentNullException.ThrowIfNull(fix);
        var range = fix.Range;
        var written = new Written(fix.Path, fix.Version, [range.Start.Line, range.Start.Column, range.End.Line, range.End.Column], fix.Kind, fix.Title);
        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(written, Options));
    }

    /// <summary>
    /// What a fix is, read from its id.
    /// </summary>
    /// <param name="id">The id, as <see cref="Of"/> wrote it.</param>
    /// <param name="fix">What the fix is; <see langword="null"/> when the id is not one.</param>
    /// <returns>Whether <paramref name="id"/> is the id of a fix: <see langword="false"/> for a text that is not base64url, not the JSON an id holds, or
    /// that lacks a part.</returns>
    public static bool TryRead(string? id, [NotNullWhen(true)] out FixIdParts? fix)
    {
        fix = null;
        if (string.IsNullOrEmpty(id) || !Base64Url.IsValid(id))
        {
            return false;
        }

        try
        {
            var written = JsonSerializer.Deserialize<Written>(Base64Url.DecodeFromChars(id), Options);
            if (written is not { P: { } path, R: [var startLine, var startColumn, var endLine, var endColumn], K: { } kind, T: { } title })
            {
                return false;
            }

            fix = new(path, written.V, new TextRange(new(startLine, startColumn), new(endLine, endColumn)), kind, title);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
