namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// A hook of an invocation (<see cref="ProgramInvocation.BeforeProcedure"/>, <see cref="ProgramInvocation.OnError"/>): a member of an interface, called
/// on the procedure's object when the object implements the interface (<strong>MS-VBAL §5.2.4.2</strong>), as <c>TypeOf</c> tells it
/// (<strong>MS-VBAL §5.6.7</strong>). The object calls its own member that implements the interface's (<strong>MS-VBAL §5.3.1.9</strong>). A hook is
/// not called on an object that does not implement the interface, nor for a procedure of a standard module.
/// </summary>
/// <remarks>
/// Its arguments bind to the parameters of the interface's member as the arguments of a <see cref="ProgramCall"/> do. An interface the program does not
/// have, or a member that is not a <c>Public</c> <c>Function</c> or <c>Property Get</c> of it, ends the invocation as
/// <see cref="Platform.Protocol.ExecutionOutcome.NotFound"/> before anything runs.
/// </remarks>
/// <param name="Interface">
/// The name of the interface, as RD-VBA names it: a class module of the program, or, qualified with the name of a library the program references, a class
/// of that library: <c>Library.Class</c>.
/// </param>
/// <param name="Member">The name of a <c>Public</c> <c>Function</c> or <c>Property Get</c> of the interface.</param>
/// <param name="Arguments">The arguments, by the names of the parameters of the interface's member they are passed to.</param>
public sealed record class ProgramHookCall(string Interface, string Member, IReadOnlyList<ProgramArgument> Arguments);
