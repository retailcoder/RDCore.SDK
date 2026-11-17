using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using RDCore.SDK.Client;

namespace RDCore.SDK.Server.Assistant;

/// <summary>
/// The capability a language server that serves assistant tools declares in its <c>initialize</c> result: under <c>experimental</c>, the key
/// <see cref="RDCorePlatformProtocol.ChatToolsCapability"/>, whose value names the methods that list and call the tools.
/// </summary>
/// <remarks>
/// The value is <c>{"list": "rdcore/tools/list", "call": "rdcore/tools/call"}</c>. It has no <c>context</c> key: the server has no method that gives an
/// assistant its context. <see cref="RDCoreServerApp"/> declares it for a server whose services hold an <see cref="AssistantToolCatalog"/> with a tool in it.
/// </remarks>
public static class AssistantToolsCapability
{
    /// <summary>
    /// Declares the tools of a catalog in a server's capabilities, when it has any.
    /// </summary>
    /// <param name="capabilities">The capabilities of the <c>initialize</c> result.</param>
    /// <param name="catalog">The server's tools, or <see langword="null"/> when it serves none.</param>
    /// <returns>Whether the capability was declared: <see langword="false"/> when there is no catalog, or it has no tool.</returns>
    public static bool Declare(ServerCapabilities capabilities, AssistantToolCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        if (catalog is not { Tools.Count: > 0 })
        {
            return false;
        }

        capabilities.Experimental ??= new Dictionary<string, JToken>();
        capabilities.Experimental[RDCorePlatformProtocol.ChatToolsCapability] = new JObject
        {
            ["list"] = RDCorePlatformProtocol.ToolsList,
            ["call"] = RDCorePlatformProtocol.ToolsCall,
        };
        return true;
    }
}
