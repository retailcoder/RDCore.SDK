using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents an <c>Empty</c> value.
/// </summary>
public sealed record class VBEmptyValue() : VBTypedValue(VBEmptyType.TypeInfo), 
    IVBTypedValue<VBEmptyValue, int>
{
    private static readonly Lazy<VBEmptyValue> _emptyValue = new(() => new(), LazyThreadSafetyMode.PublicationOnly);
    public static VBEmptyValue Empty { get; } = _emptyValue.Value;

    public int Value => 0;
    public override int Size => sizeof(int);

    public bool Equals(IVBTypedValue<VBEmptyValue, int>? other) => Value == other?.Value;
    public override int GetHashCode() => Value.GetHashCode();
}
