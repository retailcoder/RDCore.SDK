namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// An argument of a call of an invocation, by the name of the parameter it is passed to: a JSON value (<see cref="ProgramValueArgument"/>), the value of
/// one of the invocation's calls (<see cref="ProgramCallArgument"/>), a part of an error (<see cref="ProgramErrorArgument"/>), or the run's value
/// (<see cref="ProgramResultArgument"/>).
/// </summary>
public abstract record class ProgramArgument
{
    private protected ProgramArgument(string parameter) => Parameter = parameter;

    /// <summary>
    /// The name of the parameter the argument is passed to, compared as RD-VBA compares names (case-insensitively).
    /// </summary>
    public string Parameter { get; init; }
}
