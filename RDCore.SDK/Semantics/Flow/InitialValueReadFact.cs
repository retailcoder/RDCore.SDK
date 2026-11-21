using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Semantics.Flow;

/// <summary>
/// A read of a procedure's automatic local variable that the variable's initial value may reach: on some code path from the start of the procedure to the
/// read, nothing writes to the variable. A variable is initialized to the default value of its declared type when it is created
/// (<strong>MS-VBAL §2.3</strong>), and a local variable of a procedure is created each time the procedure is invoked.
/// </summary>
/// <remarks>
/// <para>
/// An automatic local variable is one a <c>Dim</c> declares, or that a name declares by being used, in a procedure that is not <c>Static</c>: nothing but the
/// procedure's own code gives it a value. A write is anything that may give it one: an assignment, a loop's control variable, <c>ReDim</c>, <c>Erase</c>, a
/// file input statement, a write to an element or a member of it, and passing it to a procedure that may take it by reference. A read of a part of the
/// variable (an element of an array, a member of a <c>Type</c>) is a read of the variable.
/// </para>
/// <para>
/// What may happen counts as happening: an error on the way to the read, a jump, and a loop that runs no time are code paths. A read is stated only when the
/// language core could see every expression of the body and follow every code path of it; no read is stated otherwise, which is not to say that there is none.
/// </para>
/// </remarks>
/// <param name="Variable">The address of the variable.</param>
/// <param name="Name">The name of the variable, as the read writes it.</param>
/// <param name="Location">Where the read is written: the range of the name.</param>
/// <param name="IsOnlyValue">
/// Whether the initial value is the only value that reaches the read: no code path from a write of the variable gets to it, so the read always finds the
/// default value of the variable's declared type.
/// </param>
public sealed record class InitialValueReadFact(Uri Variable, string Name, SourceLocation Location, bool IsOnlyValue);
