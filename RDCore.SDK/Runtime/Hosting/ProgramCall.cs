namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// A call that an invocation makes in its run, other than the procedure's: one of its <see cref="ProgramInvocation.Calls"/>, whose value is kept under
/// <see cref="Name"/>, one of its <see cref="ProgramInvocation.Guards"/>, or one of its <see cref="ProgramInvocation.ResultCalls"/>. It calls a function
/// of a standard module, a function of a predeclared class on the class's default instance (<strong>MS-VBAL §5.2.4.1.2</strong>), or, with no
/// <see cref="Procedure"/>, makes a new object of a class (<strong>MS-VBAL §5.6.8</strong>).
/// </summary>
/// <remarks>
/// <para>
/// Its arguments bind to the function's parameters as the arguments of an invocation do (<see cref="ProgramInvocation"/>): an argument that names no
/// parameter, or two that name the same one, is error 448; a parameter with no argument that is not <c>Optional</c>, error 449; a value that does not
/// convert, error 13.
/// </para>
/// <para>
/// A module the program does not have as one of these, a class with a <see cref="Procedure"/> whose <c>VB_PredeclaredId</c> is not <c>True</c>, a
/// standard module or a library's module with no <see cref="Procedure"/>, or a <see cref="Procedure"/> that is not a <c>Public</c> <c>Function</c> or
/// <c>Property Get</c> of the module, ends the invocation as <see cref="Platform.Protocol.ExecutionOutcome.NotFound"/> before anything runs.
/// </para>
/// </remarks>
/// <param name="Name">
/// The name its value is kept under, which a <see cref="ProgramCallArgument"/> names. The calls of one invocation, its guards and its result calls have
/// one name each, compared ordinally.
/// </param>
/// <param name="Module">
/// The name of the module, as RD-VBA names it: a standard module or a class module of the program (not a document module), or, qualified with the name
/// of a library the program references whose code is RD-VBA code (a project, <strong>MS-VBAL §4.1</strong>), a standard module of that library:
/// <c>Library.Module</c>. A library's module may be a private module (<strong>MS-VBAL §5.2.1.4</strong>), which the program itself cannot name.
/// </param>
/// <param name="Procedure">
/// The name of a <c>Public</c> <c>Function</c> or <c>Property Get</c> of that module. <see langword="null"/> for a new object of the class module
/// <see cref="Module"/> names, which its <c>Class_Initialize</c> initializes (<strong>MS-VBAL §5.3.1.10</strong>); <see cref="Arguments"/> is empty then.
/// </param>
/// <param name="Arguments">The arguments, by the names of the parameters they are passed to; an <c>Optional</c> parameter may have none.</param>
/// <param name="MachineHeld">
/// Whether its value is the machine's: made by the first invocation that has the call, and kept for the invocations after it, which do not make it again.
/// A later invocation's call of the same <see cref="Name"/> passes the value that is kept, whatever module and procedure it names. The value is kept until
/// <see cref="IProgramMachine.ResetAsync"/> lets go of it (an object that nothing else holds runs its <c>Class_Terminate</c> then), or the machine is
/// disposed. An error that ends a run, or an <c>End</c>, ends the program, and lets go of it with everything else, running no <c>Class_Terminate</c>.
/// Only a call of <see cref="ProgramInvocation.Calls"/> is machine-held.
/// </param>
public sealed record class ProgramCall(string Name, string Module, string? Procedure, IReadOnlyList<ProgramArgument> Arguments, bool MachineHeld = false);
