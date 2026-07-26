using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Interop;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// A <see cref="VBTypedValue"/> representing a runtime value of the <see cref="VBBooleanType"/> data type.
/// </summary>
public sealed record class VBBooleanValue() : 
    VBTypedValue(VBBooleanType.TypeInfo), 
    IVBTypedValue<VBBooleanValue, ManagedBooleanInteropValue>
{
    public VBBooleanValue(bool value) : this()
    {
        Handle = new ValueBindingHandle(new ManagedBooleanInteropValue(value));
    }

    private static readonly Lazy<VBBooleanValue> _falseValue = new(() => new VBBooleanValue { ManagedValue = new(ManagedInteropValue<bool>.BooleanFalse) });
    public static VBBooleanValue False { get; } = _falseValue.Value;

    private static readonly Lazy<VBBooleanValue> _trueValue = new(() => new VBBooleanValue { ManagedValue = new(ManagedInteropValue<bool>.BooleanTrue) });
    public static VBBooleanValue True { get; } = _trueValue.Value;

    public ManagedBooleanInteropValue Value => (ManagedBooleanInteropValue)ManagedValue.InteropValue!;
    public override int Size { get; } = 16; // yes, really.

    public override string ToString() => Value.StoredValue != 0 ? Tokens.True : Tokens.False;

    public bool Equals(IVBTypedValue<VBBooleanValue, ManagedBooleanInteropValue>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();


    // TOOD move to let-coercion semantics:
    // bool -> numeric
    //public VBDoubleValue? AsCoercedDouble(ref int depth) => new VBDoubleValue(Symbol).WithValue(-1 * Convert.ToDouble(Value));
    // bool -> string
    //public VBStringValue? AsCoercedString(ref int depth) => new VBStringValue(Symbol).WithValue(ToString());
    // bool -> string*length
    //public VBFixedStringValue? AsCoercedFixedLengthString(int length, ref int depth) =>
    //    AsCoercedString(ref depth) is VBStringValue value ? new VBFixedStringValue(length).WithFixedValue(value.Value) : null;
}
