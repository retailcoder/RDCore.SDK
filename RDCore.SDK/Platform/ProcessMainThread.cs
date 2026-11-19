using System.Collections.Concurrent;

namespace RDCore.SDK.Platform;

/// <summary>
/// The main thread of a process, given by its entry point to the work that must run there while the program runs on another thread: on macOS, AppKit runs a
/// window's loop on the process's main thread only, and a user interface set up on any other thread fails.
/// </summary>
/// <remarks>
/// <para>
/// An entry point that runs a program which may show windows serves its main thread with <see cref="Serve"/>: the program runs on a thread of the thread pool,
/// and the main thread runs the work <see cref="PostAsync"/> is given, one piece at a time, in the order it was posted, until the program ends. Work that runs for
/// as long as the program does, such as a window's message loop, returns when the token it is given is cancelled, which it is when the program ends.
/// </para>
/// <para>
/// <see cref="Current"/> is the main thread an entry point serves, or <see langword="null"/> when it serves none: code that needs the main thread asks for it
/// there, and fails as it sees fit when there is none, without trying another thread.
/// </para>
/// <example>
/// <code>
/// public static int Main(string[] args) => ProcessMainThread.Serve(() => RunAsync(args));
/// </code>
/// </example>
/// </remarks>
public sealed class ProcessMainThread
{
    private static ProcessMainThread? _current;

    private readonly BlockingCollection<(Action<CancellationToken> Work, TaskCompletionSource Done)> _work = [];
    private readonly CancellationTokenSource _ending = new();
    private int _run;

    /// <summary>
    /// A main thread that no thread serves yet: the thread that calls <see cref="Run"/> serves it. It is not <see cref="Current"/>, which only
    /// <see cref="Serve"/> makes.
    /// </summary>
    public ProcessMainThread()
    {
    }

    /// <summary>
    /// The main thread of this process, while its entry point serves it (<see cref="Serve"/>); <see langword="null"/> before it does, after it is done, and in a
    /// process whose entry point serves none.
    /// </summary>
    public static ProcessMainThread? Current => Volatile.Read(ref _current);

    /// <summary>
    /// Runs <paramref name="program"/> on a thread of the thread pool, and serves the calling thread as the process's main thread (<see cref="Current"/>) until
    /// it ends. Called by the process's entry point, on the process's main thread.
    /// </summary>
    /// <param name="program">The program: what the entry point would otherwise run on its own thread.</param>
    /// <returns>What <paramref name="program"/> returned: the process's exit code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="program"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The process's main thread is served already.</exception>
    /// <remarks>
    /// What <paramref name="program"/> throws is thrown, once the work posted to the main thread has returned. <see cref="Current"/> is <see langword="null"/> again
    /// once this returns.
    /// </remarks>
    public static int Serve(Func<Task<int>> program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var main = new ProcessMainThread();
        if (Interlocked.CompareExchange(ref _current, main, null) is not null)
        {
            throw new InvalidOperationException("The process's main thread is served already.");
        }

        try
        {
            return main.Run(program);
        }
        finally
        {
            Interlocked.CompareExchange(ref _current, null, main);
        }
    }

    /// <summary>
    /// Runs <paramref name="program"/> on a thread of the thread pool, and runs the work posted to this main thread (<see cref="PostAsync"/>) on the calling thread
    /// until the program ends.
    /// </summary>
    /// <param name="program">The program.</param>
    /// <returns>What <paramref name="program"/> returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="program"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This main thread was run already.</exception>
    /// <remarks>
    /// <para>
    /// <see cref="Serve"/> runs the process's own main thread this way. A main thread made with the constructor is served by the thread that runs it, and is
    /// not <see cref="Current"/>: for a host that has a thread of its own play the part of the main thread, or for a test.
    /// </para>
    /// <para>
    /// When the program ends, the token every piece of work is given is cancelled. Work posted before then that has not begun runs all the same, with that
    /// token cancelled, and this returns once every piece has returned. Then what the program threw is thrown, or what it returned is returned.
    /// </para>
    /// </remarks>
    public int Run(Func<Task<int>> program)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (Interlocked.Exchange(ref _run, 1) != 0)
        {
            throw new InvalidOperationException("This main thread was run already.");
        }

        var running = Task.Run(program);
        _ = running.ContinueWith(_ =>
        {
            _ending.Cancel();
            _work.CompleteAdding();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

        foreach (var (work, done) in _work.GetConsumingEnumerable())
        {
            try
            {
                work(_ending.Token);
                done.SetResult();
            }
            catch (OperationCanceledException cancelled)
            {
                done.SetCanceled(cancelled.CancellationToken);
            }
            catch (Exception exception)
            {
                done.SetException(exception);
            }
        }

        // the program has ended: its task is complete, and waiting for it blocks nothing.
#pragma warning disable VSTHRD002
        return running.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    /// <summary>
    /// Posts <paramref name="work"/> to this main thread, which runs it after the work posted before it, and does not wait for it.
    /// </summary>
    /// <param name="work">
    /// The work. It is given a token that is cancelled when the program ends: work that runs for as long as the program does returns when it is.
    /// </param>
    /// <returns>
    /// A task that completes when the work returns: faulted with what it throws, and cancelled when what it throws is an <see cref="OperationCanceledException"/>.
    /// What it throws does not stop the main thread, which goes on to the next piece.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The program has ended, and the main thread runs no more work.</exception>
    /// <remarks>
    /// Work posted before the main thread is run waits until it is. The task completes on a thread of the thread pool, never on the main thread, so a
    /// continuation of it does not wait for the work after it. Work that runs on the main thread must not wait for work it posts there itself.
    /// </remarks>
    public Task PostAsync(Action<CancellationToken> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _work.Add((work, done));
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("The program has ended: the process's main thread runs no more work.");
        }

        return done.Task;
    }
}
