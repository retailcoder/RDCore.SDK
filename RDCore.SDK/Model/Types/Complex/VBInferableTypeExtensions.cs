using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.Types.Complex;

/// <summary>
/// How the candidates of an <see cref="IVBInferableType"/> are added and resolved to one type.
/// </summary>
public static class IVBInferableTypeExtensions
{
    /// <summary>
    /// The one type the candidates of an inferable type resolve to; <see langword="null"/> when they resolve to none.
    /// </summary>
    /// <param name="inferable">The inferable type.</param>
    /// <returns>
    /// <see langword="null"/> when there is no candidate; the candidate when there is one; <c>Variant</c> when there are several and none of them is
    /// <c>Object</c>, a type with members (<see cref="IVBMemberOwnerType"/>) or an inferable type; <see langword="null"/> otherwise.
    /// </returns>
    public static VBType? Resolve(this IVBInferableType inferable) => inferable.CandidateTypes.Count switch
    {
        0 => null,
        1 => inferable.CandidateTypes.Single(),
        _ => inferable.CandidateTypes.All(IsPlainValueType) ? VBVariantType.TypeInfo : null,
    };

    /// <summary>
    /// A set of candidates with a candidate added.
    /// </summary>
    /// <param name="candidates">The candidates.</param>
    /// <param name="vbType">The candidate to add.</param>
    /// <returns>
    /// The candidates, with <see cref="VBLongType"/> added for an integral candidate (<see cref="IIntegralNumericType"/>), <see cref="VBDoubleType"/> for a
    /// floating-point one (<see cref="IFloatingPointNumericType"/>), <see cref="VBCurrencyType"/> for a fixed-point one (<see cref="IFixedPointNumericType"/>),
    /// the candidates of an inferable candidate (<see cref="IVBInferableType"/>), and any other candidate as it is.
    /// </returns>
    public static ImmutableHashSet<VBType> MergeCandidate(this ImmutableHashSet<VBType> candidates, VBType vbType) => vbType switch
    {
        IVBInferableType inferable => candidates.Union(inferable.CandidateTypes),
        IIntegralNumericType => candidates.Add(VBLongType.TypeInfo),
        IFloatingPointNumericType => candidates.Add(VBDoubleType.TypeInfo),
        IFixedPointNumericType => candidates.Add(VBCurrencyType.TypeInfo),
        _ => candidates.Add(vbType),
    };

    private static bool IsPlainValueType(VBType type) => type is not (VBObjectType or IVBMemberOwnerType or IVBInferableType);
}
