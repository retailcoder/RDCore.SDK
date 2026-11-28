using RDCore.SDK.Platform.Protocol;

namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// A machine: one runtime session of a composed program (<see cref="IComposedProgram.CreateMachineAsync"/>), which runs one invocation at a time.
/// </summary>
/// <remarks>
/// Every machine of a program has a state of its own: module-level variables, <c>Static</c> locals, objects, open files, error state and current directory.
/// Two machines may run at the same time, each on a thread of its own; concurrency comes from several machines, never from two invocations of one.
/// <see cref="ResetAsync"/> returns a machine to the state it was made in, so that nothing of one invocation reaches the next.
/// </remarks>
public interface IProgramMachine : IAsyncDisposable
{
    /// <summary>
    /// Runs one procedure of the program with the invocation's arguments. A procedure of a class whose <c>VB_PredeclaredId</c> is <c>True</c> runs on the
    /// class's default instance, which keeps its state between invocations; a procedure of any other class module runs on a new object of the class,
    /// initialized before it and terminated once it returns (<see cref="ProgramInvocation"/>). The invocation's calls and its factory run before it, in
    /// the same run (<see cref="ProgramInvocation.Calls"/>, <see cref="ProgramInvocation.Factory"/>).
    /// </summary>
    /// <param name="invocation">The procedure, its arguments as JSON values, and the calls and the factory that run before it.</param>
    /// <param name="cancellationToken">
    /// Interrupts the invocation between two instructions: the result is <see cref="ExecutionOutcome.Interrupted"/>. A call outside the program (a
    /// <c>Declare</c> or a COM object) returns before the interruption is seen.
    /// </param>
    /// <returns>
    /// The value the procedure returned, as JSON, or how it ended otherwise: a run-time error that nothing handled
    /// (<see cref="ExecutionOutcome.RuntimeError"/>), including one that the <c>Class_Initialize</c> of the object of a class module left unhandled, a
    /// standard module or a class module the program does not have, or a procedure its module does not declare <c>Public</c>
    /// (<see cref="ExecutionOutcome.NotFound"/>), an <c>End</c> statement (<see cref="ExecutionOutcome.Halted"/>), or an invocation while another runs
    /// (<see cref="ExecutionOutcome.Refused"/>). None of these is an exception. A call or a factory ends the invocation as the procedure does: its value
    /// is not returned.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Two calls of the invocation have one <see cref="ProgramCall.Name"/>, or a <see cref="ProgramCallArgument"/> names no call made before the one it is
    /// of. Nothing runs.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The machine was disposed.</exception>
    Task<ProgramInvocationResult> InvokeAsync(ProgramInvocation invocation, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the machine to the state it was made in: releases the values it keeps for its invocations (<see cref="ProgramCall.MachineHeld"/>), then its
    /// module-level variables and <c>Static</c> locals, running the <c>Class_Terminate</c> of each object released, closes its open files, clears its
    /// error state and restores its current directory.
    /// </summary>
    /// <param name="cancellationToken">Cuts the reset short, between two instructions of a <c>Class_Terminate</c> or before the next one runs.</param>
    /// <returns>
    /// <see langword="true"/> when the machine is back in the state it was made in; <see langword="false"/> when the token cut the reset short or an
    /// invocation is running. A machine whose reset answered <see langword="false"/> is not used again.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The machine was disposed.</exception>
    Task<bool> ResetAsync(CancellationToken cancellationToken);
}
