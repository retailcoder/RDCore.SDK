using RDCore.SDK.Workspace;

namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// An RD-VBA program composed once from its files (parsed, its symbols defined and bound), which makes the machines that run it.
/// </summary>
/// <remarks>
/// A host composes the program at startup, reads what it serves from <see cref="Documents"/> (an RDWeb host reads its endpoints from the annotations), and
/// makes as many machines as it runs invocations at once. Disposing the program disposes no machine: each machine is disposed by its owner, before the
/// program.
/// </remarks>
public interface IComposedProgram : IAsyncDisposable
{
    /// <summary>
    /// The program's files, each with its parse: the modules of the program, a file of several modules with one for each of its blocks
    /// (<see cref="RDCoreDocument.ModuleUris"/>).
    /// </summary>
    IReadOnlyList<RDCoreDocument> Documents { get; }

    /// <summary>
    /// Makes a machine of the program, in a state of its own: no module-level variable set, no object made, no file open.
    /// </summary>
    /// <param name="cancellationToken">Cancels making the machine.</param>
    /// <returns>The machine; its owner disposes it.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled before the machine was made.</exception>
    /// <exception cref="ObjectDisposedException">The program was disposed.</exception>
    ValueTask<IProgramMachine> CreateMachineAsync(CancellationToken cancellationToken);
}
