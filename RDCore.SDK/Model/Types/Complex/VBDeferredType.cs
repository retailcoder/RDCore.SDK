using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types.Abstract;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.Types.Complex;

/// <summary>
/// A data type that no statement declares, which is inferred from how the code uses what has it: the types the code gives it are its candidates.
/// </summary>
/// <remarks>
/// <para>
/// <strong>RD-VBA addition.</strong> MS-VBAL leaves the type of a name that nothing declares to <c>Option Explicit</c> (<strong>MS-VBAL §5.6.10</strong>): an
/// implicit variable is a <c>Variant</c>. Inference says more of it, for tooling: a candidate is a type the code gives the name, such as the type of a value
/// assigned to it, or the type of the target it is assigned to.
/// </para>
/// <para>
/// An inferred type is never a declaration, and inference is a property of the model: no option turns it on or off. <c>Option Strict</c> makes the use of an
/// inferred type a compile error, and leaves its candidates as they are.
/// </para>
/// </remarks>
public interface IVBInferableType
{
    /// <summary>
    /// The candidates: the types the code gives what has this type, after the merge rules of
    /// <see cref="IVBInferableTypeExtensions.MergeCandidate"/>.
    /// </summary>
    ImmutableHashSet<VBType> CandidateTypes { get; init; }

    /// <summary>
    /// A copy of this type, with a candidate added by the merge rules of <see cref="IVBInferableTypeExtensions.MergeCandidate"/>.
    /// </summary>
    /// <param name="vbType">The candidate to add.</param>
    /// <returns>A copy of this type whose candidates include <paramref name="vbType"/>.</returns>
    IVBInferableType WithCandidateType(VBType vbType);
}

/// <summary>
/// A member that no statement declares, of a type whose members are inferred from how the code uses them: its candidates are the types of its value.
/// </summary>
public interface IVBDeferrableTypeMember : IVBInferableType
{
    /// <summary>
    /// The type the member is given when it is declared; <see langword="null"/> while it is not.
    /// </summary>
    VBType? DeferredVBType { get; init; }

    /// <summary>
    /// A copy of this member given a type: the type becomes its only candidate.
    /// </summary>
    /// <param name="vbType">The type of the member.</param>
    /// <returns>A copy of this member whose type is <paramref name="vbType"/>.</returns>
    VBDeferredTypeMemberSymbol WithDeferredVBType(VBType vbType);
}

/// <summary>
/// A type of a module that no statement declares, which is inferred from how the code uses it: the type of a class the code creates or accesses the members
/// of, before any module declares it (<see cref="VBDeferredClassType"/>, <see cref="VBDeferredModuleType"/>).
/// </summary>
/// <remarks>
/// <strong>RD-VBA addition.</strong> It is never a declaration (<see cref="IVBInferableType"/>), and it is hidden: no code names it.
/// </remarks>
public abstract record class VBDeferredType : VBType, IVBInferableType
{
    /// <summary>
    /// A type of a module that no statement declares, with no members and no candidates.
    /// </summary>
    /// <param name="name">The name the code uses for the type.</param>
    /// <param name="uri">The location of the module the type would be declared in.</param>
    public VBDeferredType(string name, Uri uri)
        : base(typeof(object), name, isHidden: true)
    {
        Uri = uri;
    }

    /// <summary>
    /// The location of the module the type would be declared in.
    /// </summary>
    public Uri Uri { get; init; }

    /// <summary>
    /// The members the code uses of the type.
    /// </summary>
    public ImmutableHashSet<VBDeferredTypeMemberSymbol> Members { get; init; } = [];

    /// <summary>
    /// A copy of this type with the specified members, in place of its own.
    /// </summary>
    /// <param name="members">The members of the copy.</param>
    /// <returns>A copy of this type whose members are <paramref name="members"/>.</returns>
    public VBDeferredType WithMembers(IEnumerable<VBDeferredTypeMemberSymbol> members) => this with { Members = [.. members] };

    /// <summary>
    /// The declared type this type was found to be; <see langword="null"/> while no declared type is known to be it.
    /// </summary>
    public IVBMemberOwnerType? DeferredVBTypeInfo { get; init; }

    /// <summary>
    /// A copy of this type, found to be the specified declared type.
    /// </summary>
    /// <param name="vbType">The declared type.</param>
    /// <returns>A copy of this type whose <see cref="DeferredVBTypeInfo"/> is <paramref name="vbType"/>.</returns>
    public VBDeferredType WithDeferredVBType(IVBMemberOwnerType vbType) => this with { DeferredVBTypeInfo = vbType };

    /// <inheritdoc/>
    public ImmutableHashSet<VBType> CandidateTypes { get; init; } = [];

    /// <inheritdoc/>
    public IVBInferableType WithCandidateType(VBType vbType) => this with { CandidateTypes = CandidateTypes.MergeCandidate(vbType) };
}
