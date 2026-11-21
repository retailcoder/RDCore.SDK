using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Semantics.Flow;

/// <summary>
/// An assignment of a whole variable whose value no code path reads: every path from the assignment replaces the variable whole again, or ends the variable's
/// extent (<strong>MS-VBAL §2.3</strong>), before anything reads it. The assignment is a <c>Let</c> (<strong>MS-VBAL §5.4.3.8</strong>), a
/// <c>Set</c> (<strong>MS-VBAL §5.4.3.9</strong>), an <c>LSet</c> (<strong>MS-VBAL §5.4.3.6</strong>) or an <c>RSet</c>
/// (<strong>MS-VBAL §5.4.3.7</strong>) statement.
/// </summary>
/// <remarks>
/// <para>
/// A variable whose value outlives the procedure's invocation (a <c>ByRef</c> parameter, whose argument is the caller's; a variable of a module or of a class;
/// a <c>Static</c> local; the result of a function or a <c>Property Get</c>) is read once the invocation ends, so its assignment is stated only when the
/// code that follows replaces it whole first. Code of another procedure that may run in between (a call, a property, an object created or released, an
/// event raised) is taken to read what it can see of the variable.
/// </para>
/// <para>
/// What may happen counts as happening: an error on the way, a jump, and a loop that runs no time are code paths, and an assignment that raises an error
/// replaces nothing. An assignment is stated only when the language core could see every expression of the body and follow every code path of it; none is
/// stated otherwise, which is not to say that there is none.
/// </para>
/// </remarks>
/// <param name="Variable">The address of the variable.</param>
/// <param name="Name">The name of the variable, as the assignment writes it.</param>
/// <param name="Location">Where the assignment writes the variable: the range of its name in the target.</param>
public sealed record class UnreadAssignmentFact(Uri Variable, string Name, SourceLocation Location);
