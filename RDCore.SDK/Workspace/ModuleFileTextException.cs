namespace RDCore.SDK.Workspace;

/// <summary>
/// Why a module file's text cannot be edited or written (<see cref="ModuleFileText"/>).
/// </summary>
public enum ModuleFileTextFailure
{
    /// <summary>
    /// The edited text has a character the file's encoding cannot write.
    /// </summary>
    NotRepresentable,

    /// <summary>
    /// The file has bytes its encoding has no character for: an edit would write the replacement characters it was read with.
    /// </summary>
    NotLossless,
}

/// <summary>
/// A module file's text that cannot be edited or written in the file's encoding; nothing is written.
/// </summary>
public sealed class ModuleFileTextException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="failure">Why the text cannot be edited or written.</param>
    /// <param name="message">What a user is told, in the language of the user interface.</param>
    public ModuleFileTextException(ModuleFileTextFailure failure, string message)
        : base(message)
    {
        Failure = failure;
    }

    /// <summary>
    /// Why the text cannot be edited or written.
    /// </summary>
    public ModuleFileTextFailure Failure { get; }
}
