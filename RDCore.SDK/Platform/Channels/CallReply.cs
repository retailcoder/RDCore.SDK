namespace RDCore.SDK.Platform.Channels;

/// <summary>
/// What answers a call over a <see cref="CallChannel"/> that its handler answers later (<see cref="CallChannel.Handle{TCall, TAnswer}(string, Action{TCall, CallReply{TAnswer}})"/>),
/// from whichever thread makes the call.
/// </summary>
/// <remarks>
/// A call is answered once: the first answer or failure is written to the other end, and what comes after it is not. A call that is never answered waits until
/// the channel closes.
/// </remarks>
/// <typeparam name="TAnswer">What the call answers.</typeparam>
public sealed class CallReply<TAnswer>
{
    private readonly CallChannel _channel;
    private readonly long _number;
    private int _replied;

    internal CallReply(CallChannel channel, long number)
    {
        _channel = channel;
        _number = number;
    }

    /// <summary>
    /// Answers the call. The thread that answers writes the answer to the other end.
    /// </summary>
    /// <param name="answer">The answer.</param>
    public void Answer(TAnswer answer)
    {
        if (Interlocked.Exchange(ref _replied, 1) == 0)
        {
            _channel.Reply(_number, answered: true, CallChannel.Serialized(answer));
        }
    }

    /// <summary>
    /// Fails the call: the caller's <see cref="CallChannelException"/> says what <paramref name="failure"/> says.
    /// </summary>
    /// <param name="failure">Why the call failed.</param>
    public void Fail(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        if (Interlocked.Exchange(ref _replied, 1) == 0)
        {
            _channel.Reply(_number, answered: false, System.Text.Encoding.UTF8.GetBytes(failure.Message));
        }
    }
}
