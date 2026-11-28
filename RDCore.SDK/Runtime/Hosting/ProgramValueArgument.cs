using System.Text.Json.Nodes;

namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// An argument given as a JSON value, converted to the parameter's declared type as the arguments of a <see cref="ProgramInvocation"/> are.
/// </summary>
/// <param name="Parameter">The name of the parameter the argument is passed to.</param>
/// <param name="Value">The value, as JSON.</param>
public sealed record class ProgramValueArgument(string Parameter, JsonNode? Value) : ProgramArgument(Parameter);
