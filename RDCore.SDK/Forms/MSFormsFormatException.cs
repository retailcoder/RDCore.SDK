namespace RDCore.SDK.Forms;

/// <summary>
/// The reasons the designer data of a Microsoft Forms form cannot be read.
/// </summary>
public enum MSFormsFormatProblem
{
    /// <summary>
    /// The designer data is not a compound file ([MS-CFB]), the OLE storage Microsoft Forms persists a form to, and no
    /// <c>.frx</c> blob header precedes one.
    /// </summary>
    NotACompoundFile,

    /// <summary>
    /// The designer data is a compound file whose sector chains or directory are damaged.
    /// </summary>
    CorruptCompoundFile,

    /// <summary>
    /// A storage of a parent control (the form, a <c>Frame</c>, a <c>MultiPage</c> or one of its pages) has no form stream
    /// (<c>f</c>, [MS-OFORMS] 2.1.2.1.1).
    /// </summary>
    MissingFormStream,

    /// <summary>
    /// A structure of the designer data was written by a version of Microsoft Forms this reader does not know, or names a
    /// font or picture class it does not know ([MS-OFORMS] 2.4.7).
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// A stream ends before one of its structures does, or a structure's own size is smaller than its content.
    /// </summary>
    Truncated,

    /// <summary>
    /// A control's site names no class the form describes, or is nested deeper than the controls before it allow
    /// ([MS-OFORMS] 2.2.10.7, 2.5.13).
    /// </summary>
    InvalidControl,

    /// <summary>
    /// The sizes the sites of a parent control give their controls in its object stream (<c>o</c>) do not add up to that
    /// stream's length ([MS-OFORMS] 2.1.2.2.1).
    /// </summary>
    InvalidObjectStream,
}

/// <summary>
/// The exception that is thrown when the designer data of a Microsoft Forms form cannot be read.
/// </summary>
/// <param name="problem">What is wrong with the designer data.</param>
public sealed class MSFormsFormatException(MSFormsFormatProblem problem) : FormatException(MessageOf(problem))
{
    /// <summary>
    /// Gets what is wrong with the designer data.
    /// </summary>
    public MSFormsFormatProblem Problem { get; } = problem;

    private static string MessageOf(MSFormsFormatProblem problem) => problem switch
    {
        MSFormsFormatProblem.NotACompoundFile => MSFormsMessages.NotACompoundFile,
        MSFormsFormatProblem.CorruptCompoundFile => MSFormsMessages.CorruptCompoundFile,
        MSFormsFormatProblem.MissingFormStream => MSFormsMessages.MissingFormStream,
        MSFormsFormatProblem.UnsupportedVersion => MSFormsMessages.UnsupportedVersion,
        MSFormsFormatProblem.InvalidControl => MSFormsMessages.InvalidControl,
        MSFormsFormatProblem.InvalidObjectStream => MSFormsMessages.InvalidObjectStream,
        _ => MSFormsMessages.Truncated,
    };
}
