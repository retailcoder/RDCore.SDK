using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.SDK.Model.Values;

/// <summary>
/// Represents a precompiler constant value; treated as an <c>Integer</c>.
/// </summary>
public sealed record class PrecompilerConstantValue : VBIntegerValue
{
    /// <summary>
    /// Creates a new precompiler constant value.
    /// </summary>
    /// <param name="value">The underlying value of this constant.</param>
    public PrecompilerConstantValue(short value) : base(value) { }
}
