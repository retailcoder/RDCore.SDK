namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// An argument of the <see cref="ProgramInvocation.OnError"/> hook that passes a part of the error the procedure left unhandled: its number
/// (<c>Err.Number</c>) as a JSON number, or its description (<c>Err.Description</c>) as a JSON string. The value converts to the parameter's declared type
/// as the value of a <see cref="ProgramValueArgument"/> does.
/// </summary>
/// <param name="Parameter">The name of the parameter the argument is passed to.</param>
/// <param name="Part">The part of the error it passes.</param>
public sealed record class ProgramErrorArgument(string Parameter, ProgramErrorPart Part) : ProgramArgument(Parameter);
