using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Semantics.Refactoring;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// What can be done about a finding, which the platform knows how to do: the fixes an analyzer states with what it found, and which travel with the diagnostic
/// in its <c>data</c> (<strong>LSP 3.17</strong> <c>Diagnostic.data</c>) for the language server to offer as code actions.
/// </summary>
/// <remarks>
/// An analyzer says what the fix is, and the platform makes it: a rename, or another refactoring, is worked out by the language server against the whole
/// workspace when it is asked for, and is offered only when it can be made. An extension states such a fix without knowing how the workspace is changed, and
/// gets the code actions of the platform for it. A fix that changes only the document the finding is in (the edits of one procedure) is the analyzer's to
/// make, from the syntax tree it was given: it states the edits themselves (<see cref="Edits"/>). A fix that changes what the project file says (a library
/// the project references, or what it says of the module) is a change the analyzer states, and the language server makes (<see cref="Project"/>).
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
    /// The fix is a change of the project: what its project file is to say, which the language server writes when the fix is taken
    /// (<see cref="ServerCommands.ChangeProject"/>); <see langword="null"/> when it is not.
    /// </summary>
    [JsonProperty("project", NullValueHandling = NullValueHandling.Ignore)]
    public ProjectFix? Project { get; init; }

    /// <summary>
    /// The fix is to add an item to the list of a setting, such as a name the analyzer is to allow; <see langword="null"/> when it is not.
    /// </summary>
    [JsonProperty("setting", NullValueHandling = NullValueHandling.Ignore)]
    public SettingFix? Setting { get; init; }

    /// <summary>
    /// The fixes a diagnostic carries, when it carries any: a diagnostic whose <c>data</c> is something else - the metadata of an error - has none.
    /// </summary>
    /// <param name="diagnostic">The diagnostic, as a client sent it back with a code action request.</param>
    public static DiagnosticFixes? Of(Diagnostic diagnostic)
    {
        if (diagnostic.Data is not JObject data || (data["rename"] is not JObject && data["edits"] is not JArray && data["refactoring"] is not JObject
            && data["project"] is not JObject && data["setting"] is not JObject))
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
    /// The fixes, with the version of the document they were made of on each edit fix that does not state one (<see cref="EditFix.Version"/>). An
    /// analyzer is not told the version of the document it reads: what answers <see cref="RDCoreExtensionProtocol.DiagnoseDocument"/> stamps its
    /// findings' fixes with <see cref="DiagnoseDocumentPayload.SourceVersion"/>.
    /// </summary>
    /// <param name="sourceVersion">The version of the document the findings were made of; <see langword="null"/> when it is not known.</param>
    public DiagnosticFixes AtVersion(int? sourceVersion)
        => sourceVersion is null || Edits.All(fix => fix.Version is not null)
            ? this
            : this with { Edits = [.. Edits.Select(fix => fix.Version is null ? fix with { Version = sourceVersion } : fix)] };

    /// <summary>
    /// The fixes, as a diagnostic's <c>data</c>.
    /// </summary>
    public JToken ToData() => JObject.FromObject(this);

    /// <summary>
    /// Whether <see cref="Edits"/> is written in the <c>data</c>: not when it is empty.
    /// </summary>
    public bool ShouldSerializeEdits() => Edits.Count > 0;

    /// <summary>
    /// The edits that fix every diagnostic of a code in one document at once, as a fix of the whole document makes them: of each diagnostic, in the order of
    /// their ranges, the first of its <see cref="Edits"/>, when it was made of the given version of the document, keeps what the code does
    /// (<see cref="EditFix.ChangesBehavior"/>), has edits, and none of them overlaps an edit taken before it. A diagnostic whose fix is not taken is left for
    /// a fix of its own.
    /// </summary>
    /// <remarks>
    /// Two edits overlap when their ranges share a character, or when they start at the same position: the order of two insertions at one position would be
    /// the client's.
    /// </remarks>
    /// <param name="diagnostics">The diagnostics of the document, of any code; those of other codes are not fixed.</param>
    /// <param name="code">The code whose diagnostics are fixed.</param>
    /// <param name="version">The version of the document the edits are made of (<see cref="EditFix.Version"/>).</param>
    /// <returns>The diagnostics whose fixes are taken, and the edits of those fixes, none of them overlapping another.</returns>
    public static (IReadOnlyList<Diagnostic> Fixed, IReadOnlyList<TextEdit> Edits) OfEvery(
        IEnumerable<Diagnostic> diagnostics, RDCoreDiagnosticCode code, int version)
    {
        var fixedOnes = new List<Diagnostic>();
        var taken = new List<TextEdit>();
        foreach (var diagnostic in diagnostics
            .Where(diagnostic => RDCoreDiagnosticCode.TryParse(diagnostic.Code?.String, out var its) && its == code)
            .OrderBy(diagnostic => diagnostic.Range.Start.Line).ThenBy(diagnostic => diagnostic.Range.Start.Character))
        {
            if (Of(diagnostic)?.Edits is not [{ ChangesBehavior: false, Edits.Count: > 0 } fix, ..] || fix.Version != version
                || fix.Edits.Any(edit => taken.Any(other => Overlap(edit.Range, other.Range))))
            {
                continue;
            }

            fixedOnes.Add(diagnostic);
            taken.AddRange(fix.Edits);
        }

        return (fixedOnes, taken);
    }

    private static bool Overlap(Range one, Range other)
        => Before(one.Start, other.End) && Before(other.Start, one.End) || one.Start.Line == other.Start.Line && one.Start.Character == other.Start.Character;

    private static bool Before(Position one, Position other) => one.Line < other.Line || one.Line == other.Line && one.Character < other.Character;
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

/// <summary>
/// An item added to the list of a setting, which fixes a finding: the name the finding is about, added to the names the analyzer allows
/// (<c>Diagnostics.MeaningfulNames.AllowedNames</c>). The client keeps the list (<c>rdcore/settings/listValue</c>); the platform offers the fix only to a client
/// that expects to be asked to.
/// </summary>
public sealed record class SettingFix
{
    /// <summary>
    /// What the code action that adds the item is called, in the language of the analyzer's messages: <c>Allow the name 'i'</c>.
    /// </summary>
    [JsonProperty("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The key of the setting, under <c>Configuration</c>: <c>Diagnostics.MeaningfulNames.AllowedNames</c>.
    /// </summary>
    [JsonProperty("key")]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// The item, as it is written: <c>i</c>.
    /// </summary>
    [JsonProperty("value")]
    public string Value { get; init; } = string.Empty;
}
