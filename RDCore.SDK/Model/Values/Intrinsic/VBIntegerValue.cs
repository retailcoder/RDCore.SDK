using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents an <c>Integer</c> value
/// </summary>
public record class VBIntegerValue() : VBNumericTypedValue(VBIntegerType.TypeInfo),
    IVBTypedValue<VBIntegerValue, short>,
    INumericValue<VBIntegerValue>
{
    public VBIntegerValue(short value) : this()
    {
        UnderlyingValue = new(new VBRuntimeValue<short>(value));
    }

    public short Value => ((VBRuntimeValue<short>)UnderlyingValue.RuntimeValue!).StoredValue;
    public override int Size { get; } = sizeof(short);

    public bool Equals(IVBTypedValue<VBIntegerValue, short>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
