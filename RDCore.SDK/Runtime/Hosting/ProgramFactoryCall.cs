namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// The factory of an invocation (<see cref="ProgramInvocation.Factory"/>): a function of a predeclared class that runs on the class's default instance and
/// returns the object the invocation's procedure runs on.
/// </summary>
/// <remarks>
/// Its arguments bind to the function's parameters as the arguments of a <see cref="ProgramCall"/> do.
/// </remarks>
/// <param name="Procedure">
/// The name of a <c>Public</c> <c>Function</c> or <c>Property Get</c> of the class that <see cref="ProgramInvocation.Module"/> names.
/// </param>
/// <param name="Arguments">The arguments, by the names of the parameters they are passed to; an <c>Optional</c> parameter may have none.</param>
public sealed record class ProgramFactoryCall(string Procedure, IReadOnlyList<ProgramArgument> Arguments);
