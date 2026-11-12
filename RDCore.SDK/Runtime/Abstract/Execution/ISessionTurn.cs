namespace RDCore.SDK.Runtime.Abstract.Execution;

/// <summary>
/// Whose turn it is to run code in a session: the program that is running, or an event that something outside the workspace raised.
/// </summary>
/// <remarks>
/// <para>
/// A session runs one thing at a time. The interpreter is not re-entrant from two threads, and the objects of a program are not safe to be read by one thread while another
/// changes them - but the objects of a library are not the program's to schedule. A host application raises an event when something happens to it, at a time that is its own,
/// on a thread that is its own; the procedures that handle the event are the program's, and run in the program's session.
/// </para>
/// <para>
/// <strong>MS-VBAL</strong> says when an event is handled only for the events of the language itself (<strong>§5.4.2.20</strong>: <c>RaiseEvent</c> invokes the handlers, there and
/// then), and for the others it says only that <c>DoEvents</c> "yields execution so that the operating system can process externally generated events"
/// (<strong>§6.1.2.8.1.5</strong>). The rest is how MS-VBA gets an event from a host application, which is a call from the server to the program, and a program can only take a
/// call when it is not busy: while it waits for a call of its own to return (the server raises <c>NewWorkbook</c> before <c>Workbooks.Add</c> returns, and the handlers may
/// answer it), when it yields with <c>DoEvents</c>, and when it is over. That is all of it: nothing is handled in the middle of a statement, and a program that does none of these
/// is a program no event reaches. Until the session is open to it (<see cref="IsOpenForEvents"/>) an event waits, and so does whatever raised it.
/// </para>
/// <para>
/// A program that is running holds the turn, and gives it up while it waits for a call to finish (<see cref="Yield"/>), which is what opens the session to the events.
/// </para>
/// <para>
/// ⚖️<strong>RDCore</strong> provides implementations of this interface <strong>licensed under GPLv3</strong>.
/// </para>
/// </remarks>
public interface ISessionTurn
{
    /// <summary>
    /// Takes the turn for a program that starts. Waits for an event that is being handled to be done.
    /// </summary>
    void Enter();

    /// <summary>
    /// Gives the turn back: the program is over, and the events that waited for it are handled.
    /// </summary>
    void Exit();

    /// <summary>
    /// Gives the turn up while the program waits for a call to finish, and takes it back when the returned scope is disposed.
    /// </summary>
    /// <remarks>
    /// A scope for code that is not in a turn does nothing: only what holds the turn can give it up.
    /// </remarks>
    IDisposable Yield();

    /// <summary>
    /// Lets the asynchronous events that wait be handled, and goes on when they have been: what <c>DoEvents</c> does.
    /// </summary>
    void Pump();

    /// <summary>
    /// Whether an event that no call was waiting for may be handled now: no program is running, one waits for a call, or one is pumping.
    /// </summary>
    /// <remarks>
    /// Not while the program waits at a stop (<see cref="Stopped"/>): no code of the program runs then, as none does in MS-VBA's break mode. The one exception is
    /// code that runs beside the program that waits (<see cref="Alongside"/>), whose calls may raise events of their own.
    /// </remarks>
    bool IsOpenForEvents { get; }

    /// <summary>
    /// Raised with <see langword="true"/> when a program starts running in the session and none was, and with <see langword="false"/> when the last one ends: what a
    /// host of windows needs to know to hold their messages while a program runs, as MS-VBA does (none is dispatched until the program waits for it, at <c>DoEvents</c>
    /// or a modal form, or ends). Raised on the thread that starts or ends the program.
    /// </summary>
    event Action<bool>? Running;

    /// <summary>
    /// Whether a program runs in the session: what <see cref="Running"/> last said.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Says that an asynchronous event waits for the session to be open to it, for as long as the returned scope is not disposed - which is how <see cref="Pump"/> knows
    /// there is something to wait for.
    /// </summary>
    IDisposable Waiting();

    /// <summary>
    /// Runs <paramref name="handle"/> in the session, and returns when it is done.
    /// </summary>
    /// <remarks>
    /// Called by whatever received the event, on whatever thread that is, once the event may be handled: a synchronous one at once, an asynchronous one when
    /// <see cref="IsOpenForEvents"/>. It blocks that thread, which is the point: the server that raised the event is waiting for the handlers to be done - they may set an
    /// argument it passed by reference, such as the <c>Cancel</c> of an event that can be cancelled.
    /// </remarks>
    /// <param name="handle">What handles the event.</param>
    void RunEvent(Action handle);

    /// <summary>
    /// Runs <paramref name="handle"/> in the session, as <see cref="RunEvent(Action)"/> does, for an event raised on a thread that has calls of its own to make while
    /// it waits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A thread that raises an event is often the only thread its objects can be called on: an apartment of COM objects, the thread of a form's window. While that
    /// thread waits for something - the turn, or the person at a debugger when a handler of the event is stopped at a breakpoint - the calls asked of it meanwhile still
    /// have to be made, or whoever asked for one waits for ever. <paramref name="serve"/> is how: it waits up to the time it is given, making those calls meanwhile.
    /// </para>
    /// <para>
    /// While the program waits at a stop (<see cref="Stopped"/>), the handlers of an event of the program wait for it to be resumed, by <paramref name="serve"/>, and run
    /// after it in the order their events came; an event raised by a call of code beside the program (<see cref="Alongside"/>) is handled at once.
    /// </para>
    /// <para>
    /// The default ignores <paramref name="serve"/>, and is <see cref="RunEvent(Action)"/>.
    /// </para>
    /// </remarks>
    /// <param name="handle">What handles the event.</param>
    /// <param name="serve">What the thread the event was raised on does with time it waits: it waits up to the time given, making the calls asked of it meanwhile.</param>
    void RunEvent(Action handle, Action<TimeSpan> serve) => RunEvent(handle);

    /// <summary>
    /// Whether the calling thread is running the handlers of an event of the program: one raised while the program gave the turn up (<see cref="Yield"/>,
    /// <see cref="Pump"/>), or while the handlers of another such event did.
    /// </summary>
    /// <remarks>
    /// Such a thread runs the program's code as the program's own thread does, and may be held where the program stops (<see cref="IExecutionGate.Suspend"/>). An event
    /// raised by a call of code beside a program that waits (<see cref="Alongside"/>) is not one: it is that code's. The default is <see langword="false"/>.
    /// </remarks>
    bool RunsAnEventOfTheProgram => false;

    /// <summary>
    /// What the calling thread does with time it waits, when it is running the handlers of an event that was raised with one
    /// (<see cref="RunEvent(Action, Action{TimeSpan})"/>): it waits up to the time given, making the calls asked of the thread meanwhile.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> for any other thread. The default is <see langword="null"/>.
    /// </remarks>
    Action<TimeSpan>? ServeOfTheCallingThread => null;

    /// <summary>
    /// Says that the program waits at a stop - a <c>Stop</c>, a breakpoint, an error a debugger waits at - for as long as the returned scope is not disposed.
    /// </summary>
    /// <remarks>
    /// No code of the program runs meanwhile, as none does in MS-VBA's break mode: an event of the program that comes waits for the program to be resumed, and is handled
    /// then; it is neither handled nor dropped. Called by whoever holds the program where it stops. The default does nothing.
    /// </remarks>
    /// <returns>The scope of the stop.</returns>
    IDisposable Stopped() => SessionTurnScope.None;

    /// <summary>
    /// Says that the calling thread runs code beside the program that waits at a stop - an expression a debugger evaluates, a statement typed at the stop - for as long as
    /// the returned scope is not disposed.
    /// </summary>
    /// <remarks>
    /// The calls that code makes give the turn up as the program's do (<see cref="Yield"/>), but only to the events they raise themselves: those are the code's, handled at
    /// once and not held where they stop, and an event of the program that waits goes on waiting. The default does nothing.
    /// </remarks>
    /// <returns>The scope of the code.</returns>
    IDisposable Alongside() => SessionTurnScope.None;

    /// <summary>
    /// What surrounds the handling of an event when no program is running to own the session: the host's, since what the handlers print has nowhere to go but where the host
    /// says. <see langword="null"/> when the host says nothing.
    /// </summary>
    /// <remarks>
    /// Called when such an event is about to be handled; the scope it returns is disposed when it has been. An event that is handled while a program runs prints where the program does.
    /// </remarks>
    Func<IDisposable>? EventScope { get; set; }
}

// the scope of the members an implementation of ISessionTurn does not provide: nothing to undo.
internal sealed class SessionTurnScope : IDisposable
{
    public static readonly SessionTurnScope None = new();

    private SessionTurnScope()
    {
    }

    public void Dispose()
    {
    }
}
