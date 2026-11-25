using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model;

namespace RDCore.SDK.Workspace;

/// <summary>
/// A language the platform serves: what it is called to a client, which files are written in it, and what the environment it runs in
/// is like - which is what tells one dialect of the family from another.
/// </summary>
/// <remarks>
/// The platform is one implementation of the <strong>MS-VBAL</strong> semantics for several members of the BASIC family
/// (<see cref="SupportedLanguages"/>), and a language is the dialect table behind it: the pieces a dialect decides stay behind this type
/// rather than being re-decided wherever they matter.
/// </remarks>
public class SupportedLanguage
{
    /// <summary>
    /// Creates the definition of a language.
    /// </summary>
    /// <param name="id">The language identifier a client names the language by (<c>languageId</c>).</param>
    /// <param name="name">What the language is called.</param>
    /// <param name="fileTypes">The patterns of the files written in it.</param>
    public SupportedLanguage(string id, string name, params string[] fileTypes)
    {
        Id = id;
        Name = name;
        FileTypes = fileTypes;
    }

    /// <summary>The language identifier a client names the language by (<c>languageId</c>).</summary>
    public string Id { get; }

    /// <summary>What the language is called.</summary>
    public string Name { get; }

    /// <summary>The patterns of the files written in the language.</summary>
    public string[] FileTypes { get; }

    /// <summary>
    /// The patterns of the files that are the modules of a workspace of the language: those a folder with no project file is made a project of
    /// (<see cref="IProjectFileLoader.LoadOrSynthesizeAsync"/>). They are the language's own files (<see cref="FileTypes"/>), unless the language says
    /// otherwise.
    /// </summary>
    /// <remarks>
    /// A BASIC's are RD-VBA's, except the code of an RDForms form (<see cref="SupportedLanguages.BASIC"/>): its own file, a <c>.rdc</c>, is a program of the
    /// shell, as the lines typed at its prompt are, and is not a module of a project.
    /// </remarks>
    public string[] ModuleFileTypes
    {
        get => _moduleFileTypes ?? FileTypes;
        init => _moduleFileTypes = value;
    }

    private readonly string[]? _moduleFileTypes;

    /// <summary>
    /// Where the variable that a reference to an undeclared name declares lives (<strong>MS-VBAL §5.6.10</strong>).
    /// </summary>
    /// <remarks>
    /// <see cref="ImplicitDeclarationScope.Procedure"/> - a local of the procedure - is VBA's and VB6's. <see cref="ImplicitDeclarationScope.Module"/>
    /// is a BASIC's: a variable of the module, so that what one line assigns the next one reads.
    /// </remarks>
    public ImplicitDeclarationScope ImplicitDeclarationScope { get; init; } = ImplicitDeclarationScope.Procedure;

    /// <summary>
    /// Whether a <c>Print</c> statement with no file number - <c>Print "x"</c> - is a statement of the language.
    /// </summary>
    /// <remarks>
    /// In VB6 it is the <c>Print</c> member of the form or report it is written in, so it exists only where there is one. A BASIC has it as its
    /// own: it writes to the output of the program.
    /// <para>
    /// In VBA <c>Print</c> is a reserved identifier: it is illegal as the name of anything, it has no semantics, and it is not a recognized statement
    /// inside a procedure. Only the Immediate window accepts a bare <c>Print</c>, where it writes what <c>Debug.Print</c> does - and the Immediate window
    /// is not procedure scope.
    /// </para>
    /// </remarks>
    public bool HasBarePrint { get; init; }

    /// <summary>
    /// Whether the language has the <c>Option Explicit</c> directive (<strong>MS-VBAL §5.2.1.3</strong>), with which a module requires that every
    /// name it uses is declared.
    /// </summary>
    /// <remarks>
    /// VBA and VB6 do. A BASIC does not: a variable is whatever a line first assigns, which is what makes it a language to type in a line at a time, and a
    /// module of it cannot state a directive that it has no way to write. Whether a module states it is not something to have an opinion about, then.
    /// </remarks>
    public bool HasOptionExplicit { get; init; } = true;

    /// <summary>
    /// Whether the language is written in the syntax of classic BASIC: a <c>Rem</c> comment and the <c>Error</c> statement are how it writes a comment and raises an
    /// error, and not relics of the language it descends from.
    /// </summary>
    /// <remarks>
    /// A BASIC does, since that is what it is. VBA and VB6 keep both for backward compatibility only: <c>'</c> is the comment, and <c>Err.Raise</c> raises an error
    /// (<strong>MS-VBAL §5.4.4.3</strong>).
    /// </remarks>
    public bool UsesClassicBasicSyntax { get; init; }

    /// <summary>
    /// Whether a file of the language may have statements outside its procedures (file-level statements), which run in the order they are written: the
    /// script's own code, which is the program's entry point (<see cref="ProgramEntryPoint.ScriptName"/>).
    /// </summary>
    /// <remarks>
    /// A script has them: VBScript runs a file from its first line to its last, and calls its procedures only where the file does.
    /// VBA and VB6 do not: a statement outside a procedure is a syntax error (<strong>MS-VBAL §5.1</strong>), unless the workspace allows it
    /// (<see cref="Server.Configuration.SdkWorkspaceOptions.FileLevelStatements"/>).
    /// </remarks>
    public bool HasFileLevelStatements { get; init; }

    /// <summary>
    /// The libraries every project of the language references without its project file listing them, by name, in their order of reference precedence: after
    /// the standard library, and before the references the project file lists (<strong>RD-VBAL §2.3.1.3</strong>).
    /// </summary>
    /// <remarks>
    /// The standard library (<c>VBA</c>) is every language's, and is none of them. A VB6 project also has the Visual Basic runtime objects and procedures
    /// (<c>VBRUN</c>, the third type library of <c>MSVBVM60.DLL</c>): the constants of its forms and controls (<c>vbFixedSingle</c>, <c>vbTwips</c>,
    /// <c>vbHourglass</c>, <c>vbChecked</c>) and its <c>PropertyBag</c>; and the Visual Basic objects and procedures (<c>VB</c>, <c>VB6.OLB</c>): its forms,
    /// controls and <c>App</c>, <c>Screen</c> and <c>Printer</c>. A <c>.vbp</c> lists neither, and the VB6 IDE does not let a project remove them. A name
    /// both the standard library and <c>VBRUN</c> declare is the standard library's: <c>vbCancel</c> is <c>2</c>, where <c>VBRUN</c>'s is <c>0</c>.
    /// </remarks>
    public IReadOnlyList<string> ImplicitReferences { get; init; } = [];

    /// <summary>
    /// The libraries a project of the language with these modules references without its project file listing them, by name, in their order of reference
    /// precedence: the language's (<see cref="ImplicitReferences"/>), then RDForms (<see cref="ProjectModuleKinds.RDFormsLibraryName"/>) when a module is
    /// the code of an RDForms form (<see cref="ProjectModuleKinds.IsRDFormsModule"/>).
    /// </summary>
    /// <remarks>
    /// A project sees the forms and controls of its forms the way a VB6 project sees VB's: a project that has an RDForms form references RDForms, and its
    /// project file does not list it. RDForms comes after the language's own, so in a VB6 project a name both <c>VB</c> and RDForms declare is
    /// <c>VB</c>'s.
    /// </remarks>
    /// <param name="modulePaths">The paths or relative URIs of the project's modules (<see cref="RDCoreFile.RelativeUri"/>).</param>
    /// <returns>The names; <see cref="ImplicitReferences"/> when no module is the code of an RDForms form, or when the language already has RDForms.</returns>
    public IReadOnlyList<string> ImplicitReferencesOf(IEnumerable<string?> modulePaths)
        => modulePaths.Any(ProjectModuleKinds.IsRDFormsModule)
           && !ImplicitReferences.Contains(ProjectModuleKinds.RDFormsLibraryName, StringComparer.OrdinalIgnoreCase)
            ? [.. ImplicitReferences, ProjectModuleKinds.RDFormsLibraryName]
            : ImplicitReferences;

    /// <summary>
    /// Whether a file is written in the language: whether its extension is the extension of one of its <see cref="FileTypes"/>.
    /// </summary>
    /// <remarks>
    /// A module that a block of a file declares (<see cref="RDCoreDocument.BlockUri"/>) is written in the language of its file.
    /// </remarks>
    /// <param name="path">The file's path, or its name; or the path of a block's module.</param>
    /// <returns><see langword="true"/> when the extension is one of the language's, compared without regard to case.</returns>
    public bool IsSourceFile(string path) => HasExtensionOfOne(FileTypes, path);

    /// <summary>
    /// Whether a file is a module of a workspace of the language: whether its extension is the extension of one of its <see cref="ModuleFileTypes"/>.
    /// </summary>
    /// <remarks>
    /// A module that a block of a file declares (<see cref="RDCoreDocument.BlockUri"/>) is a module of the workspaces its file is a module of.
    /// </remarks>
    /// <param name="path">The file's path, or its name; or the path of a block's module.</param>
    /// <returns><see langword="true"/> when the extension is one of the language's module files, compared without regard to case.</returns>
    public bool IsModuleFile(string path) => HasExtensionOfOne(ModuleFileTypes, path);

    private static bool HasExtensionOfOne(string[] fileTypes, string path)
    {
        var extension = Path.GetExtension(RDCoreDocument.FileOf(path));
        return extension.Length > 0 && fileTypes.Any(fileType => string.Equals(fileType.TrimStart('*'), extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The patterns of the files written in the language, as a document filter pattern.</summary>
    public string FilterString => string.Join(";", FileTypes.Select(fileType => $"**/{fileType}").ToArray());

    /// <summary>The selector of the documents written in the language.</summary>
    public TextDocumentSelector ToTextDocumentSelector() => new(
        new TextDocumentFilter
        {
            Language = Id,
            Pattern = FilterString,
        });

    /// <inheritdoc/>
    public override string ToString() => Id;
}

/// <summary>
/// The languages the platform serves.
/// </summary>
public static class SupportedLanguages
{
    /// <summary>
    /// RD-VBA: the platform's own implementation of <strong>MS-VBAL</strong>, and the default.
    /// </summary>
    /// <remarks>
    /// Its modules are standard modules (<c>.bas</c>), class modules (<c>.cls</c>), UserForms (<c>.frm</c>), document modules (<c>.doccls</c>), and the code
    /// of RDForms forms (<c>.rdform</c>, <see cref="ProjectModuleKinds.RDFormsModuleExtension"/>), whose RDForms document (<c>.xaml</c>) is not a module.
    /// </remarks>
    public static SupportedLanguage RDVBA { get; } = new(
        "vba", "Microsoft Visual Basic for Applications", "*.bas", "*.cls", "*.frm", "*.doccls", "*.rdform");

    /// <summary>
    /// VB6.
    /// </summary>
    /// <remarks>
    /// Its modules are standard modules (<c>.bas</c>), class modules (<c>.cls</c>), and designed modules: forms (<c>.frm</c>), UserControls (<c>.ctl</c>),
    /// PropertyPages (<c>.pag</c>) and UserDocuments (<c>.dob</c>). The code of a UserControl, a PropertyPage or a UserDocument is built on the class its
    /// designer block names (<see cref="Forms.FormDescriptor.BaseClass"/>). A Designer (<c>.dsr</c>) is not a module: its designer's objects are in its
    /// binary <c>.dsx</c>. A form ported to RDForms is the code of an RDForms form (<c>.rdform</c>, <see cref="ProjectModuleKinds.RDFormsModuleExtension"/>),
    /// whose RDForms document (<c>.xaml</c>) is not a module.
    /// </remarks>
    public static SupportedLanguage VB6 { get; } = new("vb6", "Microsoft Visual Basic 6.0", "*.bas", "*.cls", "*.frm", "*.ctl", "*.pag", "*.dob", "*.rdform")
    {
        ImplicitReferences = ["VBRUN", "VB"],
    };

    /// <summary>
    /// The platform's BASIC, which an interactive shell is written in.
    /// </summary>
    /// <remarks>
    /// Its source is a <c>.rdc</c> file: a program of the shell, a text file of lines. It is not a <c>.bas</c>, which is a module of the BASIC of
    /// Visual Basic (a file with procedures in it, and a header), which this is not. The modules of a workspace the shell opens are RD-VBA's
    /// (<see cref="SupportedLanguage.ModuleFileTypes"/>): standard modules (<c>.bas</c>), class modules (<c>.cls</c>), UserForms (<c>.frm</c>) and
    /// document modules (<c>.doccls</c>). The code of an RDForms form (<c>.rdform</c>) is not one of them.
    /// </remarks>
    public static SupportedLanguage BASIC { get; } = new("basic", "RDCore BASIC", "*.rdc")
    {
        ModuleFileTypes = ["*.bas", "*.cls", "*.frm", "*.doccls"],
        ImplicitDeclarationScope = ImplicitDeclarationScope.Module,
        HasBarePrint = true,
        HasOptionExplicit = false,
        UsesClassicBasicSyntax = true,
    };

    /// <summary>
    /// VBScript: Microsoft Visual Basic Scripting Edition, as Windows Script Host runs it.
    /// </summary>
    /// <remarks>
    /// Its source is a script (<c>.vbs</c>): a text file with no header. A project of the language references the two libraries of Windows Script Host
    /// without its project file listing them: <c>IHost</c>, the type library of <c>wscript.exe</c>, whose <c>WScript</c> object every script names, and
    /// <c>IWshRuntimeLibrary</c> (Windows Script Host Object Model, <c>wshom.ocx</c>), whose <c>WshShell</c> a script creates with
    /// <c>CreateObject("WScript.Shell")</c>. Its statements outside procedures are the script, which a run runs from its first line to its last
    /// (<see cref="SupportedLanguage.HasFileLevelStatements"/>).
    /// </remarks>
    public static SupportedLanguage VBScript { get; } = new("vbscript", "Microsoft Visual Basic Scripting Edition", "*.vbs")
    {
        ImplicitReferences = ["IHost", "IWshRuntimeLibrary"],
        HasFileLevelStatements = true,
    };

    /// <summary>Every language the platform serves.</summary>
    public static IReadOnlyList<SupportedLanguage> All { get; } = [RDVBA, VB6, BASIC, VBScript];

    /// <summary>
    /// The selector of the documents of every language the platform serves, by their files: a client names a document's language its own way, so what selects
    /// a document is the file it is.
    /// </summary>
    public static TextDocumentSelector DocumentSelector { get; } = new(All
        .SelectMany(language => language.FileTypes).Distinct()
        .Select(fileType => new TextDocumentFilter { Pattern = $"**/{fileType}" }));

    /// <summary>
    /// The language a project says it is written in: <see cref="VB6"/> for a project imported from a Visual Basic 6 project
    /// (<see cref="RDCoreProject.SourceProject"/>, a <c>.vbp</c>), and <see cref="VBScript"/> for a project whose modules are all scripts (<c>.vbs</c>).
    /// </summary>
    /// <remarks>
    /// A server takes it when its client names no language in the <c>initializationOptions</c> of its <c>initialize</c> request
    /// (<see cref="Platform.Protocol.RDCoreInitializationOptions.Language"/>): what the client names wins. Any other project says no language, and the
    /// server's own setting stands.
    /// </remarks>
    /// <param name="project">The project.</param>
    /// <returns>The language, or <see langword="null"/> when the project says none.</returns>
    public static SupportedLanguage? OfProject(RDCoreProject project)
        => project.SourceProject is { } source && string.Equals(Path.GetExtension(source), ".vbp", StringComparison.OrdinalIgnoreCase) ? VB6
            : project.Modules is { Length: > 0 } modules && modules.All(module => module.RelativeUri is { } path && VBScript.IsSourceFile(path)) ? VBScript
            : null;

    /// <summary>
    /// The language with the given identifier.
    /// </summary>
    /// <param name="id">The identifier, as <see cref="SupportedLanguage.Id"/>; compared without regard to case.</param>
    /// <param name="language">The language, when there is one by that identifier.</param>
    /// <returns><see langword="false"/> when the platform serves no such language.</returns>
    public static bool TryGet(string? id, out SupportedLanguage language)
    {
        language = All.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))!;
        return language is not null;
    }

    /// <summary>
    /// The language with the given identifier.
    /// </summary>
    /// <param name="id">The identifier, as <see cref="SupportedLanguage.Id"/>.</param>
    /// <exception cref="InvalidOperationException">The platform serves no such language.</exception>
    public static SupportedLanguage Get(string? id)
        => TryGet(id, out var language)
            ? language
            : throw new InvalidOperationException(
                $"'{id}' is not a language the platform serves. They are: {string.Join(", ", All.Select(candidate => candidate.Id))}.");
}
