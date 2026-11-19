using System.Text.Json.Serialization;

namespace RDCore.SDK.Assistant;

// the arguments and results of the tools that fix code: list_fixes and preview_fix read, apply_fix and rename change the workspace's documents.

/// <summary>
/// The arguments of <see cref="CoreAssistantTools.ListFixes"/>: a document, and optionally a range of it.
/// </summary>
/// <remarks>
/// With no <see cref="Line"/>, the fixes are those of every diagnostic of the document. With a line and a column, they are the fixes of what is at that
/// position, or in the range up to <see cref="EndLine"/> and <see cref="EndColumn"/>: the diagnostics there, and the refactorings of the code there.
/// </remarks>
public sealed record class ListFixesToolArguments
{
    /// <summary>
    /// The document: a path relative to the workspace root, or absolute.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The line the range starts on, from 1; <see langword="null"/> for the whole document.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// The column the range starts at, from 1; <see langword="null"/> for the start of <see cref="Line"/>.
    /// </summary>
    public int? Column { get; init; }

    /// <summary>
    /// The line the range ends on, from 1; <see langword="null"/> for a range that ends on <see cref="Line"/>.
    /// </summary>
    public int? EndLine { get; init; }

    /// <summary>
    /// The column the range ends at, from 1; <see langword="null"/> for a range that ends where it starts when <see cref="EndLine"/> is
    /// <see langword="null"/>, or at the start of <see cref="EndLine"/> otherwise.
    /// </summary>
    public int? EndColumn { get; init; }

    /// <summary>
    /// The range the arguments say, 1-based; <see langword="null"/> for the whole document.
    /// </summary>
    [JsonIgnore]
    public TextRange? Range => Line is not { } line ? null : RangeFrom(new TextPosition(line, Column ?? 1));

    private TextRange RangeFrom(TextPosition start) => EndLine is { } endLine
        ? new(start, new TextPosition(endLine, EndColumn ?? 1))
        : new(start, EndColumn is { } endColumn ? start with { Column = endColumn } : start);
}

/// <summary>
/// What <see cref="CoreAssistantTools.ListFixes"/> answers.
/// </summary>
public sealed record class ListFixesToolResult
{
    /// <summary>
    /// The fixes, in the order the language server offers them, the preferred ones of each diagnostic first; at most as many as the tool's cap.
    /// </summary>
    public IReadOnlyList<ToolFix> Fixes { get; init; } = [];

    /// <summary>
    /// How many fixes there are, those beyond the cap included.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Whether some fixes are left out: a range finds fewer.
    /// </summary>
    public bool Truncated { get; init; }
}

/// <summary>
/// A fix of a document, as <see cref="CoreAssistantTools.ListFixes"/> answers it: a code action of the language server.
/// </summary>
public sealed record class ToolFix
{
    /// <summary>
    /// What names the fix to <see cref="CoreAssistantTools.PreviewFix"/> and <see cref="CoreAssistantTools.ApplyFix"/>, as long as the document is not
    /// changed: the fix itself, which the language server finds again (<c>FixIds</c>, RDCore.SDK), and no handle to a fix it keeps.
    /// </summary>
    public required string FixId { get; init; }

    /// <summary>
    /// What the fix does, in the language of the client's user interface, as an editor's menu shows it.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The kind of code action it is (<strong>LSP 3.17</strong> <c>CodeActionKind</c>): <c>quickfix</c> for a fix of a diagnostic, <c>refactor.extract</c>
    /// and the other <c>refactor</c> kinds for a refactoring.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Whether it is the fix of its diagnostic that an editor applies first (<strong>LSP 3.17</strong> <c>CodeAction.isPreferred</c>).
    /// </summary>
    public bool IsPreferred { get; init; }

    /// <summary>
    /// Whether the fix is an edit of the documents, which <see cref="CoreAssistantTools.PreviewFix"/> shows and <see cref="CoreAssistantTools.ApplyFix"/>
    /// applies. A fix that is not an edit is run by its editor's command (a rename to a name the user types, an import), which the tools do not run.
    /// </summary>
    public bool HasEdit { get; init; }

    /// <summary>
    /// The diagnostic it fixes; <see langword="null"/> for a refactoring, which fixes none.
    /// </summary>
    public ToolDiagnostic? Diagnostic { get; init; }
}

/// <summary>
/// The arguments of <see cref="CoreAssistantTools.PreviewFix"/>: a fix <see cref="CoreAssistantTools.ListFixes"/> listed.
/// </summary>
public record class FixToolArguments
{
    /// <summary>
    /// The fix's <see cref="ToolFix.FixId"/>.
    /// </summary>
    public required string FixId { get; init; }
}

/// <summary>
/// The arguments of <see cref="CoreAssistantTools.ApplyFix"/>: a fix <see cref="CoreAssistantTools.ListFixes"/> listed, and the questions of its edit
/// that are answered yes.
/// </summary>
public sealed record class ApplyFixToolArguments : FixToolArguments
{
    /// <summary>
    /// The <see cref="ToolEditQuestion.Id"/> of each question of the edit that is answered yes (<see cref="EditToolResult.Questions"/>), the others being
    /// answered no; <see langword="null"/> when the call answers none, and an edit that asks a question is then not applied.
    /// </summary>
    public IReadOnlyList<string>? Accept { get; init; }
}

/// <summary>
/// The arguments of <see cref="CoreAssistantTools.Rename"/>: the name at a position, the name it is given, and the questions of the edit that are
/// answered yes.
/// </summary>
public sealed record class RenameToolArguments : PositionToolArguments
{
    /// <summary>
    /// The name the symbol is given.
    /// </summary>
    public required string NewName { get; init; }

    /// <summary>
    /// The <see cref="ToolEditQuestion.Id"/> of each question of the edit that is answered yes (<see cref="EditToolResult.Questions"/>), the others being
    /// answered no; <see langword="null"/> when the call answers none, and an edit that asks a question is then not applied.
    /// </summary>
    public IReadOnlyList<string>? Accept { get; init; }
}

/// <summary>
/// What <see cref="CoreAssistantTools.PreviewFix"/> answers: what the fix would change, and nothing changed.
/// </summary>
public sealed record class PreviewFixToolResult
{
    /// <summary>
    /// What the fix does, as <see cref="ToolFix.Title"/> says it.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The documents the fix changes, each with its change; by path.
    /// </summary>
    public IReadOnlyList<ToolFileChange> Files { get; init; } = [];

    /// <summary>
    /// The questions the edit asks before it is applied; none for an edit that asks nothing. <see cref="CoreAssistantTools.ApplyFix"/> applies none of the
    /// edits a question is of unless it is answered yes.
    /// </summary>
    public IReadOnlyList<ToolEditQuestion> Questions { get; init; } = [];
}

/// <summary>
/// What <see cref="CoreAssistantTools.ApplyFix"/> and <see cref="CoreAssistantTools.Rename"/> answer: the edit applied, or the questions it asks first.
/// </summary>
/// <remarks>
/// An edit that asks a question is not applied by a call that answers none (<see cref="ApplyFixToolArguments.Accept"/>,
/// <see cref="RenameToolArguments.Accept"/> <see langword="null"/>): the result lists its questions, and the tool is called again with the answers. The
/// approval a host asks of its user before it makes the tool's call answers none of these questions: it is given before the questions are known.
/// </remarks>
public sealed record class EditToolResult
{
    /// <summary>
    /// Whether the edit was applied to the documents.
    /// </summary>
    public bool Applied { get; init; }

    /// <summary>
    /// The documents the edit changed, or would change, each with its change; by path.
    /// </summary>
    public IReadOnlyList<ToolFileChange> Files { get; init; } = [];

    /// <summary>
    /// The questions the edit asks that the call did not answer; none when it was applied.
    /// </summary>
    public IReadOnlyList<ToolEditQuestion> Questions { get; init; } = [];
}

/// <summary>
/// A change of a document, as the tools that fix code say it.
/// </summary>
public sealed record class ToolFileChange
{
    /// <summary>
    /// The document: its path relative to the workspace root, with <c>/</c> between folders; absolute for a document outside the root.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The path the document is renamed to, said as <see cref="Path"/> is; <see langword="null"/> when it keeps its path. A rename of a module renames the
    /// file named after it.
    /// </summary>
    public string? NewPath { get; init; }

    /// <summary>
    /// The change of the document's text, as a unified diff (<c>UnifiedDiff</c>, RDCore.SDK); empty when only its path changes.
    /// </summary>
    public string Diff { get; init; } = string.Empty;
}

/// <summary>
/// A question an edit asks before it is applied: whether some of its edits, which may not be wanted, are made (<strong>LSP 3.17</strong>
/// <c>ChangeAnnotation</c>, with <c>needsConfirmation</c>).
/// </summary>
/// <remarks>
/// A rename asks one for the strings a library calls the symbol by, and one for the members accessed late-bound that may be the symbol: answered no, those
/// edits are left out. A rename of a <c>Public</c> procedure of a standard module asks whether something outside the code calls it by its name: answered
/// no, nothing is renamed.
/// </remarks>
public sealed record class ToolEditQuestion
{
    /// <summary>
    /// What names the question in an answer (<see cref="ApplyFixToolArguments.Accept"/>, <see cref="RenameToolArguments.Accept"/>).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The question, in the language of the client's user interface.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// What the question's edits are, in more detail than <see cref="Label"/>; <see langword="null"/> when the label says it all.
    /// </summary>
    public string? Description { get; init; }
}
