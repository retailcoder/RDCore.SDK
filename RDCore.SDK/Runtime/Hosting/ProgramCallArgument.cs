namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// An argument that passes the value of one of the invocation's calls (<see cref="ProgramInvocation.Calls"/>), an object, to a parameter whose declared
/// type is <c>Object</c>, <c>Variant</c>, or a class or interface the object is of (<strong>MS-VBAL §5.6.7</strong>, as <c>TypeOf</c> tells it). A
/// parameter of any other declared type, or an object of another class, is error 13 (Type mismatch).
/// </summary>
/// <param name="Parameter">The name of the parameter the argument is passed to.</param>
/// <param name="Call">
/// The <see cref="ProgramCall.Name"/> of one of the invocation's <see cref="ProgramInvocation.Calls"/>, made before the call the argument is of.
/// </param>
public sealed record class ProgramCallArgument(string Parameter, string Call) : ProgramArgument(Parameter);
