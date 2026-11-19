using System.Text.Json.Serialization;

namespace RDCore.SDK.Assistant;

/// <summary>
/// The names of the tools the language server serves an assistant (<see cref="ToolDescriptor.Name"/>), with the arguments each takes and the structured
/// result each answers with in this namespace.
/// </summary>
public static class CoreAssistantTools
{
    /// <summary>
    /// The diagnostics of a document, or of every document of the workspace: <see cref="DiagnosticsToolArguments"/>, answered with a
    /// <see cref="DiagnosticsToolResult"/>.
    /// </summary>
    public const string GetDiagnostics = "get_diagnostics";

    /// <summary>
    /// The symbols of the workspace whose name matches a query, as <c>workspace/symbol</c> finds them: <see cref="FindSymbolToolArguments"/>, answered with
    /// a <see cref="SymbolsToolResult"/>.
    /// </summary>
    public const string FindSymbol = "find_symbol";

    /// <summary>
    /// The names that refer to the symbol named at a position, as <c>textDocument/references</c> finds them: <see cref="ReferencesToolArguments"/>, answered
    /// with a <see cref="LocationsToolResult"/>.
    /// </summary>
    public const string References = "references";

    /// <summary>
    /// Where the symbol named at a position is defined, as <c>textDocument/definition</c> finds it: <see cref="PositionToolArguments"/>, answered with a
    /// <see cref="LocationsToolResult"/>.
    /// </summary>
    public const string Definition = "definition";

    /// <summary>
    /// What the name or expression written at a position is, as <c>rdcore/semantics/describe</c> describes it: <see cref="PositionToolArguments"/>,
    /// answered with a <c>DescribeToolResult</c> (RDCore.SDK).
    /// </summary>
    public const string DescribeAt = "describe_at";

    /// <summary>
    /// The declarations of a document's module, as <c>rdcore/semantics/outline</c> outlines it: <see cref="OutlineToolArguments"/>, answered with an
    /// <c>OutlineToolResult</c> (RDCore.SDK).
    /// </summary>
    public const string Outline = "outline";
}

/// <summary>
/// The arguments of <see cref="CoreAssistantTools.Outline"/>.
/// </summary>
public sealed record class OutlineToolArguments
{
    /// <summary>
    /// The document: a path relative to the workspace root, or absolute.
    /// </summary>
    public required string Path { get; init; }
}

/// <summary>
/// The arguments of <see cref="CoreAssistantTools.GetDiagnostics"/>.
/// </summary>
public sealed record class DiagnosticsToolArguments
{
    /// <summary>
    /// The document: a path relative to the workspace root, or absolute; <see langword="null"/> for every document of the workspace.
    /// </summary>
    public string? Path { get; init; }
}

/// <summary>
/// The arguments of <see cref="CoreAssistantTools.FindSymbol"/>.
/// </summary>
public sealed record class FindSymbolToolArguments
{
    /// <summary>
    /// What the names match: the characters of the query, in order and in any case (<c>gtnm</c> matches <c>GetName</c>).
    /// </summary>
    public required string Query { get; init; }
}

/// <summary>
/// The arguments of a tool that is asked about the name at a position of a document: <see cref="CoreAssistantTools.Definition"/>.
/// </summary>
public record class PositionToolArguments
{
    /// <summary>
    /// The document: a path relative to the workspace root, or absolute.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The line of the position, from 1.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// The column of the position, from 1, anywhere in the name.
    /// </summary>
    public required int Column { get; init; }

    /// <summary>
    /// The position, 1-based.
    /// </summary>
    [JsonIgnore]
    public TextPosition Position => new(Line, Column);
}

/// <summary>
/// The arguments of <see cref="CoreAssistantTools.References"/>.
/// </summary>
public sealed record class ReferencesToolArguments : PositionToolArguments
{
    /// <summary>
    /// Whether the names the symbol's declarations declare are references too; <see langword="true"/> when the call does not say.
    /// </summary>
    public bool IncludeDeclaration { get; init; } = true;
}

/// <summary>
/// How severe a diagnostic is, as LSP grades it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolDiagnosticSeverity>))]
public enum ToolDiagnosticSeverity
{
    /// <summary>
    /// The code does not compile, or fails when it runs.
    /// </summary>
    Error,

    /// <summary>
    /// The code compiles, and is likely to be wrong.
    /// </summary>
    Warning,

    /// <summary>
    /// Something the user may want to know of the code.
    /// </summary>
    Information,

    /// <summary>
    /// A suggestion, which an editor shows the least of.
    /// </summary>
    Hint,
}

/// <summary>
/// What <see cref="CoreAssistantTools.GetDiagnostics"/> answers.
/// </summary>
public sealed record class DiagnosticsToolResult
{
    /// <summary>
    /// The diagnostics, by document, then by position; at most as many as the tool's cap.
    /// </summary>
    public IReadOnlyList<ToolDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>
    /// How many diagnostics there are, those beyond the cap included.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Whether some diagnostics are left out (<see cref="Total"/> is more than <see cref="Diagnostics"/> has): a call for one document lists them all.
    /// </summary>
    public bool Truncated { get; init; }
}

/// <summary>
/// A diagnostic of a document, as <see cref="CoreAssistantTools.GetDiagnostics"/> answers it.
/// </summary>
public sealed record class ToolDiagnostic
{
    /// <summary>
    /// Where the diagnostic is.
    /// </summary>
    public required TextLocation Location { get; init; }

    /// <summary>
    /// The diagnostic's code (<c>RDC01011</c>, <c>VBC00013</c>).
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// How severe it is.
    /// </summary>
    public ToolDiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// What it says, in the language of the client's user interface.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The address of the page that documents the code; <see langword="null"/> for a code that has none.
    /// </summary>
    public string? HelpUrl { get; init; }

    /// <summary>
    /// Whether a code action fixes it.
    /// </summary>
    public bool HasFix { get; init; }
}

/// <summary>
/// What <see cref="CoreAssistantTools.FindSymbol"/> answers.
/// </summary>
public sealed record class SymbolsToolResult
{
    /// <summary>
    /// The symbols, the best matches first; at most as many as the tool's cap.
    /// </summary>
    public IReadOnlyList<ToolSymbol> Symbols { get; init; } = [];

    /// <summary>
    /// How many symbols match, those beyond the cap included.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Whether some symbols are left out: a longer query finds fewer.
    /// </summary>
    public bool Truncated { get; init; }
}

/// <summary>
/// A symbol of the workspace, as <see cref="CoreAssistantTools.FindSymbol"/> answers it.
/// </summary>
public sealed record class ToolSymbol
{
    /// <summary>
    /// Its name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// What it is, as LSP names the kind of a symbol (<c>Function</c>, <c>Class</c>, <c>Variable</c>).
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// The name of what declares it: its module, or for a member of a <c>Type</c> or an <c>Enum</c>, the type; <see langword="null"/> for a module.
    /// </summary>
    public string? Container { get; init; }

    /// <summary>
    /// Where its name is declared.
    /// </summary>
    public required TextLocation Location { get; init; }
}

/// <summary>
/// What <see cref="CoreAssistantTools.References"/> and <see cref="CoreAssistantTools.Definition"/> answer: places of the workspace.
/// </summary>
public sealed record class LocationsToolResult
{
    /// <summary>
    /// Each place, by document, then by position; at most as many as the tool's cap. None where no name is written at the position, or where what the name
    /// refers to is not known.
    /// </summary>
    public IReadOnlyList<TextLocation> Locations { get; init; } = [];

    /// <summary>
    /// How many places there are, those beyond the cap included.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Whether some places are left out.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// Why the places may be incomplete, in the language of the client's user interface: a name whose meaning is not known (a member accessed late-bound)
    /// could refer to the symbol too. <see langword="null"/> when every reference is known.
    /// </summary>
    public string? Incomplete { get; init; }
}
