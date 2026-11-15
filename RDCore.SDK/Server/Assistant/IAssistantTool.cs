using RDCore.SDK.Assistant;
using System.Globalization;
using System.Text.Json.Nodes;

namespace RDCore.SDK.Server.Assistant;

/// <summary>
/// A tool a server serves an assistant: what it is (<see cref="Describe"/>), and what a call of it answers (<see cref="CallAsync"/>).
/// </summary>
/// <remarks>
/// <para>
/// The language server lists and calls the tools of its <see cref="AssistantToolCatalog"/> over <c>rdcore/tools/list</c> and <c>rdcore/tools/call</c>.
/// <see cref="AssistantTool{TArguments, TResult}"/> implements this interface for a tool whose arguments and result are records, and derives their schemas
/// from them.
/// </para>
/// <para>
/// A tool reads the workspace, or makes an edit that the client applies. It never writes a file of the workspace itself.
/// </para>
/// </remarks>
public interface IAssistantTool
{
    /// <summary>
    /// The name a call gives (<see cref="ToolDescriptor.Name"/>): letters, digits, <c>_</c> and <c>-</c>, at most 64 characters, the same in every language.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// What the tool is, in a language: its description, the schema of its arguments and of its result, and its annotations.
    /// </summary>
    /// <param name="culture">The language of the client's user interface.</param>
    /// <returns>The descriptor, whose <see cref="ToolDescriptor.Name"/> is <see cref="Name"/>.</returns>
    ToolDescriptor Describe(CultureInfo culture);

    /// <summary>
    /// Calls the tool.
    /// </summary>
    /// <param name="arguments">The arguments of the call, as the tool's input schema describes them; an empty object for none.</param>
    /// <param name="token">Cancels the call: <c>$/cancelRequest</c> of the request that made it.</param>
    /// <returns>What the tool answers.</returns>
    /// <exception cref="AssistantToolRefusedException">The tool refuses the call: the arguments are not valid, or what they name cannot be done.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    Task<ToolCallResult> CallAsync(JsonObject arguments, CancellationToken token);
}

/// <summary>
/// Thrown by a tool that refuses a call: the catalog answers it with a result whose <see cref="ToolCallResult.IsError"/> is <see langword="true"/>, and
/// whose text is the exception's message.
/// </summary>
/// <param name="message">Why the call is refused, in the language of the client's user interface, for an assistant to read.</param>
public sealed class AssistantToolRefusedException(string message) : Exception(message);
