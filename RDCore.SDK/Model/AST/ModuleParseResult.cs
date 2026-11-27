using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Errors;
using RDCore.SDK.Workspace.Forms;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST;

public record class ModuleParseResult
{
    public static ModuleParseResult Success(ModuleNode node) => new() { SyntaxTree = node };

    public ModuleNode? SyntaxTree { get; init; }
    public ImmutableArray<SyntaxNode> PrecompilerTrivia { get; init; } = [];

    /// <summary>
    /// The source of the module that is not code, in the order it is written: the <c>.cls</c> file header (<see cref="ModuleHeaderTriviaNode"/>),
    /// comments (<see cref="CommentTriviaNode"/>) and the annotations in them (<see cref="AnnotationTriviaNode"/>, inside the range of the comment that holds them),
    /// the unary <c>+</c> signs, which the VBE erases (<see cref="ErasedPlusTriviaNode"/>), and the line continuations (<see cref="LineContinuationTriviaNode"/>).
    /// </summary>
    /// <remarks>
    /// Together with <see cref="SyntaxTree"/> and <see cref="PrecompilerTrivia"/> it accounts for every token of the source, which is what a formatter
    /// or an exporter needs to write the module back out. Trivia is not among the children of the syntax nodes, since it can be written anywhere.
    /// <para>
    /// A file that has blocks (<see cref="BlockModules"/>) has the trivia of all its modules here, and the header and the end line of each block
    /// (<see cref="BlockBoundaryTriviaNode"/>), which no module's tree holds.
    /// </para>
    /// </remarks>
    public ImmutableArray<SyntaxNode> Trivia { get; init; } = [];
    public ImmutableArray<VBSyntaxErrorInfo> SyntaxErrors { get; init; } = [];

    /// <summary>
    /// The characters the lines of the module's text end with: <c>"\r\n"</c>, <c>"\n"</c> or <c>"\r"</c>, as the first line of the text ends; <c>"\r\n"</c>
    /// for a text of one line.
    /// </summary>
    /// <remarks>
    /// An edit that writes lines of its own ends them with it, so that they end the way the lines around them do (<see cref="Extensibility.Diagnostics.LineEdits"/>).
    /// A text whose lines do not all end the same way has the ending of its first line. The parser sets it, from the text it reads.
    /// </remarks>
    public string LineEnding { get; init; } = "\r\n";

    /// <summary>
    /// What the module's designer declares, when the module is a form module: the form's class and its controls, read from the module's designer block and
    /// its companion <c>.frx</c> file (<see cref="FormDesigner.Read"/>); <c>null</c> for any other module, and for a form whose designer data cannot be read.
    /// </summary>
    /// <remarks>
    /// The parser, which reads text alone, never sets it: the language server, which has the companion file, does, on the parse it caches. It travels with the
    /// parse to the host, whose checks of the module's declarations need to know the names its controls take.
    /// </remarks>
    public FormDescriptor? Designer { get; init; }

    /// <summary>
    /// The modules that blocks of the file declare (<c>Class Name ... End Class</c>, <c>Module Name ... End Module</c>), in the order they are written; empty
    /// for a file that has none, which is every file of one module.
    /// </summary>
    /// <remarks>
    /// <see cref="SyntaxTree"/> is the file's own module: what is outside every block. <see cref="Trivia"/>, <see cref="PrecompilerTrivia"/>,
    /// <see cref="SyntaxErrors"/> and <see cref="LineEnding"/> are the whole file's, the blocks' included. Positions in a block's tree are positions in the
    /// file. <see cref="Workspace.RDCoreDocument"/> maps the file to its modules and back.
    /// </remarks>
    public ImmutableArray<BlockModule> BlockModules { get; init; } = [];

    /// <summary>
    /// Where the block that declares the module is in its file (<see cref="BlockModule.Range"/>), from the start of its header to the end of its
    /// <c>End Class</c> or <c>End Module</c>, when this is the parse of a block's module (<see cref="Workspace.RDCoreDocument.ParseOf"/>);
    /// <see langword="null"/> for the parse of a file, and for a file's own module.
    /// </summary>
    /// <remarks>
    /// The lines of a block's module are those between its header and its end: what an edit adds to the module is written on them, before the end line
    /// (<see cref="Semantics.Refactoring.DeclarationEdits.SectionEnd"/>).
    /// </remarks>
    public Source.SourceRange? BlockRange { get; init; }

    public bool IsSuccess => SyntaxTree is not null && SyntaxErrors.Length == 0;
}
