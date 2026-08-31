using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Client;

/// <summary>
/// Describes all core platform capabilities.
/// </summary>
public class CorePlatformClientCapabilities
{
    /// <summary>
    /// The core platform capabilities of the parser process.
    /// </summary>
    [Optional]
    public ParserCapabilities? Parsing { get; set; }
}

/// <summary>
/// Regroups all core platform <c>Parsing</c> capabilities.
/// </summary>
public class ParserCapabilities
{
    /// <summary>
    /// If supported, enables the language server to request a parse result containing the full syntax tree of a specified workspace document.
    /// </summary>
    public ParseFullDocument ParseFullDocument { get; set; } = new();
}

public static class RDCorePlatformProtocol
{
    /// <summary>
    /// Requests an AST from the parser for a full document.
    /// </summary>
    public const string ParseFullDocument = "rdcore/parser/document";
}

[JsonDerivedType(typeof(ParseFullDocument))]
[JsonPolymorphic]
public abstract record class CorePlatformClientCapability(bool IsSupported = true);

/// <summary>
/// Enables the language server to request a parse result containing the full syntax tree of a specified workspace document.
/// </summary>
public record class ParseFullDocument(bool IsSupported = false) : CorePlatformClientCapability(IsSupported);
