using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents a <see cref="VBObjectType"/> value, i.e. an object reference.
/// </summary>
/// <remarks>
/// The default value of a <c>VBObjectValue</c> is <see cref="VBNothingValue"/>.
/// </remarks>
public record class VBObjectValue : VBTypedValue,
    IVBTypedValue<VBObjectValue, object>
{
    private static readonly Lazy<VBObjectValue> _nothing = new(() 
        => new VBNothingValue(), LazyThreadSafetyMode.PublicationOnly);
    public static VBObjectValue Nothing => _nothing.Value;

    public VBObjectValue(object reference) : base(VBObjectType.TypeInfo) 
    {
        Handle = new ReferenceBindingHandle(new VBRuntimeReference(typeof(object), reference));
    }

    public object Value => UnderlyingValue.RuntimeReference!.Value.Value;
    public override int Size => sizeof(int); // not quite

    public bool IsNothing() => Value == Nothing.Value;

    public bool Equals(IVBTypedValue<VBObjectValue, object>? other) => object.ReferenceEquals(Value, other?.Value);
    public override int GetHashCode() => Value.GetHashCode();
}
