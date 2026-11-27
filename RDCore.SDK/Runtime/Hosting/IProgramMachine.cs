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
    /// Runs one procedure of the program with the invocation's arguments.
    /// </summary>
    /// <param name="invocation">The procedure, and its arguments as JSON values.</param>
    /// <param name="cancellationToken">
    /// Interrupts the invocation between two instructions: the result is <see cref="ExecutionOutcome.Interrupted"/>. A call outside the program (a
    /// <c>Declare</c> or a COM object) returns before the interruption is seen.
    /// </param>
    /// <returns>
    /// The value the procedure returned, as JSON, or how it ended otherwise: a run-time error that nothing handled
    /// (<see cref="ExecutionOutcome.RuntimeError"/>), a module the program does not have, or a procedure its module does not declare <c>Public</c>
    /// (<see cref="ExecutionOutcome.NotFound"/>), an <c>End</c>
    /// statement (<see cref="ExecutionOutcome.Halted"/>), or an invocation while another runs (<see cref="ExecutionOutcome.Refused"/>). None of these is an
    /// exception.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The machine was disposed.</exception>
    Task<ProgramInvocationResult> InvokeAsync(ProgramInvocation invocation, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the machine to the state it was made in: releases its module-level variables and <c>Static</c> locals, running the <c>Class_Terminate</c> of
    /// each object released, closes its open files, clears its error state and restores its current directory.
    /// </summary>
    /// <param name="cancellationToken">Cuts the reset short, between two instructions of a <c>Class_Terminate</c> or before the next one runs.</param>
    /// <returns>
    /// <see langword="true"/> when the machine is back in the state it was made in; <see langword="false"/> when the token cut the reset short or an
    /// invocation is running. A machine whose reset answered <see langword="false"/> is not used again.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The machine was disposed.</exception>
    Task<bool> ResetAsync(CancellationToken cancellationToken);
}
