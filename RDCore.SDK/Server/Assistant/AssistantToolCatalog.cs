using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using RDCore.SDK.Assistant;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RDCore.SDK.Server.Assistant;

/// <summary>
/// The tools a server serves an assistant, by name: what <c>rdcore/tools/list</c> lists and <c>rdcore/tools/call</c> calls.
/// </summary>
/// <remarks>
/// A call is answered with what the tool answers, its text cut at <see cref="ToolResultLimits.TextLength"/> characters. A tool that refuses the call, by
/// throwing an <see cref="AssistantToolRefusedException"/> or an <see cref="RpcErrorException"/>, is answered with a refusal
/// (<see cref="ToolCallResult.Refusal"/>): the call did nothing, and the assistant reads why. A call of a tool the catalog does not have is a JSON-RPC
/// error (<c>-32602</c>), as the <strong>Model Context Protocol</strong> answers it.
/// </remarks>
public sealed partial class AssistantToolCatalog
{
    /// <summary>
    /// JSON-RPC "Invalid params": the code a call of a tool the catalog does not have is refused with.
    /// </summary>
    public const int UnknownToolCode = -32602;

    private readonly Dictionary<string, IAssistantTool> _tools;

    /// <summary>
    /// A catalog of tools.
    /// </summary>
    /// <param name="tools">The tools, in the order the catalog lists them.</param>
    /// <exception cref="ArgumentException">Two tools have the same name, or a name is not one a tool can have (<see cref="IAssistantTool.Name"/>).</exception>
    public AssistantToolCatalog(IEnumerable<IAssistantTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);
        Tools = [.. tools];
        _tools = new(StringComparer.Ordinal);
        foreach (var tool in Tools)
        {
            if (!ToolName().IsMatch(tool.Name))
            {
                throw new ArgumentException($"'{tool.Name}' is not the name of a tool: letters, digits, '_' and '-', at most 64 characters.", nameof(tools));
            }

            if (!_tools.TryAdd(tool.Name, tool))
            {
                throw new ArgumentException($"Two tools are named '{tool.Name}'.", nameof(tools));
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex ToolName();

    /// <summary>
    /// The tools, in the order the catalog lists them.
    /// </summary>
    public IReadOnlyList<IAssistantTool> Tools { get; }

    /// <summary>
    /// Lists the tools (<c>rdcore/tools/list</c>).
    /// </summary>
    /// <param name="culture">The language of the client's user interface.</param>
    /// <returns>The descriptor of each tool, in that language.</returns>
    public ToolsListResult List(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return new() { Tools = [.. Tools.Select(tool => tool.Describe(culture))] };
    }

    /// <summary>
    /// Calls a tool (<c>rdcore/tools/call</c>).
    /// </summary>
    /// <param name="call">The tool, and its arguments.</param>
    /// <param name="token">Cancels the call.</param>
    /// <returns>What the tool answers, or why it refuses the call.</returns>
    /// <exception cref="RpcErrorException">The catalog has no tool by the name the call gives (<see cref="UnknownToolCode"/>).</exception>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task<ToolCallResult> CallAsync(ToolCallParams call, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (!_tools.TryGetValue(call.Name, out var tool))
        {
            throw new RpcErrorException(UnknownToolCode, null!, string.Format(CultureInfo.CurrentUICulture, AssistantMessages.UnknownTool, call.Name));
        }

        ToolCallResult result;
        try
        {
            result = await tool.CallAsync(call.Arguments ?? [], token);
        }
        catch (AssistantToolRefusedException refused)
        {
            return ToolCallResult.Refusal(refused.Message);
        }
        catch (RpcErrorException refused) when (!token.IsCancellationRequested)
        {
            return ToolCallResult.Refusal(refused.Message);
        }

        return result with { Content = [.. result.Content.Select(content => content with { Text = ToolResultLimits.CapText(content.Text, CultureInfo.CurrentUICulture) })] };
    }
}
