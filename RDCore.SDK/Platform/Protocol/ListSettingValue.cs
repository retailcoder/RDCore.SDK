using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Client;

namespace RDCore.SDK.Platform.Protocol;

/// <summary><see cref="ListSettingValueParams"/> as a request of <c>rdcore/settings/listValue</c>, which the language server sends a client.</summary>
[Method(RDCorePlatformProtocol.ListSettingValue, Direction.ServerToClient)]
public sealed record class ListSettingValueRequest : ListSettingValueParams, IRequest<ListSettingValueResult>
{
    /// <summary>A request of no item.</summary>
    public ListSettingValueRequest() { }

    /// <summary>A request of an item of a list.</summary>
    /// <param name="change">The item, the list, and whether it is added or removed.</param>
    public ListSettingValueRequest(ListSettingValueParams change) : base(change) { }
}
