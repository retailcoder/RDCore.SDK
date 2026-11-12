namespace RDCore.SDK.Runtime.Abstract.Execution;

/// <summary>
/// Which run-time errors a program under a debugger waits at.
/// </summary>
public enum ErrorBreakMode
{
    /// <summary>
    /// No error: an error is dealt with as the program says, and the debugger does not see it.
    /// </summary>
    None,

    /// <summary>
    /// The errors that nothing on the call stack has a handler for, and that will end the program.
    /// </summary>
    Unhandled,

    /// <summary>
    /// Every error, where it is raised, whatever handler there is to catch it.
    /// </summary>
    All,
}
