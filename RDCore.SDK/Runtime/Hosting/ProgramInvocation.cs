using System.Text.Json.Nodes;

namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// One call of a procedure of a composed program, by name, with its arguments as JSON values; and, before it in the same run, the calls whose values it
/// needs (<see cref="Calls"/>) and the factory that makes the object it runs on (<see cref="Factory"/>).
/// </summary>
/// <remarks>
/// <para>
/// A machine matches each argument to the parameter of that name as RD-VBA compares names (case-insensitively), whatever the comparer of
/// <see cref="Arguments"/>, and converts its value to the parameter's declared type. Each of these ends the invocation with a run-time error
/// (<see cref="Platform.Protocol.ExecutionOutcome.RuntimeError"/>) before the procedure runs, as <strong>MS-VBAL §5.3.1.11</strong> maps named
/// arguments:
/// </para>
/// <list type="bullet">
/// <item><description>an argument that names no parameter, or two that name the same one in different cases, is error 448 (Named argument not
/// found);</description></item>
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
/// (<strong>MS-VBAL §5.6.8</strong>). Its <c>Class_Initialize</c> runs before the procedure, and its <c>Class_Terminate</c> once the procedure has
/// returned and its value is taken, before the invocation ends (<strong>MS-VBAL §5.3.1.10</strong>). An error that the procedure leaves unhandled, or an
/// <c>End</c>, ends the run, and the run's end lets go of the object.
/// </para>
/// <para>
/// An error that <c>Class_Initialize</c> leaves unhandled ends the invocation with that run-time error, and the procedure does not run. An argument named
/// <c>Me</c> names no parameter. A document module is not invoked: its object is its host's document.
/// </para>
/// <para>
/// The whole invocation is one run: its <see cref="Calls"/>, then its <see cref="Factory"/>, then the procedure. An error that any of them leaves
/// unhandled, or an <c>End</c> in any of them, ends the run, and nothing after it runs.
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
    /// The calls made before the procedure, in this order, each a function of a standard module whose value is an object. Each value is kept under its
    /// call's <see cref="ProgramCall.Name"/> for the rest of the run, and is passed to a parameter of a later call, or of the factory, by a
    /// <see cref="ProgramCallArgument"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The run's end lets go of the value of each call that is not <see cref="ProgramCall.MachineHeld"/>, in the reverse of the order the calls were made,
    /// once the procedure's value is taken: an object that nothing else holds runs its <c>Class_Terminate</c> then (<strong>MS-VBAL §5.3.1.10</strong>).
    /// An invocation refused at its arguments, or at the factory's value, lets go of them the same way.
    /// </para>
    /// <para>
    /// A call whose module the program does not have as a standard module, or whose procedure that module does not declare as a <c>Public</c>
    /// <c>Function</c> or <c>Property Get</c>, ends the invocation as <see cref="Platform.Protocol.ExecutionOutcome.NotFound"/> before anything runs. A call whose value
    /// is not an object is error 13 (Type mismatch).
    /// </para>
    /// </remarks>
    public IReadOnlyList<ProgramCall> Calls { get; init; } = [];

    /// <summary>
    /// The factory that makes the object the procedure runs on, after the <see cref="Calls"/>: a <c>Public</c> <c>Function</c> or <c>Property Get</c> of the
    /// class that <see cref="Module"/> names, whose <c>VB_PredeclaredId</c> is <c>True</c>. <see langword="null"/> when the procedure runs on its module.
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
    /// that is not predeclared, or a factory that is no <c>Public</c> <c>Function</c> or <c>Property Get</c> of it, ends the invocation as
    /// <see cref="Platform.Protocol.ExecutionOutcome.NotFound"/> before anything runs.
    /// </para>
    /// </remarks>
    public ProgramFactoryCall? Factory { get; init; }
}
