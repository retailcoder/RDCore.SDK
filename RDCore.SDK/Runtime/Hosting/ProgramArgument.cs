namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// An argument of a <see cref="ProgramCall"/> or a <see cref="ProgramFactoryCall"/>, by the name of the parameter it is passed to: a JSON value
/// (<see cref="ProgramValueArgument"/>), or the value of an earlier call of the invocation (<see cref="ProgramCallArgument"/>).
/// </summary>
public abstract record class ProgramArgument
{
    private protected ProgramArgument(string parameter) => Parameter = parameter;

    /// <summary>
    /// The name of the parameter the argument is passed to, compared as RD-VBA compares names (case-insensitively).
    /// </summary>
    public string Parameter { get; init; }
}
