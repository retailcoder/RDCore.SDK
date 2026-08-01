using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// A <see cref="VBNumericTypedValue"/> representing a runtime value of the <see cref="VBCurrencyType"/> data type.
/// </summary>
public sealed record class VBCurrencyValue() 
    : VBNumericTypedValue(VBCurrencyType.TypeInfo), IVBTypedValue<VBCurrencyValue, VBRuntimeCurrencyValue>, INumericValue<VBCurrencyValue>
{
    public VBRuntimeCurrencyValue Value => ((VBRuntimeValue<VBRuntimeCurrencyValue>)ManagedValue.RuntimeValue!).Value;
    public override int Size => sizeof(long);

    public bool Equals(IVBTypedValue<VBCurrencyValue, VBRuntimeCurrencyValue>? other) => Value.StoredValue == other?.Value.StoredValue;
    public override int GetHashCode() => Value.GetHashCode();
}
