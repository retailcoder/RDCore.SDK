using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Assistant;

/// <summary>
/// Asks the language server for the tools it serves an assistant (<c>rdcore/tools/list</c>): sent by a client that expects the <c>AssistantTools</c>
/// capability.
/// </summary>
/// <remarks>
/// The tools and their results are shaped as the <strong>Model Context Protocol</strong> (2025-06-18) shapes them, so that a client that bridges them to an
/// assistant passes them through as they are. The catalog is not paged: one answer lists every tool.
/// </remarks>
public record class ToolsListParams;

/// <summary>
/// The tools the language server serves an assistant, in the language of the client's user interface.
/// </summary>
public record class ToolsListResult
{
    /// <summary>
    /// Each tool, in the order the server lists them.
    /// </summary>
    public IReadOnlyList<ToolDescriptor> Tools { get; init; } = [];
}

/// <summary>
/// What a tool is, as an assistant reads it: its name, what it does, the arguments it takes and what it answers.
/// </summary>
public sealed record class ToolDescriptor
{
    /// <summary>
    /// The name a call gives (<see cref="ToolCallParams.Name"/>): letters, digits, <c>_</c> and <c>-</c>, at most 64 characters, the same in every language.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// A short name for a person to read, in the language of the client's user interface; <see langword="null"/> when the tool has none.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// What the tool does and when to call it, in the language of the client's user interface. Positions in it, in its arguments and in its results are
    /// 1-based lines and columns.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The JSON Schema of the arguments a call gives (<see cref="ToolCallParams.Arguments"/>): an object schema.
    /// </summary>
    public JsonObject InputSchema { get; init; } = new() { ["type"] = "object" };

    /// <summary>
    /// The JSON Schema of the structured result of a call (<see cref="ToolCallResult.StructuredContent"/>); <see langword="null"/> for a tool that answers
    /// with text alone.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? OutputSchema { get; init; }

    /// <summary>
    /// What a call does to the workspace: the hints a client decides by whether to ask its user before a call.
    /// </summary>
    public ToolAnnotations Annotations { get; init; } = new();
}

/// <summary>
/// What a call of a tool does, as hints a client decides by whether to ask its user before it calls it.
/// </summary>
/// <remarks>
/// The hints describe the tool; they guard nothing. A client that applies a tool's edits applies them only once its user approves them.
/// </remarks>
public sealed record class ToolAnnotations
{
    /// <summary>
    /// Whether the tool changes nothing: it only reads the workspace.
    /// </summary>
    public bool ReadOnlyHint { get; init; }

    /// <summary>
    /// Whether a tool that changes something may change what is there already, rather than only add to it: an edit of a document.
    /// </summary>
    public bool DestructiveHint { get; init; }

    /// <summary>
    /// Whether a second call with the same arguments does nothing more than the first.
    /// </summary>
    public bool IdempotentHint { get; init; }

    /// <summary>
    /// Whether the tool reaches beyond the workspace: the network, another program. The language server's tools do not.
    /// </summary>
    public bool OpenWorldHint { get; init; }
}

/// <summary>
/// Calls a tool the language server serves an assistant (<c>rdcore/tools/call</c>).
/// </summary>
/// <remarks>
/// A tool that is not in the catalog is refused with the JSON-RPC error <c>-32602</c>. A call the tool itself refuses, arguments it cannot read
/// included, is answered with a result whose <see cref="ToolCallResult.IsError"/> is <see langword="true"/>. <c>$/cancelRequest</c> cancels the call.
/// </remarks>
public record class ToolCallParams
{
    /// <summary>
    /// The name of the tool (<see cref="ToolDescriptor.Name"/>).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The arguments, as the tool's <see cref="ToolDescriptor.InputSchema"/> describes them; <see langword="null"/> for none.
    /// </summary>
    public JsonObject? Arguments { get; init; }
}

/// <summary>
/// What a call of a tool answers: a text for an assistant to read, and the same result as structured data where the tool has an
/// <see cref="ToolDescriptor.OutputSchema"/>.
/// </summary>
/// <remarks>
/// Text in a result that comes from the workspace (comments, strings, names) is data the code holds, and a result says it as such.
/// </remarks>
public record class ToolCallResult
{
    /// <summary>
    /// What the call answers, for an assistant to read.
    /// </summary>
    public IReadOnlyList<ToolContent> Content { get; init; } = [];

    /// <summary>
    /// The result as data, as the tool's <see cref="ToolDescriptor.OutputSchema"/> describes it; <see langword="null"/> for a result in text alone, and for a
    /// refusal.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonObject? StructuredContent { get; init; }

    /// <summary>
    /// Whether the tool refused the call: <see cref="Content"/> says why.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// A result in text alone.
    /// </summary>
    /// <param name="text">What the call answers.</param>
    /// <param name="structured">The same result as data; <see langword="null"/> for none.</param>
    /// <returns>The result.</returns>
    public static ToolCallResult Of(string text, JsonObject? structured = null) => new() { Content = [ToolContent.Of(text)], StructuredContent = structured };

    /// <summary>
    /// A refusal: the call did nothing, for the reason given.
    /// </summary>
    /// <param name="reason">Why, for an assistant to read.</param>
    /// <returns>The result.</returns>
    public static ToolCallResult Refusal(string reason) => new() { Content = [ToolContent.Of(reason)], IsError = true };
}

/// <summary>
/// One part of what a call of a tool answers: a text.
/// </summary>
public sealed record class ToolContent
{
    /// <summary>
    /// The kind of the part: <c>text</c>.
    /// </summary>
    public string Type { get; init; } = TextType;

    /// <summary>
    /// The text.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// The kind of a part that is a text (<see cref="Type"/>).
    /// </summary>
    public const string TextType = "text";

    /// <summary>
    /// A part that is a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The part.</returns>
    public static ToolContent Of(string text) => new() { Text = text };
}
