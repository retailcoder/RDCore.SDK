using System.Text.Json.Nodes;

namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// One call of a procedure of a composed program, by name, with its arguments as JSON values.
/// </summary>
/// <remarks>
/// A machine matches each argument to the parameter of that name as RD-VBA compares names (case-insensitively), whatever the comparer of
/// <see cref="Arguments"/>, and converts its value to the parameter's declared type. Each of these ends the invocation with a run-time error
/// (<see cref="Platform.Protocol.ExecutionOutcome.RuntimeError"/>) before the procedure runs, as <strong>MS-VBAL §5.3.1.11</strong> maps named
/// arguments:
/// <list type="bullet">
/// <item><description>an argument that names no parameter, or two that name the same one in different cases, is error 448 (Named argument not
/// found);</description></item>
/// <item><description>a parameter with no argument that is not <c>Optional</c> is error 449 (Argument not optional);</description></item>
/// <item><description>a value that does not convert is error 13 (Type mismatch).</description></item>
/// </list>
/// </remarks>
/// <param name="Module">The name of the module that declares the procedure, as RD-VBA names it.</param>
/// <param name="Procedure">The name of a <c>Public</c> <c>Sub</c>, <c>Function</c> or <c>Property Get</c> of that module.</param>
/// <param name="Arguments">The arguments, by the names of the parameters they are passed to; an <c>Optional</c> parameter may have none.</param>
public sealed record class ProgramInvocation(string Module, string Procedure, IReadOnlyDictionary<string, JsonNode?> Arguments);
