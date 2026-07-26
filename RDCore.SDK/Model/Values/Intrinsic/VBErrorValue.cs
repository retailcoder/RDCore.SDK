using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents an <c>Error</c> value.
/// </summary>
/// <param name="Value">The numeric underlying value.</param>
public sealed record class VBErrorValue(int Value = 0) : VBTypedValue(VBErrorType.TypeInfo),
    IVBTypedValue<VBErrorValue, int>
{
    public override int Size => sizeof(int);

    public bool Equals(IVBTypedValue<VBErrorValue, int>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();
}