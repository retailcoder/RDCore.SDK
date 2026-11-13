using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// What can be done about a finding, which the platform knows how to do: the fixes an analyzer states with what it found, and which travel with the diagnostic
/// in its <c>data</c> (<strong>LSP 3.17</strong> <c>Diagnostic.data</c>) for the language server to offer as code actions.
/// </summary>
/// <remarks>
/// An analyzer says what the fix is, and the platform makes it: a rename, or another refactoring, is worked out by the language server against the whole
/// workspace when it is asked for, and is offered only when it can be made. An extension states such a fix without knowing how the workspace is changed, and
/// gets the code actions of the platform for it. A fix that changes only the document the finding is in (the edits of one procedure) is the analyzer's to
/// make, from the syntax tree it was given: it states the edits themselves (<see cref="Edits"/>).
/// </remarks>
public sealed record class DiagnosticFixes
{
    /// <summary>
    /// The fix is to rename the name in the range of the diagnostic, which is the name of a declaration; <see langword="null"/> when it is not.
    /// </summary>
    [JsonProperty("rename", NullValueHandling = NullValueHandling.Ignore)]
    public RenameFix? Rename { get; init; }

    /// <summary>
    /// The fixes that are edits of the document the diagnostic is in, which the analyzer made, best first; empty when there are none. Each is offered as a
    /// code action of its own. The first is the one a client may apply without showing it (<strong>LSP 3.17</strong> <c>CodeAction.isPreferred</c>), unless
    /// it changes what the code does (<see cref="EditFix.ChangesBehavior"/>); no other one is.
    /// </summary>
    [JsonProperty("edits")]
    public IReadOnlyList<EditFix> Edits { get; init; } = [];

    /// <summary>
    /// The fix is a refactoring of the declaration or the statements in the range of the diagnostic, which the language server plans against the whole
    /// workspace when it is asked for, as it plans a <see cref="Rename"/>; <see langword="null"/> when it is not.
    /// </summary>
    [JsonProperty("refactoring", NullValueHandling = NullValueHandling.Ignore)]
    public RefactoringFix? Refactoring { get; init; }

    /// <summary>
    /// The fixes a diagnostic carries, when it carries any: a diagnostic whose <c>data</c> is something else - the metadata of an error - has none.
    /// </summary>
    /// <param name="diagnostic">The diagnostic, as a client sent it back with a code action request.</param>
    public static DiagnosticFixes? Of(Diagnostic diagnostic)
    {
        if (diagnostic.Data is not JObject data || (data["rename"] is not JObject && data["edits"] is not JArray && data["refactoring"] is not JObject))
        {
            return null;
        }

        try
        {
            return data.ToObject<DiagnosticFixes>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The fixes, as a diagnostic's <c>data</c>.
    /// </summary>
    public JToken ToData() => JObject.FromObject(this);

    /// <summary>
    /// Whether <see cref="Edits"/> is written in the <c>data</c>: not when it is empty.
    /// </summary>
    public bool ShouldSerializeEdits() => Edits.Count > 0;
}

/// <summary>
/// A rename that fixes a finding: the name in the range of the diagnostic, to a name the client's user chooses, or to one of the names the analyzer suggests.
/// </summary>
public sealed record class RenameFix
{
    /// <summary>
    /// The names the analyzer suggests, best first: <c>Name</c> for <c>strName</c>. Empty when only the user can say what the name should be.
    /// </summary>
    [JsonProperty("suggestions")]
    public IReadOnlyList<string> Suggestions { get; init; } = [];
}

/// <summary>
/// Edits of the document a finding is in, which fix it: what the code action that makes them is called, and the edits, of the version of the document the
/// finding was made of.
/// </summary>
public sealed record class EditFix
{
    /// <summary>
    /// What the code action that makes the edits is called, in the language of the analyzer's messages. The title of a fix that changes what the code does
    /// says what changes.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The edits, none of them overlapping another, as <strong>LSP 3.17</strong> <c>TextEdit</c>s of the document.
    /// </summary>
    [JsonProperty("edits")]
    public IReadOnlyList<TextEdit> Edits { get; init; } = [];

    /// <summary>
    /// Whether the edits change what the code does, as a fix that passes a parameter <c>ByRef</c> or that qualifies a member with <c>Me</c> does. Such a fix
    /// is never one a client may apply without showing it (<strong>LSP 3.17</strong> <c>CodeAction.isPreferred</c>): not in a fix of a whole document, and
    /// not on save.
    /// </summary>
    [JsonProperty("changesBehavior", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ChangesBehavior { get; init; }

    /// <summary>
    /// The version of the document the edits are edits of (<strong>LSP 3.17</strong> <c>VersionedTextDocumentIdentifier.version</c>): the version the finding
    /// was made of (<see cref="DiagnoseDocumentPayload.SourceVersion"/>); <see langword="null"/> when it is not known. The edits are sent with the version, and
    /// a client applies them to that version of the document only; a fix whose version is not known, or is not the document's, is not offered.
    /// </summary>
    [JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
    public int? Version { get; init; }
}

/// <summary>
/// A refactoring that fixes a finding, which the language server plans against the whole workspace when it is asked for: the code action of the refactoring,
/// offered for the range of the diagnostic.
/// </summary>
public sealed record class RefactoringFix
{
    /// <summary>
    /// The refactoring, by the kind of its code action (<see cref="RefactoringKinds"/>).
    /// </summary>
    [JsonProperty("kind")]
    public string Kind { get; init; } = string.Empty;
}
