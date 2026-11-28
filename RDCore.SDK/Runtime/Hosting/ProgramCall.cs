namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// A call that an invocation makes before its procedure, in the same run (<see cref="ProgramInvocation.Calls"/>): a function of a standard module, whose
/// value, an object, is kept under <see cref="Name"/> for the calls and the factory after it.
/// </summary>
/// <remarks>
/// Its arguments bind to the function's parameters as the arguments of an invocation do (<see cref="ProgramInvocation"/>): an argument that names no
/// parameter, or two that name the same one, is error 448; a parameter with no argument that is not <c>Optional</c>, error 449; a value that does not
/// convert, error 13.
/// </remarks>
/// <param name="Name">
/// The name its value is kept under, which a <see cref="ProgramCallArgument"/> of a later call or of the factory names. Two calls of one invocation have
/// two names, compared ordinally.
/// </param>
/// <param name="Module">The name of a standard module of the program, as RD-VBA names it.</param>
/// <param name="Procedure">The name of a <c>Public</c> <c>Function</c> or <c>Property Get</c> of that module.</param>
/// <param name="Arguments">The arguments, by the names of the parameters they are passed to; an <c>Optional</c> parameter may have none.</param>
/// <param name="MachineHeld">
/// Whether its value is the machine's: made by the first invocation that has the call, and kept for the invocations after it, which do not make it again.
/// A later invocation's call of the same <see cref="Name"/> passes the value that is kept, whatever module and procedure it names. The value is kept until
/// <see cref="IProgramMachine.ResetAsync"/> lets go of it (an object that nothing else holds runs its <c>Class_Terminate</c> then), or the machine is
/// disposed. An error that ends a run, or an <c>End</c>, ends the program, and lets go of it with everything else, running no <c>Class_Terminate</c>.
/// </param>
public sealed record class ProgramCall(string Name, string Module, string Procedure, IReadOnlyList<ProgramArgument> Arguments, bool MachineHeld = false);
