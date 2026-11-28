using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Semantics;

/// <summary>
/// A member a procedure accesses on an object whose declared type does not say it has one, with how the member is used and the types its uses give its
/// value (<strong>MS-VBAL §5.6.12</strong>).
/// </summary>
/// <remarks>
/// <para>
/// <strong>RD-VBA addition.</strong> The receiver is a name: a variable declared <c>Object</c> or <c>Variant</c>, a name nothing declares
/// (<see cref="DeferredSymbolFact"/>), or a variable of a class of the project that declares no member of the name (<c>Method or data member not
/// found</c>). Tooling reads the fact to offer the member in completion and to generate it. A late-bound access is stated whatever the classes its object
/// may be of: whether one of them has the member is the evaluation's to say (<see cref="Facts.MemberAccessFact.LateBoundCandidates"/>).
/// </para>
/// <para>
/// A use's kind says what the code does with the member: <see cref="DeferredUseKind.Read"/> for its value, <see cref="DeferredUseKind.Let"/> and
/// <see cref="DeferredUseKind.Set"/> for an assignment to it (a property), <see cref="DeferredUseKind.Call"/> for a call with or without arguments, and
/// <see cref="DeferredUseKind.MemberAccess"/> when a member of what it returns is accessed. A candidate is the declared type of the value an assignment
/// gives the member, and of the variable or parameter its value is assigned or passed to. A value whose type is <c>Variant</c> or not known gives none.
/// </para>
/// </remarks>
/// <param name="Receiver">The name of the object, as the first access writes it.</param>
/// <param name="Member">The name of the member, as the first access writes it.</param>
/// <param name="Location">Where the first access is written: the range of the member's name.</param>
/// <param name="Uses">Every access of the member on the receiver in the procedure, in the order they are written.</param>
/// <param name="Candidates">The names of the types its uses give the member's value, each once, ordered by name.</param>
public sealed record class DeferredMemberFact(
    string Receiver, string Member, SourceLocation Location, ImmutableArray<DeferredUseFact> Uses, ImmutableArray<string> Candidates)
{
    /// <summary>
    /// The name of the declared type of the receiver: <c>Object</c>, <c>Variant</c>, or the class that declares no member of the name;
    /// <see langword="null"/> when the receiver is a name nothing declares under <c>Option Explicit</c>.
    /// </summary>
    public string? ReceiverType { get; init; }

    /// <summary>
    /// The address of the variable the receiver is; <see langword="null"/> when the receiver is a name nothing declares under <c>Option Explicit</c>.
    /// </summary>
    public Uri? ReceiverVariable { get; init; }

    /// <summary>
    /// The name of the one type the candidates resolve to (<see cref="Model.Types.Complex.IVBInferableTypeExtensions.Resolve"/>); <see langword="null"/>
    /// when there is no candidate.
    /// </summary>
    public string? Resolved { get; init; }
}
