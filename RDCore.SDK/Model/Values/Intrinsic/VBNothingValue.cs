using RDCore.SDK.Runtime.Shared;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents the <c>Nothing</c> <see cref="VBObjectValue"/> literal.
/// </summary>
public sealed record class VBNothingValue() : VBObjectValue(MemoryAddress.Zero) { }