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

    /// <summary>
    /// Whether two identities are of the same object: their numbers are equal.
    /// </summary>
    /// <param name="other">The other identity.</param>
    /// <returns><see langword="true"/> when both are the same object's, or both <c>Nothing</c>'s.</returns>
    public bool Equals(VBRuntimeObjectId other) => _id == other._id;

    /// <summary>
    /// The hash of the identity's number.
    /// </summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode() => _id.GetHashCode();

    /// <summary>
    /// The identity's number, in invariant digits.
    /// </summary>
    /// <returns>The number; <c>0</c> for <c>Nothing</c>.</returns>
    public override string ToString() => _id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The identity as a number: <c>0</c> for <c>Nothing</c>, and for any object one that no other object of the process has.
    /// </summary>
    public long Identity => _id;
}
