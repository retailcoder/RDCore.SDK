using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// A value representing a fixed-size array.
/// </summary>
public sealed record class VBFixedSizeArrayValue : VBArrayValue
{
    /// <summary>
    /// Creates a new fixed-size array with the specified declared dimensions.
    /// </summary>
    /// <param name="dimensions">An array of </param>
    /// <param name="itemType"></param>
    public VBFixedSizeArrayValue((int uBound, int lBound)[] dimensions, VBType? itemType = null)
        : base(dimensions, itemType ?? VBVariantType.TypeInfo) { }
}
