using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Interop;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents a <c>Long</c> value.
/// </summary>
public sealed record class VBLongValue() : VBNumericTypedValue(VBLongType.TypeInfo),
    IVBTypedValue<VBLongValue, int>,
    INumericValue<VBLongValue>
{
    public VBLongValue(int value) : this()
    {
        ManagedValue = new(new ManagedInteropValue<int>(value));
    }

    public int Value => ((ManagedInteropValue<int>)ManagedValue.InteropValue!).StoredValue;
    public override int Size => sizeof(int);

    public bool Equals(IVBTypedValue<VBLongValue, int>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
