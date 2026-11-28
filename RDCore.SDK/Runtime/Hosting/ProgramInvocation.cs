using System.Text.Json.Nodes;

namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// One call of a procedure of a composed program, by name, with its arguments; and, in the same run, the calls that make the values it needs
/// (<see cref="Calls"/>), the guards that may answer for it (<see cref="Guards"/>), the factory that makes the object it runs on (<see cref="Factory"/>),
/// the hooks of that object (<see cref="BeforeProcedure"/>, <see cref="OnError"/>), and the calls that turn its value into the invocation's
/// (<see cref="ResultCalls"/>).
/// </summary>
/// <remarks>
/// <para>
/// A machine matches each argument to the parameter of that name as RD-VBA compares names (case-insensitively), whatever the comparer of
/// <see cref="Arguments"/>, and converts its value to the parameter's declared type. Each of these ends the invocation with a run-time error
/// (<see cref="Platform.Protocol.ExecutionOutcome.RuntimeError"/>) before the procedure runs, as <strong>MS-VBAL §5.3.1.11</strong> maps named
/// arguments:
/// </para>
/// <list type="bullet">
/// <item><description>an argument that names no parameter, or two that name the same one (in different cases, or one in <see cref="Arguments"/> and one in
/// <see cref="ProcedureArguments"/>), is error 448 (Named argument not found);</description></item>
/// <item><description>a parameter with no argument that is not <c>Optional</c> is error 449 (Argument not optional);</description></item>
/// <item><description>a value that does not convert is error 13 (Type mismatch).</description></item>
/// </list>
/// <para>
/// A procedure of a class whose <c>VB_PredeclaredId</c> is <c>True</c> runs on the class's default instance (<strong>MS-VBAL §5.2.4.1.2</strong>), the
/// program's own: the first reference to it, from the program or from an invocation, makes it, and its <c>Class_Initialize</c> runs then. It keeps its
/// state from one invocation to the next, and no invocation terminates it. A <c>Set</c> of it to <c>Nothing</c> lets go of it, and so does
/// <see cref="IProgramMachine.ResetAsync"/>; the next reference makes another.
/// </para>
/// <para>
/// A procedure of any other class module runs on a new object of the class, made for the invocation as <c>New</c> makes one
/// (<strong>MS-VBAL §5.6.8</strong>). Its <c>Class_Initialize</c> runs before the procedure, and its <c>Class_Terminate</c> once the run has its value,
/// before the invocation ends (<strong>MS-VBAL §5.3.1.10</strong>). An error that the procedure leaves unhandled, or an <c>End</c>, ends the run, and the
/// run's end lets go of the object.
/// </para>
/// <para>
/// An error that <c>Class_Initialize</c> leaves unhandled ends the invocation with that run-time error, and the procedure does not run. An argument named
/// <c>Me</c> names no parameter. A document module is not invoked: its object is its host's document.
/// </para>
/// <para>
/// The whole invocation is one run, in this order: the <see cref="Calls"/>, the <see cref="Guards"/>, the <see cref="Factory"/>,
/// <see cref="BeforeProcedure"/>, the procedure, <see cref="OnError"/> when the procedure leaves an error unhandled, and the <see cref="ResultCalls"/>. A
/// guard or <see cref="BeforeProcedure"/> that answers for the procedure skips the rest, up to the <see cref="ResultCalls"/>. An error that any of them
/// leaves unhandled, other than the procedure's error that <see cref="OnError"/> answers for, or an <c>End</c> in any of them, ends the run, and nothing
/// after it runs.
/// </para>
/// <para>
/// The procedure's object is the object of a class module it runs on: the factory's object, the default instance, or the new object. A procedure of a
/// standard module has none, and its invocation runs no hook.
/// </para>
/// </remarks>
/// <param name="Module">
/// The name of the module that declares the procedure, as RD-VBA names it: a standard module, or a class module of the program (not one of a library).
/// With a <see cref="Factory"/>, the class whose default instance runs it.
/// </param>
/// <param name="Procedure">
/// The name of a <c>Public</c> <c>Sub</c>, <c>Function</c> or <c>Property Get</c> of that module; with a <see cref="Factory"/>, of the class of the object
/// the factory returns.
/// </param>
/// <param name="Arguments">The arguments, by the names of the parameters they are passed to; an <c>Optional</c> parameter may have none.</param>
public sealed record class ProgramInvocation(string Module, string Procedure, IReadOnlyDictionary<string, JsonNode?> Arguments)
{
    /// <summary>
    /// The arguments of the procedure that <see cref="Arguments"/> cannot give: a <see cref="ProgramCallArgument"/> passes the value of one of the
    /// <see cref="Calls"/>; a <see cref="ProgramValueArgument"/> passes a JSON value, as <see cref="Arguments"/> does. A
    /// <see cref="ProgramErrorArgument"/> or a <see cref="ProgramResultArgument"/> is not one of them.
    /// </summary>
    public IReadOnlyList<ProgramArgument> ProcedureArguments { get; init; } = [];

    /// <summary>
    /// The calls made first, in this order, each a call whose value is an object. Each value is kept under its call's <see cref="ProgramCall.Name"/> for
    /// the rest of the run, and a <see cref="ProgramCallArgument"/> passes it to a parameter of a later call, a guard, the factory, a hook, a result call
    /// or the procedure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The run's end lets go of the value of each call that is not <see cref="ProgramCall.MachineHeld"/>, in the reverse of the order the calls were made,
    /// once the invocation's value is taken: an object that nothing else holds runs its <c>Class_Terminate</c> then (<strong>MS-VBAL §5.3.1.10</strong>).
    /// An invocation refused at its arguments, or at the factory's value, lets go of them the same way.
    /// </para>
    /// <para>
    /// A call that names a module or a procedure the program does not have, as <see cref="ProgramCall"/> describes them, ends the invocation as
    /// <see cref="Platform.Protocol.ExecutionOutcome.NotFound"/> before anything runs. A call whose value is not an object is error 13 (Type mismatch).
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProgramCall> Calls { get; init; } = [];

    /// <summary>
    /// The guards, called after the <see cref="Calls"/> in this order, each a function whose value is an object or <c>Nothing</c>. A guard that returns
    /// <c>Nothing</c> lets the run go on; a guard that returns an object answers for the procedure: the object is the run's value, and the run goes on at
    /// the <see cref="ResultCalls"/>. A guard whose value is not an object is error 13 (Type mismatch).
    /// </summary>
    /// <remarks>
    /// A guard has a <see cref="ProgramCall.Procedure"/> and is not <see cref="ProgramCall.MachineHeld"/>. No <see cref="ProgramCallArgument"/> passes its
    /// value. The run's end lets go of the object a guard answers with, after the invocation's value is taken.
    /// </remarks>
    public IReadOnlyList<ProgramCall> Guards { get; init; } = [];

    /// <summary>
    /// The factory that makes the object the procedure runs on, after the <see cref="Guards"/>: a <c>Public</c> <c>Function</c> or <c>Property Get</c> of
    /// the class that <see cref="Module"/> names, whose <c>VB_PredeclaredId</c> is <c>True</c>. <see langword="null"/> when the procedure runs on its
    /// module.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The factory runs on the class's default instance (<strong>MS-VBAL §5.2.4.1.2</strong>), which keeps its state as it does for any procedure of the
    /// class. The procedure then runs on the object it returns, as a member of that object's class: the run's end lets go of the object, as it lets go of
    /// the values of the calls, before them.
    /// </para>
    /// <para>
    /// A factory that returns <c>Nothing</c> is error 91 (Object variable not set), and one that returns a value that is not an object, error 13 (Type
    /// mismatch). An object that is not of a class of the program, or whose class has no <c>Public</c> <c>Sub</c>, <c>Function</c> or <c>Property Get</c>
    /// named <see cref="Procedure"/>, is error 438 (Object doesn't support this property or method). The procedure does not run in any of them. A class
    /// that is not predeclared, or a factory that is not a <c>Public</c> <c>Function</c> or <c>Property Get</c> of it, ends the invocation as
    /// <see cref="Platform.Protocol.ExecutionOutcome.NotFound"/> before anything runs.
    /// </para>
    /// </remarks>
    public ProgramFactoryCall? Factory { get; init; }

    /// <summary>
    /// The hook called before the procedure, when the procedure's object implements the hook's interface. Its value is taken as a guard's is: an object
    /// answers for the procedure, which does not run; <c>Nothing</c> lets the procedure run; any other value is error 13 (Type mismatch).
    /// <see langword="null"/> when there is none.
    /// </summary>
    public ProgramHookCall? BeforeProcedure { get; init; }

    /// <summary>
    /// The hook called when the procedure leaves a run-time error unhandled, when the procedure's object implements the hook's interface. A
    /// <see cref="ProgramErrorArgument"/> passes the error's number or description to it. <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The run handles the error as the error handler of a caller does: the procedure's object and the values of the <see cref="Calls"/> are kept, and the
    /// hook runs. A hook that returns an object answers for the procedure: the object is the run's value, the error is cleared as leaving an error handler
    /// clears it (<strong>MS-VBAL §6.1.3.2.1.1</strong>), and the run goes on at the <see cref="ResultCalls"/>. A hook that returns <c>Nothing</c>, or a
    /// value that is not an object, leaves the procedure's error unhandled: it ends the run, as it does with no hook.
    /// </para>
    /// <para>
    /// An error that the hook leaves unhandled ends the run with that error. The hook does not run for an <c>End</c>, an interruption, an internal error,
    /// or an error raised before the procedure runs.
    /// </para>
    /// </remarks>
    public ProgramHookCall? OnError { get; init; }

    /// <summary>
    /// The calls made once the run has its value, in this order: the procedure's value (<c>Empty</c> for a <c>Sub</c>), or the object of the guard or the
    /// hook that answered for it. A <see cref="ProgramResultArgument"/> passes the run's value to a parameter of each, and each call's value becomes the
    /// run's value: the last one's is the invocation's value.
    /// </summary>
    /// <remarks>
    /// A result call has a <see cref="ProgramCall.Procedure"/> and is not <see cref="ProgramCall.MachineHeld"/>. Its value may be of any type, and no
    /// <see cref="ProgramCallArgument"/> passes it. The run's end lets go of each one's value after the invocation's value is taken.
    /// </remarks>
    public IReadOnlyList<ProgramCall> ResultCalls { get; init; } = [];
}
