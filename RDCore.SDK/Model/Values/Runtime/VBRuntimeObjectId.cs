using System.Globalization;

namespace RDCore.SDK.Model.Values.Runtime;

/// <summary>
/// Represents a <em>runtime object</em>, an <em>instance</em> of a class definition.
/// </summary>
/// <remarks>
/// Each identity made is a number that no other identity of the process has: the next of a counter, which starts at <c>1</c>. The
/// <see langword="default"/> identity, <c>0</c>, is <c>Nothing</c>'s. An identity is compared only within the process that made it.
/// </remarks>
public readonly record struct VBRuntimeObjectId() : IEquatable<VBRuntimeObjectId>
{
    private static long s_last;

    private readonly long _id = Interlocked.Increment(ref s_last);

    public bool Equals(VBRuntimeObjectId other) => _id == other._id;
    public override int GetHashCode() => _id.GetHashCode();
    public override string ToString() => _id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The identity as a number: <c>0</c> for <c>Nothing</c>, and for any object one that no other object of the process has.
    /// </summary>
    public long Identity => _id;
}
