using System.Text;

namespace RDCore.SDK.Workspace;

/// <summary>
/// What a module of a project is, as the platform decides it: a standard module, a class module, or the code of a host's document.
/// </summary>
public enum ProjectModuleKind
{
    /// <summary>A standard module (a <c>.bas</c>): its text has no <c>VERSION</c> header.</summary>
    Standard = 0,
    /// <summary>
    /// A class or designer module: its text opens on a <c>VERSION</c> header (<see cref="ModuleHeader.IsClassModule"/>), or its file is an RDForms form's
    /// code (<see cref="ProjectModuleKinds.RDFormsModuleExtension"/>).
    /// </summary>
    Class = 1,
    /// <summary>The code of a host's document (<strong>MS-VBAL §4.2.1</strong>): the project names its document (<see cref="RDCoreModule.Super"/>).</summary>
    Document = 2,
}

/// <summary>
/// Decides what a module of a project is, and reads its text, the way every side of the platform does: a client that shows a project's modules, such as an
/// IDE's add-in, gets the answer the environment host and the language server get.
/// </summary>
/// <remarks>
/// A module whose entry in the project names a document (<see cref="RDCoreModule.Super"/>) is a document module, whatever its text and its file name. The code
/// of an RDForms form (a <see cref="RDFormsModuleExtension"/> file) is a class module: its text has no <c>VERSION</c> header, and its file's extension is what
/// says it. Any other module is a class module when its text has a <c>VERSION</c> header, and a standard module otherwise.
/// </remarks>
public static class ProjectModuleKinds
{
    /// <summary>
    /// The extension of the file that holds the code of an RDForms form: <c>.rdform</c>.
    /// </summary>
    /// <remarks>
    /// An RDForms form is two files of one base name, in one folder: its code, a class module whose text is its <c>Attribute VB_*</c> lines and then its
    /// declarations and procedures, with no <c>VERSION</c> header and no designer block; and its RDForms document, a <c>.xaml</c> beside it, which holds
    /// its controls.
    /// </remarks>
    public const string RDFormsModuleExtension = ".rdform";

    /// <summary>
    /// Whether the file at <paramref name="path"/> is the code of an RDForms form: its extension is <see cref="RDFormsModuleExtension"/>, whatever its case.
    /// </summary>
    /// <param name="path">The file's path or relative URI, or <see langword="null"/> for a module that has no file.</param>
    /// <returns><see langword="false"/> when <paramref name="path"/> is <see langword="null"/>.</returns>
    public static bool IsRDFormsModule(string? path)
        => path is not null && path.EndsWith(RDFormsModuleExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The name of the library that describes RDForms' forms and controls: <c>RDForms</c>.
    /// </summary>
    /// <remarks>
    /// A project that has the code of an RDForms form (<see cref="IsRDFormsModule"/>) references it without its project file listing it, as a VB6 project
    /// references <c>VB</c> (<c>SupportedLanguage.ImplicitReferencesOf</c>).
    /// </remarks>
    public const string RDFormsLibraryName = "RDForms";

    /// <summary>
    /// The kind of a module that the project says is the code of <paramref name="super"/>, or of no document when it is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// This overload has no file to go by, so the code of an RDForms form is a standard module here: <see cref="Of(DocClassType?, string?, string?)"/> takes
    /// its path.
    /// </remarks>
    /// <param name="super">What the project says the module's document is (<see cref="RDCoreModule.Super"/>).</param>
    /// <param name="source">The module's text (<see cref="ReadText"/>), or <see langword="null"/> when it could not be read.</param>
    public static ProjectModuleKind Of(DocClassType? super, string? source) => Of(super, source, path: null);

    /// <summary>
    /// The kind of a module that the project says is the code of <paramref name="super"/>, or of no document when it is <see langword="null"/>, in the file
    /// at <paramref name="path"/>.
    /// </summary>
    /// <param name="super">What the project says the module's document is (<see cref="RDCoreModule.Super"/>).</param>
    /// <param name="source">The module's text (<see cref="ReadText"/>), or <see langword="null"/> when it could not be read.</param>
    /// <param name="path">The module's file path or relative URI, or <see langword="null"/> for a module that has no file.</param>
    public static ProjectModuleKind Of(DocClassType? super, string? source, string? path)
        => super is not null ? ProjectModuleKind.Document
            : IsRDFormsModule(path) || ModuleHeader.IsClassModule(source) == true ? ProjectModuleKind.Class
            : ProjectModuleKind.Standard;

    /// <summary>
    /// The kind of <paramref name="module"/>, from its entry in the project (its document, and its file's extension) and its text.
    /// </summary>
    /// <param name="module">The module's entry in the project.</param>
    /// <param name="source">The module's text (<see cref="ReadText"/>), or <see langword="null"/> when it could not be read.</param>
    public static ProjectModuleKind Of(RDCoreModule module, string? source)
    {
        ArgumentNullException.ThrowIfNull(module);
        return Of(module.Super, source, module.RelativeUri);
    }

    /// <summary>
    /// The text of a module's file, decoded as the platform decodes it: UTF-8, unless a byte order mark says another Unicode encoding, and without that mark.
    /// </summary>
    /// <remarks>
    /// This is what <see cref="File.ReadAllText(string)"/> gives, the way the environment host and the language server read a module, so a client reads the
    /// same <c>VERSION</c> header they do: a header after a UTF-8 or UTF-16 mark is found, and the mark is no part of the text.
    /// </remarks>
    /// <param name="bytes">The file's bytes.</param>
    public static string ReadText(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var reader = new StreamReader(new MemoryStream(bytes, writable: false), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
