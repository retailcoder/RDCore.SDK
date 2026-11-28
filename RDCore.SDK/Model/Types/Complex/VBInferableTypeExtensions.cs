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
    /// <see langword="null"/> when there is no candidate, and the candidate when there is one. Of several candidates:
    /// <list type="bullet">
    /// <item>the widest, by <c>Double</c>, <c>Single</c>, <c>Long</c>, <c>Integer</c>, <c>Byte</c>, when each is one of those;</item>
    /// <item><c>Object</c>, when each is <c>Object</c> or a class;</item>
    /// <item><c>Variant</c> otherwise: <c>Boolean</c>, <c>String</c>, <c>Date</c>, <c>Currency</c>, <c>Decimal</c>, <c>LongLong</c>, a record and an
    /// enumeration resolve to themselves only alone. A <c>Boolean</c> is not widened to a number, which would change the value the code gives it
    /// (<c>True</c> held by an <c>Integer</c> is <c>-1</c>).</item>
    /// </list>
    /// </returns>
    public static VBType? Resolve(this IVBInferableType inferable)
    {
        var candidates = inferable.CandidateTypes;
        if (candidates.Count <= 1)
        {
            return candidates.SingleOrDefault();
        }

        if (candidates.All(candidate => WideningRank(candidate) > 0))
        {
            return candidates.MaxBy(WideningRank);
        }

        return candidates.All(candidate => candidate is VBObjectType or VBClassType or VBDeferredClassType)
            ? VBObjectType.TypeInfo
            : VBVariantType.TypeInfo;
    }

    // the numeric types several candidates resolve to the widest of, narrowest first; 0 for any other type. Boolean is not one: True held by a number is -1.
    private static int WideningRank(VBType type) => type switch
    {
        VBByteType => 1,
        VBIntegerType => 2,
        VBLongType => 3,
        VBSingleType => 4,
        VBDoubleType => 5,
        _ => 0,
    };

    /// <summary>
    /// A set of candidates with a candidate added.
    /// </summary>
    /// <param name="candidates">The candidates.</param>
    /// <param name="vbType">The candidate to add.</param>
    /// <returns>
    /// The candidates, with the candidates of an inferable candidate (<see cref="IVBInferableType"/>) added, or the inferable candidate itself when it has
    /// none (a class no module declares yet); any other candidate is added as it is: an <c>Integer</c> stays an <c>Integer</c>, and <see cref="Resolve"/>
    /// decides what several candidates resolve to.
    /// </returns>
    public static ImmutableHashSet<VBType> MergeCandidate(this ImmutableHashSet<VBType> candidates, VBType vbType) => vbType switch
    {
        IVBInferableType { CandidateTypes.Count: > 0 } inferable => candidates.Union(inferable.CandidateTypes),
        _ => candidates.Add(vbType),
    };
}
