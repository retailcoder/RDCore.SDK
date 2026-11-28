using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Semantics.Flow;

/// <summary>
/// A read of a <c>Variant</c> automatic local, with the subtypes of the values that may reach it: those of the definitions of the variable that a code path
/// leads from to the read (<strong>MS-VBAL §2.3</strong>), the variable's initial value included.
/// </summary>
/// <remarks>
/// <para>
/// <strong>RD-VBA addition.</strong> <c>If c Then v = 1 Else v = "a"</c> followed by a read of <c>v</c> gives the read <c>Integer</c> and
/// <c>String</c>, whichever branch is written first; <c>If c Then v = 1</c> gives it <c>Integer</c> and <c>Empty</c>.
/// </para>
/// <para>
/// What may happen counts as happening: an error on the way, a jump, and a loop that runs no time are code paths. A read is stated only when the language core
/// could see every expression of the body and follow every code path of it; none is stated otherwise, which is not to say that there is none.
/// </para>
/// </remarks>
/// <param name="Variable">The address of the variable.</param>
/// <param name="Name">The name of the variable, as the read writes it.</param>
/// <param name="Location">Where the read is written: the range of the name.</param>
/// <param name="Subtypes">The subtypes of the values that may reach the read.</param>
public sealed record class VariantReadFact(Uri Variable, string Name, SourceLocation Location, VariantSubtypes Subtypes);
