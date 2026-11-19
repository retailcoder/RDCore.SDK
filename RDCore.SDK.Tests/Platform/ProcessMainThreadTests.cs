using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// The main thread of a process, served by its entry point while the program runs on another thread: work posted to it runs on it, and it returns the
/// program's exit code once the program ends.
/// </summary>
[TestClass]
[TestCategory("Platform")]
public sealed class ProcessMainThreadTests
{
    // runs a main thread on a thread of the test's own, which plays the part of the process's, and answers what it returned and the thread it was.
    private static (int Result, Thread Main) RunOnAThreadOfItsOwn(ProcessMainThread main, Func<Task<int>> program)
    {
        var result = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = main.Run(program);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw failure;
        }

        return (result, thread);
    }

    [TestMethod]
    public void PostedWork_RunsOnTheThreadThatServes_AndTheProgramsResultIsReturned()
    {
        var main = new ProcessMainThread();
        Thread? ranOn = null;

        var (result, thread) = RunOnAThreadOfItsOwn(main, async () =>
        {
            await main.PostAsync(_ => ranOn = Thread.CurrentThread);
            return 7;
        });

        Assert.AreEqual(7, result);
        Assert.AreSame(thread, ranOn);
    }

    // a window's loop runs until the program ends: the token it is given is cancelled then, and the main thread returns once the loop does.
    [TestMethod]
    public void WorkThatRunsUntilTheProgramEnds_IsCancelledWhenItEnds()
    {
        var main = new ProcessMainThread();
        using var looping = new ManualResetEventSlim();
        Task? loop = null;

        var (result, _) = RunOnAThreadOfItsOwn(main, () =>
        {
            loop = main.PostAsync(token =>
            {
                looping.Set();
                token.WaitHandle.WaitOne();
            });

            looping.Wait();
            return Task.FromResult(3);
        });

        Assert.AreEqual(3, result);
        Assert.IsTrue(loop!.IsCompletedSuccessfully);
    }

    [TestMethod]
    public void WorkThatThrows_FaultsItsTask_AndTheMainThreadRunsTheNext()
    {
        var main = new ProcessMainThread();
        var next = false;
        Exception? thrown = null;

        var (result, _) = RunOnAThreadOfItsOwn(main, async () =>
        {
            try
            {
                await main.PostAsync(_ => throw new InvalidOperationException("setup failed"));
            }
            catch (InvalidOperationException exception)
            {
                thrown = exception;
            }

            await main.PostAsync(_ => next = true);
            return 0;
        });

        Assert.AreEqual(0, result);
        Assert.AreEqual("setup failed", thrown?.Message);
        Assert.IsTrue(next);
    }

    [TestMethod]
    public void WorkPostedAfterTheProgramEnded_IsRefused()
    {
        var main = new ProcessMainThread();
        RunOnAThreadOfItsOwn(main, () => Task.FromResult(0));

        Assert.ThrowsExactly<InvalidOperationException>(() => main.PostAsync(_ => { }));
    }

    [TestMethod]
    public void WhatTheProgramThrows_IsThrown_OnceThePostedWorkReturned()
    {
        var main = new ProcessMainThread();
        Task? posted = null;

        var thrown = Assert.ThrowsExactly<FormatException>(() => RunOnAThreadOfItsOwn(main, () =>
        {
            posted = main.PostAsync(_ => { });
            throw new FormatException("the program failed");
        }));

        Assert.AreEqual("the program failed", thrown.Message);
        Assert.IsTrue(posted!.IsCompleted);
    }

    [TestMethod]
    public void AMainThread_IsRunOnce()
    {
        var main = new ProcessMainThread();
        RunOnAThreadOfItsOwn(main, () => Task.FromResult(0));

        Assert.ThrowsExactly<InvalidOperationException>(() => main.Run(() => Task.FromResult(0)));
    }

    // the process's own is the one its entry point serves, while it serves it, and none before or after.
    [TestMethod]
    public void TheProcesssMainThread_IsCurrentWhileItIsServed()
    {
        ProcessMainThread? during = null;
        var refused = false;

        var result = ProcessMainThread.Serve(async () =>
        {
            during = ProcessMainThread.Current;
            refused = Assert.ThrowsExactly<InvalidOperationException>(() => ProcessMainThread.Serve(() => Task.FromResult(1))) is not null;
            await during!.PostAsync(_ => { });
            return 5;
        });

        Assert.AreEqual(5, result);
        Assert.IsNotNull(during);
        Assert.IsTrue(refused);
        Assert.IsNull(ProcessMainThread.Current);
    }
}
