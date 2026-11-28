using System.Collections.Immutable;

namespace RDCore.SDK.Semantics.Flow;

/// <summary>
/// The subtypes a <c>Variant</c> may hold (<strong>MS-VBAL §2.1</strong>): those of the values the code gives it, by their names.
/// </summary>
/// <remarks>
/// <strong>RD-VBA addition.</strong> The subtype of a value is the declared type of the expression that gives it: <c>v = 1</c> gives <c>Integer</c>,
/// <c>v = "a"</c> gives <c>String</c>, <c>Set v = New Collection</c> gives <c>Collection</c>, and the initial value of a local gives <c>Empty</c>. A value
/// whose subtype is not known (a <c>Variant</c> expression, an argument a call may assign, a <c>For Each</c> element, what <c>Input #</c> reads) gives none,
/// and the subtypes are then not complete.
/// </remarks>
/// <param name="Subtypes">
/// The names of the subtypes (<see cref="Model.Types.Abstract.VBType.Name"/>), each once, in ordinal order: the order the code is written in does not change
/// them.
/// </param>
/// <param name="IsComplete">Whether every value the code gives has a known subtype: the variable holds one of <paramref name="Subtypes"/>, and nothing else.</param>
public sealed record class VariantSubtypes(ImmutableArray<string> Subtypes, bool IsComplete)
{
    /// <summary>
    /// The subtypes of the specified values, each once, in ordinal order.
    /// </summary>
    /// <param name="subtypes">The names of the subtypes of the values the code gives, in any order and with repeats.</param>
    /// <param name="isComplete">Whether every value the code gives has a known subtype.</param>
    /// <returns>The subtypes.</returns>
    public static VariantSubtypes Of(IEnumerable<string> subtypes, bool isComplete)
        => new([.. subtypes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)], isComplete);

    /// <summary>
    /// The one subtype the variable holds, when the subtypes are complete and there is one; <see langword="null"/> otherwise.
    /// </summary>
    public string? OnlySubtype => IsComplete && Subtypes.Length == 1 ? Subtypes[0] : null;

    /// <summary>
    /// Whether the specified subtypes are the same as these: the same names, and both complete or neither.
    /// </summary>
    /// <param name="other">The other subtypes.</param>
    /// <returns><see langword="true"/> when they are the same.</returns>
    public bool Equals(VariantSubtypes? other)
        => other is not null && IsComplete == other.IsComplete && Subtypes.AsSpan().SequenceEqual(other.Subtypes.AsSpan());

    /// <inheritdoc/>
    public override int GetHashCode()
        => (Subtypes.IsDefault ? [] : Subtypes).Aggregate(IsComplete.GetHashCode(), (hash, name) => HashCode.Combine(hash, name));
}
