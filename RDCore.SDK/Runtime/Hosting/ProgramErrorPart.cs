namespace RDCore.SDK.Runtime.Hosting;

/// <summary>
/// The part of an error that a <see cref="ProgramErrorArgument"/> passes (<strong>MS-VBAL §6.1.3.2</strong>, the <c>Err</c> class).
/// </summary>
public enum ProgramErrorPart
{
    /// <summary>
    /// The error's number: <c>Err.Number</c>.
    /// </summary>
    Number,

    /// <summary>
    /// The error's description: <c>Err.Description</c>.
    /// </summary>
    Description,
}
