using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents a <c>LongLong</c> value.
/// </summary>
public sealed record class VBLongLongValue() : VBNumericTypedValue(VBLongLongType.TypeInfo),
    IVBTypedValue<VBLongLongValue, long>, 
    INumericValue<VBLongLongValue>
{
    public VBLongLongValue(long value) : this()
    {
        ManagedValue = new(new VBRuntimeValue<long>(value));
    }

    public long Value => ((VBRuntimeValue<long>)ManagedValue.RuntimeValue!).Value;
    public override int Size => sizeof(long);

    public bool Equals(IVBTypedValue<VBLongLongValue, long>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
