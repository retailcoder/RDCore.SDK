using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Interop;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents a <c>Double</c> value.
/// </summary>
public sealed record class VBDoubleValue() : VBNumericTypedValue(VBDoubleType.TypeInfo),
    IVBTypedValue<VBDoubleValue, double>, INumericValue<VBDoubleValue>
{
    public VBDoubleValue(double value) : this()
    {
        ManagedValue = new(new ManagedInteropValue<double>(value));
    }

    public double Value => ((ManagedInteropValue<double>)ManagedValue.InteropValue!).Value;
    public override int Size => 8;

    public bool Equals(IVBTypedValue<VBDoubleValue, double>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
