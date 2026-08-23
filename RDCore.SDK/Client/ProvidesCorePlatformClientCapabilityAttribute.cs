namespace RDCore.SDK.Client;

/// <summary>
/// Specifies the supported core platform capabilities of this assembly.
/// </summary>
/// <typeparam name="TCapability"></typeparam>
[AttributeUsage(AttributeTargets.Assembly)]
public class ProvidesCorePlatformClientCapabilityAttribute<TCapability> : Attribute
    where TCapability : CorePlatformClientCapability
{ }
