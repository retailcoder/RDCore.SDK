namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// An argument of one of the <see cref="ProgramInvocation.ResultCalls"/> that passes the run's value: the procedure's, the object of the guard or the
/// hook that answered for it, or the value of the result call before.
/// </summary>
/// <remarks>
/// An object passes as a <see cref="ProgramCallArgument"/> passes one. A value that is not an object passes to a parameter whose declared type is
/// <c>Variant</c>; a parameter of any other declared type is error 13 (Type mismatch).
/// </remarks>
/// <param name="Parameter">The name of the parameter the argument is passed to.</param>
public sealed record class ProgramResultArgument(string Parameter) : ProgramArgument(Parameter);
