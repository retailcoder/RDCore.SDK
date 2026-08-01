using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents a <c>Single</c> numeric value.
/// </summary>
/// <param name="Symbol">The symbol associated with this value.</param>
public sealed record class VBSingleValue() : VBNumericTypedValue(VBSingleType.TypeInfo),
    IVBTypedValue<VBSingleValue, float>,
    INumericValue<VBSingleValue>
{
    public VBSingleValue(float value) : this()
    {
        Handle = new ValueBindingHandle(new VBRuntimeValue<float>(value));
    }

    public float Value => ((VBRuntimeValue<float>)ManagedValue.RuntimeValue!).Value;
    public override int Size => sizeof(float);
    public bool Equals(IVBTypedValue<VBSingleValue, float>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
