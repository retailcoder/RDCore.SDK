using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Interop;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// A <see cref="VBNumericTypedValue"/> representing a runtime value of the <see cref="VBCurrencyType"/> data type.
/// </summary>
public sealed record class VBCurrencyValue() 
    : VBNumericTypedValue(VBCurrencyType.TypeInfo), IVBTypedValue<VBCurrencyValue, ManagedCurrencyInteropValue>, INumericValue<VBCurrencyValue>
{
    public ManagedCurrencyInteropValue Value => ((ManagedInteropValue<ManagedCurrencyInteropValue>)ManagedValue.InteropValue!).Value;
    public override int Size => sizeof(long);

    public bool Equals(IVBTypedValue<VBCurrencyValue, ManagedCurrencyInteropValue>? other) => Value.StoredValue == other?.Value.StoredValue;
    public override int GetHashCode() => Value.GetHashCode();
}
