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
    /// initialized before it and terminated once the run has its value (<see cref="ProgramInvocation"/>). The invocation's calls, guards, factory, hooks
    /// and result calls run in the same run.
    /// </summary>
    /// <param name="invocation">The procedure, its arguments, and the calls, guards, factory, hooks and result calls that run with it.</param>
    /// <param name="cancellationToken">
    /// Interrupts the invocation between two instructions: the result is <see cref="ExecutionOutcome.Interrupted"/>. A call outside the program (a
    /// <c>Declare</c> or a COM object) returns before the interruption is seen.
    /// </param>
    /// <returns>
    /// The invocation's value, as JSON: the procedure's, or that of the guard or the hook that answered for it, or that of the last result call. Or how it
    /// ended otherwise: a run-time error that nothing handled (<see cref="ExecutionOutcome.RuntimeError"/>), including one that the
    /// <c>Class_Initialize</c> of the object of a class module left unhandled, a module, a procedure or an interface the program does not have
    /// (<see cref="ExecutionOutcome.NotFound"/>), an <c>End</c> statement (<see cref="ExecutionOutcome.Halted"/>), or an invocation while another runs
    /// (<see cref="ExecutionOutcome.Refused"/>). None of these is an exception. A call, a guard, the factory, a hook or a result call ends the invocation
    /// as the procedure does.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The invocation is not one a machine can run, and nothing runs: two of its calls, guards and result calls have one <see cref="ProgramCall.Name"/>;
    /// a <see cref="ProgramCallArgument"/> names none of the calls made before the one it is of; a guard or a result call is machine-held or has no
    /// <see cref="ProgramCall.Procedure"/>; a call with no <see cref="ProgramCall.Procedure"/> has arguments; a <see cref="ProgramErrorArgument"/> is not
    /// an argument of <see cref="ProgramInvocation.OnError"/>; or a <see cref="ProgramResultArgument"/> is not an argument of a result call.
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
