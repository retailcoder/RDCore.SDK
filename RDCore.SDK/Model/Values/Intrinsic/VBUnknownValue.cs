using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents the placeholder runtime value of an unresolved symbol.
/// </summary>
public sealed record class VBUnknownValue() : VBTypedValue(VBUnknownType.TypeInfo), IVBTypedValue<VBUnknownValue, object>
{
    private static readonly Lazy<VBUnknownValue> _defaultValue = new(() => new(), LazyThreadSafetyMode.PublicationOnly);
    public static VBUnknownValue DefaultValue => _defaultValue.Value;

    public override int Size => sizeof(int);
    public object Value => UnderlyingValue;

    public bool Equals(IVBTypedValue<VBUnknownValue, object>? other) => false;
}
