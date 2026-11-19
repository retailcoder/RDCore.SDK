using RDCore.SDK.Platform.Channels;
using System.IO.Pipes;

namespace RDCore.Tests.Platform;

/// <summary>
/// A channel of calls between two processes of the platform: each end calls methods of the other by name, waits for the answer, and is told when there is none.
/// </summary>
[TestClass]
[TestCategory("Platform channels")]
public sealed class CallChannelTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // two ends of a pipe, each with a channel over it: the methods are registered before the channels start.
    private sealed class Ends : IDisposable
    {
        private readonly NamedPipeServerStream _server;
        private readonly NamedPipeClientStream _client;

        private Ends(NamedPipeServerStream server, NamedPipeClientStream client, Func<Stream, Stream> serving)
        {
            _server = server;
            _client = client;
            Serving = new CallChannel(serving(server));
            Calling = new CallChannel(client);
        }

        public CallChannel Serving { get; }

        public CallChannel Calling { get; }

        // the serving end's channel goes over what `serving` makes of its end of the pipe.
        public static async Task<Ends> ConnectAsync(Func<Stream, Stream>? serving = null)
        {
            var name = $"rdcore-channel-test-{Guid.NewGuid():N}";
            var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            var accepted = server.WaitForConnectionAsync();
            await client.ConnectAsync(10_000);
            await accepted;
            return new Ends(server, client, serving ?? (pipe => pipe));
        }

        public void Start()
        {
            Serving.Start();
            Calling.Start();
        }

        public void Dispose()
        {
            Calling.Dispose();
            Serving.Dispose();
            _client.Dispose();
            _server.Dispose();
        }
    }

    // an end of a pipe that takes no write longer than `most` bytes, as a pipe takes none longer than the system can lock for it, and that breaks for good
    // once it has taken `breaksAfter` bytes, in the middle of a write.
    private sealed class Narrow(Stream pipe, int most, long breaksAfter = long.MaxValue) : Stream
    {
        private long _taken;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => pipe.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => pipe.Read(buffer);

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > most)
            {
                throw new IOException("Insufficient system resources exist to complete the requested service.");
            }

            var left = breaksAfter - _taken;
            if (buffer.Length > left)
            {
                pipe.Write(buffer[..(int)left]);
                pipe.Flush();
                _taken = breaksAfter;
                throw new IOException("The pipe has been ended.");
            }

            pipe.Write(buffer);
            _taken += buffer.Length;
        }

        public override void Flush() => pipe.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pipe.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed record class Greeting(string Name, int Times);

    // a caller that waits blocks its thread, as a program does: it gets a thread of its own, rather than one the pool shares with the tests that run alongside.
    private static Task<T> OnAThreadOfItsOwn<T>(Func<T> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        new Thread(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception exception)
            {
                done.SetException(exception);
            }
        }) { IsBackground = true }.Start();
        return done.Task;
    }

    [TestMethod]
    public async Task ACall_IsAnsweredWithWhatItsHandlerReturns()
    {
        using var ends = await Ends.ConnectAsync();
        ends.Serving.Handle<Greeting, string>("greet", greeting => string.Concat(Enumerable.Repeat($"quack {greeting.Name}! ", greeting.Times)).Trim());
        ends.Start();

        Assert.AreEqual("quack Rick! quack Rick!", ends.Calling.Call<Greeting, string>("greet", new Greeting("Rick", 2)));
        Assert.AreEqual("quack Duck!", await ends.Calling.CallAsync<Greeting, string>("greet", new Greeting("Duck", 1)).WaitAsync(Patience));
    }

    [TestMethod]
    public async Task WhatAHandlerThrows_IsTheCallersFailure()
    {
        using var ends = await Ends.ConnectAsync();
        ends.Serving.Handle<int, int>("divide", divisor => 42 / divisor);
        ends.Start();

        var failure = Assert.ThrowsExactly<CallChannelException>(() => ends.Calling.Call<int, int>("divide", 0));

        Assert.AreEqual(new DivideByZeroException().Message, failure.Message);
        Assert.AreEqual(21, ends.Calling.Call<int, int>("divide", 2), "a failure is the one call's, and the channel goes on");
    }

    [TestMethod]
    public async Task AMethodTheOtherEndHasNot_IsAFailureThatSaysWhich()
    {
        using var ends = await Ends.ConnectAsync();
        ends.Start();

        var failure = Assert.ThrowsExactly<CallChannelException>(() => ends.Calling.Call<int, int>("nothing", 0));

        StringAssert.Contains(failure.Message, "nothing");
    }

    // what an event of a server is: a call is being answered, and its handler calls the end that made it - which answers while it waits for its own answer.
    [TestMethod]
    public async Task AHandlerThatCallsTheOtherEnd_IsAnsweredWhileTheCallItAnswersWaits()
    {
        using var ends = await Ends.ConnectAsync();
        ends.Serving.Handle<int, int>("outer", value => ends.Serving.Call<int, int>("inner", value) + 1);
        ends.Calling.Handle<int, int>("inner", value => value * 10);
        ends.Start();

        var answer = await OnAThreadOfItsOwn(() => ends.Calling.Call<int, int>("outer", 4)).WaitAsync(Patience);

        Assert.AreEqual(41, answer);
    }

    [TestMethod]
    public async Task CallsMadeAtOnce_EachGetTheirOwnAnswer()
    {
        using var ends = await Ends.ConnectAsync();
        ends.Serving.Handle<int, int>("square", value => value * value);
        ends.Start();

        var answers = await Task.WhenAll(Enumerable.Range(1, 20).Select(value => OnAThreadOfItsOwn(() => ends.Calling.Call<int, int>("square", value)))).WaitAsync(Patience);

        CollectionAssert.AreEqual(Enumerable.Range(1, 20).Select(value => value * value).ToArray(), answers);
    }

    [TestMethod]
    public async Task WhenTheOtherEndGoesAway_ACallThatWaitsFails_AndTheChannelIsClosed()
    {
        using var ends = await Ends.ConnectAsync();
        using var answering = new ManualResetEventSlim();
        ends.Serving.Handle<int, int>("stall", value =>
        {
            answering.Set();
            Thread.Sleep(Timeout.Infinite);
            return value;
        });
        ends.Start();

        var stalled = ends.Calling.CallAsync<int, int>("stall", 1);
        Assert.IsTrue(answering.Wait(Patience));
        ends.Serving.Dispose();

        await Assert.ThrowsExactlyAsync<CallChannelException>(() => stalled.WaitAsync(Patience));
        await ends.Calling.Closed.WaitAsync(Patience);
        Assert.ThrowsExactly<CallChannelException>(() => ends.Calling.Call<int, int>("stall", 2));
    }

    // the answer of a whole column of a worksheet is 115 MB, more than a busy system can lock for one write to a pipe: a frame is written in pieces a
    // pipe takes.
    [TestMethod]
    public async Task AnAnswerLongerThanAPipeTakesAtOnce_GoesInWritesItTakes()
    {
        using var ends = await Ends.ConnectAsync(pipe => new Narrow(pipe, most: 1024 * 1024));
        ends.Serving.Handle<int, string>("column", rows => new string('1', rows));
        ends.Start();

        var answer = await OnAThreadOfItsOwn(() => ends.Calling.Call<int, string>("column", 4 * 1024 * 1024)).WaitAsync(Patience);

        Assert.AreEqual(4 * 1024 * 1024, answer.Length);
    }

    // a frame that a failed write cut short leaves the other end waiting for the rest of it: the end that failed to write it closes the pipe, and the call
    // that waits for the answer fails.
    [TestMethod]
    public async Task AnAnswerCutShort_FailsTheCall_AndClosesBothEnds()
    {
        using var ends = await Ends.ConnectAsync(pipe => new Narrow(pipe, most: int.MaxValue, breaksAfter: 1000));
        ends.Serving.Handle<int, string>("column", rows => new string('1', rows));
        ends.Start();

        var call = OnAThreadOfItsOwn(() => ends.Calling.Call<int, string>("column", 4000));

        await Assert.ThrowsExactlyAsync<CallChannelException>(() => call.WaitAsync(Patience));
        await ends.Serving.Closed.WaitAsync(Patience);
        await ends.Calling.Closed.WaitAsync(Patience);
    }
}
