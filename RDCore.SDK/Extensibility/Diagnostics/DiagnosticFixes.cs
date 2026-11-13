using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace RDCore.SDK.Extensibility.Diagnostics;

/// <summary>
/// What can be done about a finding, which the platform knows how to do: the fixes an analyzer states with what it found, and which travel with the diagnostic
/// in its <c>data</c> (<strong>LSP 3.17</strong> <c>Diagnostic.data</c>) for the language server to offer as code actions.
/// </summary>
/// <remarks>
/// An analyzer says what the fix is, and the platform makes it: a rename is planned by the language server against the whole workspace when it is asked for
/// (<c>SymbolRename</c>), and is offered only when it can be made. An extension states a fix without knowing how the workspace
/// is renamed, and gets the code actions of the platform for it. A fix that changes only the document the finding is in - the edits of one procedure - is the
/// analyzer's to make, from the syntax tree it was given: it states the edits themselves (<see cref="Edit"/>).
/// </remarks>
public sealed record class DiagnosticFixes
{
    /// <summary>
    /// The fix is to rename the name in the range of the diagnostic, which is the name of a declaration; <see langword="null"/> when it is not.
    /// </summary>
    [JsonProperty("rename", NullValueHandling = NullValueHandling.Ignore)]
    public RenameFix? Rename { get; init; }

    /// <summary>
    /// The fix is a set of edits of the document the diagnostic is in, which the analyzer made; <see langword="null"/> when it is not.
    /// </summary>
    [JsonProperty("edit", NullValueHandling = NullValueHandling.Ignore)]
    public EditFix? Edit { get; init; }

    /// <summary>
    /// The fix is to add an item to the list of a setting - a name the analyzer is to allow; <see langword="null"/> when it is not.
    /// </summary>
    [JsonProperty("setting", NullValueHandling = NullValueHandling.Ignore)]
    public SettingFix? Setting { get; init; }

    /// <summary>
    /// The fixes a diagnostic carries, when it carries any: a diagnostic whose <c>data</c> is something else - the metadata of an error - has none.
    /// </summary>
    /// <param name="diagnostic">The diagnostic, as a client sent it back with a code action request.</param>
    public static DiagnosticFixes? Of(Diagnostic diagnostic)
    {
        if (diagnostic.Data is not JObject data || (data["rename"] is not JObject && data["edit"] is not JObject && data["setting"] is not JObject))
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

    /// <summary>The fixes, as a diagnostic's <c>data</c>.</summary>
    public JToken ToData() => JObject.FromObject(this);
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
    /// <summary>What the code action that makes the edits is called, in the language of the analyzer's messages.</summary>
    [JsonProperty("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>The edits, none of them overlapping another, as <strong>LSP 3.17</strong> <c>TextEdit</c>s of the document.</summary>
    [JsonProperty("edits")]
    public IReadOnlyList<TextEdit> Edits { get; init; } = [];
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
