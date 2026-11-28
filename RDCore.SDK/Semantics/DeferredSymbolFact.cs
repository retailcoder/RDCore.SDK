using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Semantics;

/// <summary>
/// How the code of a procedure uses a name that nothing declares (<see cref="DeferredUseFact"/>).
/// </summary>
public enum DeferredUseKind
{
    /// <summary>
    /// The name is read for its value, or passed as an argument.
    /// </summary>
    Read,

    /// <summary>
    /// The name is the target of a <c>Let</c> assignment, or the control variable of a <c>For</c> loop.
    /// </summary>
    Let,

    /// <summary>
    /// The name is the target of a <c>Set</c> assignment.
    /// </summary>
    Set,

    /// <summary>
    /// The name is called as a procedure, with or without arguments.
    /// </summary>
    Call,

    /// <summary>
    /// The name is the object of a member access (<c>x.Name</c>).
    /// </summary>
    MemberAccess,
}

/// <summary>
/// One use of a name that nothing declares.
/// </summary>
/// <param name="Kind">How the code uses the name.</param>
/// <param name="Location">Where the use is written: the range of the name.</param>
public sealed record class DeferredUseFact(DeferredUseKind Kind, SourceLocation Location)
{
    /// <summary>
    /// For a <see cref="DeferredUseKind.Call"/>, the declared type of each argument, by name, in the order they are written; the name of the unknown type for
    /// an argument whose type is not known. Empty for any other use, and for a call with no arguments.
    /// </summary>
    public ImmutableArray<string> ArgumentTypes { get; init; } = [];
}

/// <summary>
/// A name a procedure uses that nothing declares, with how it is used and the types its uses give it (<strong>MS-VBAL §5.6.10</strong>).
/// </summary>
/// <remarks>
/// <para>
/// <strong>RD-VBA addition.</strong> The fact is stated whatever the module's options say: under <c>Option Explicit</c> the name is also a compile error
/// (<c>Variable not defined</c>, once per name and procedure), and without it the name is the implicit <c>Variant</c> local MS-VBAL declares
/// (<see cref="Variable"/>). Tooling reads the fact to offer the name's members and to declare it.
/// </para>
/// <para>
/// A candidate is the declared type of the value a <c>Let</c> or a <c>Set</c> assigns to the name, of a <c>For</c> loop's start value when the name is
/// its control variable, of the variable or parameter a read of the name is assigned or passed to, and the class a <c>Set</c> makes with <c>New</c>. A
/// value whose type is <c>Variant</c> or not known gives none.
/// </para>
/// </remarks>
/// <param name="Name">The name, as its first use writes it.</param>
/// <param name="Location">Where its first use is written.</param>
/// <param name="Uses">Every use of the name in the procedure, in the order they are written.</param>
/// <param name="Candidates">The names of the types its uses give it, each once, ordered by name.</param>
public sealed record class DeferredSymbolFact(string Name, SourceLocation Location, ImmutableArray<DeferredUseFact> Uses, ImmutableArray<string> Candidates)
{
    /// <summary>
    /// The address of the implicit local variable that the name is, when the module does not declare <c>Option Explicit</c>; <see langword="null"/> under
    /// <c>Option Explicit</c>, where nothing is declared.
    /// </summary>
    public Uri? Variable { get; init; }

    /// <summary>
    /// The name of the one type the candidates resolve to (<see cref="Model.Types.Complex.IVBInferableTypeExtensions.Resolve"/>); <see langword="null"/>
    /// when there is no candidate.
    /// </summary>
    public string? Resolved { get; init; }
}
