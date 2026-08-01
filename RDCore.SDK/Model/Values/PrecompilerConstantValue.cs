using RDCore.SDK.Model.Values.Runtime;
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
    /// <param name="managedValue">The underlying managed value of this constant.</param>
    public PrecompilerConstantValue(int managedValue)
        : base()
    {
        ManagedValue = new(new VBRuntimeValue<int>(managedValue));
    }
}
