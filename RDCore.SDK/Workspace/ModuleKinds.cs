using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Symbols;

namespace RDCore.SDK.Workspace;

/// <summary>
/// What kind of module a module of a project is: the one decision every side of the platform makes of it the same way - the environment host composing its
/// session from the project file, and the language server composing the workspace it analyzes.
/// </summary>
/// <remarks>
/// A document module is the code of a host's document (<strong>MS-VBAL §4.2.1</strong>): Excel's worksheets and workbook, Word's documents, Access's forms and
/// reports. Its export (a <c>.doccls</c>, or a <c>.cls</c> from the VBE) says nothing of what it is, so the project says it: a module whose entry has a
/// <see cref="RDCoreModule.Super"/> is one, whatever its text and its file name, and nothing else is: no name and no event handler makes a module one.
/// The code of an RDForms form (<see cref="ProjectModuleKinds.RDFormsModuleExtension"/>) is a class module. Any other module is a class module when its
/// text has a <c>VERSION</c> header (<see cref="ModuleHeader.IsClassModule"/>), and a standard module otherwise.
/// <para>
/// RD-VBA hosts no document: a document module is analyzed - its code binds, the members of its host's class among its names - and never run. Code that is to
/// handle a document's events holds the document in a <c>WithEvents</c> variable of the host's class, from an application the program automates.
/// </para>
/// </remarks>
public static class ModuleKinds
{
    /// <summary>
    /// The class of a library whose object a document of <paramref name="super"/> is, whose members the code of its module names
    /// (<c>Me.Range</c> in a worksheet's); <see langword="null"/> for a document of a host RD-VBA does not know (<see cref="DocClassType.Unknown"/>).
    /// </summary>
    /// <param name="super">What the project says the module's document is.</param>
    public static DocumentHostClass? HostClassOf(DocClassType super) => super switch
    {
        DocClassType.ExcelWorkbook => new("Excel", "Workbook"),
        DocClassType.ExcelWorksheet => new("Excel", "Worksheet"),
        DocClassType.WordDocument => new("Word", "Document"),
        DocClassType.AccessForm => new("Access", "Form"),
        DocClassType.AccessReport => new("Access", "Report"),
        _ => null,
    };

    /// <summary>
    /// The kind of a module of a project.
    /// </summary>
    /// <param name="module">The module's entry in the project, or <see langword="null"/> when the project does not list it (a shell's program).</param>
    /// <param name="source">The module's text, or <see langword="null"/> when it could not be read.</param>
    public static ModuleType Of(RDCoreModule? module, string? source) => Of(module?.Super, source, module?.RelativeUri);

    /// <summary>
    /// The kind of a module that the project says is the document of <paramref name="super"/>, or of no host when it is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// This overload has no file to go by, so the code of an RDForms form is a standard module here: <see cref="Of(DocClassType?, string?, string?)"/> takes
    /// its path.
    /// </remarks>
    /// <param name="super">What the project says the module's document is (<see cref="RDCoreModule.Super"/>).</param>
    /// <param name="source">The module's text, or <see langword="null"/> when it could not be read.</param>
    public static ModuleType Of(DocClassType? super, string? source) => Of(super, source, path: null);

    /// <summary>
    /// The kind of a module that the project says is the document of <paramref name="super"/>, or of no host when it is <see langword="null"/>, in the file at
    /// <paramref name="path"/>: the code of an RDForms form (<see cref="ProjectModuleKinds.RDFormsModuleExtension"/>) is a class module.
    /// </summary>
    /// <param name="super">What the project says the module's document is (<see cref="RDCoreModule.Super"/>).</param>
    /// <param name="source">The module's text, or <see langword="null"/> when it could not be read.</param>
    /// <param name="path">The module's file path or relative URI, or <see langword="null"/> for a module that has no file.</param>
    public static ModuleType Of(DocClassType? super, string? source, string? path) => ProjectModuleKinds.Of(super, source, path) switch
    {
        ProjectModuleKind.Document => ModuleType.DocumentModule,
        ProjectModuleKind.Class => ModuleType.ClassModule,
        _ => ModuleType.StdModule,
    };

    /// <summary>
    /// The kind of the module a block of a file declares (<see cref="FileBlocks"/>): a standard module for <c>Module Name ... End Module</c>, a class module
    /// for <c>Class Name ... End Class</c>.
    /// </summary>
    /// <param name="kind">The kind of the block.</param>
    /// <returns>
    /// <see cref="ModuleType.StdModule"/> for <see cref="FileBlockKind.Module"/>, <see cref="ModuleType.ClassModule"/> for <see cref="FileBlockKind.Class"/>.
    /// </returns>
    public static ModuleType Of(FileBlockKind kind) => kind == FileBlockKind.Class ? ModuleType.ClassModule : ModuleType.StdModule;

    /// <summary>
    /// Every module a file of a project declares, read from its text without parsing it, in the order they are written: the file's own module, then the
    /// module of each of its blocks (<see cref="FileBlocks"/>).
    /// </summary>
    /// <remarks>
    /// The file's own module is named as <see cref="ModuleName.Resolve"/> names it, and its kind is <see cref="Of(RDCoreModule?, string?)"/>'s. A block's
    /// module is named as its header writes it, and its kind is its block's (<see cref="Of(FileBlockKind)"/>). A file of one module declares that one.
    /// </remarks>
    /// <param name="module">The file's entry in the project.</param>
    /// <param name="source">The file's text, or <see langword="null"/> when it could not be read.</param>
    /// <returns>
    /// The modules, the file's own first; the file's own module alone, named for its file, when <paramref name="source"/> is <see langword="null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="module"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<DeclaredModule> ModulesOf(RDCoreModule module, string? source)
    {
        ArgumentNullException.ThrowIfNull(module);
        return
        [
            new DeclaredModule(ModuleName.Resolve(source, module.RelativeUri), Of(module, source), IsBlock: false),
            .. FileBlocks.Find(source).Select(block => new DeclaredModule(block.Name, Of(block.Kind), IsBlock: true)),
        ];
    }

    /// <summary>
    /// Whether a module of this kind is a class module: its members are of each object of it, and <c>Me</c> is one of them. A document module is.
    /// </summary>
    /// <param name="type">The module's kind.</param>
    public static bool IsClass(this ModuleType type) => type is ModuleType.ClassModule or ModuleType.DocumentModule;

    /// <summary>
    /// The directives a module of the specified kind has, from those its text declares: a document module is not creatable and is extensible, whatever its
    /// text says - its export carries none of the attributes that would say so.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>VB_Creatable = False</c>: only the host makes its documents, and <c>New Sheet1</c> is a compile error.</item>
    /// <item><c>VB_Extensible = True</c>: the module extends the host's class (<strong>MS-VBAL §4.2.1</strong>), whose members its code names.</item>
    /// </list>
    /// RD-VBA hosts no document, so nothing makes the document an instance of the module: it has no default instance, whatever its text says
    /// (<c>VB_PredeclaredId</c> is <see langword="false"/>), and none of its procedures can run.
    /// </remarks>
    /// <param name="type">The module's kind (<see cref="Of(RDCoreModule?, string?)"/>).</param>
    /// <param name="declared">The directives the module's text declares.</param>
    public static ModuleDirectives DirectivesOf(ModuleType type, ModuleDirectives declared)
        => type == ModuleType.DocumentModule ? declared with { PredeclaredId = false, Creatable = false, Extensible = true } : declared;
}

/// <summary>
/// The class of a referenced library that a host's document is an object of (<see cref="ModuleKinds.HostClassOf"/>).
/// </summary>
/// <param name="Library">The name of the library, as a project references it: <c>Excel</c>.</param>
/// <param name="ClassName">The name of the class in it: <c>Worksheet</c>.</param>
public sealed record class DocumentHostClass(string Library, string ClassName);

/// <summary>
/// A module that a file of a project declares (<see cref="ModuleKinds.ModulesOf"/>): the file's own module, or the module of one of its blocks.
/// </summary>
/// <param name="Name">The module's name: its <c>Attribute VB_Name</c> or its file's name for the file's own module, its header's for a block's.</param>
/// <param name="Kind">The kind of module.</param>
/// <param name="IsBlock">
/// Whether the module is a block's, addressed under its file (<see cref="RDCoreDocument.BlockUri"/>); the file's own module otherwise, addressed by the file.
/// </param>
public sealed record class DeclaredModule(string Name, ModuleType Kind, bool IsBlock);
