using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// A <see cref="VBNumericTypedValue"/> representing a runtime value of the <see cref="VBDecimalType"/> data type.
/// </summary>
public sealed record class VBDecimalValue() 
    : VBNumericTypedValue(VBDecimalType.TypeInfo), IVBTypedValue<VBDecimalValue, decimal>, INumericValue<VBDecimalValue>
{
    public decimal Value => ((VBRuntimeValue<VBRuntimeDecimalValue>)UnderlyingValue.RuntimeValue!).Value.ManagedValue;
    public override int Size => sizeof(Decimal);

    public bool Equals(IVBTypedValue<VBDecimalValue, decimal>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
