namespace RDCore.SDK.Client.Connection;

/// <summary>
/// A request to a child process is answered by that process or not at all: when the process exits first, the request fails, and a connection that restarts
/// does not send it again (what crashed one process is likely to crash the next one with it).
/// </summary>
internal static class RequestsInFlight
{
    /// <summary>
    /// The answer to a request, or a <see cref="ServerProtocolSdkException"/> once the process it was sent to has exited.
    /// </summary>
    /// <typeparam name="T">The type of the answer.</typeparam>
    /// <param name="answer">The request, sent.</param>
    /// <param name="lost">
    /// Completes when the process the request was sent to exits, and is canceled when the process exits because the connection shut it down
    /// (<see cref="ShutDown"/>).
    /// </param>
    /// <param name="method">The method of the request, for the message.</param>
    public static async Task<T> AnsweredAsync<T>(Task<T> answer, Task lost, string method)
    {
        if (await Task.WhenAny(answer, lost) == answer)
        {
            return await answer;
        }

        // the request is abandoned: whatever becomes of it on the dead connection is observed here, and not left to the finalizer.
        _ = answer.ContinueWith(static abandoned => _ = abandoned.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        throw new ServerProtocolSdkException(ShutDown(lost)
            ? $"The connection shut the child process down before it answered '{method}'."
            : $"The child process exited before it answered '{method}'.");
    }

    /// <summary>
    /// Whether the process a request was sent to exited because the connection shut it down: a request it did not answer was abandoned by the shutdown, and
    /// no fault of the process.
    /// </summary>
    /// <param name="lost">What <see cref="AnsweredAsync"/> was given.</param>
    public static bool ShutDown(Task? lost) => lost is { IsCanceled: true };
}
