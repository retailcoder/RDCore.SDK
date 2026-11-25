using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Errors;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// A file and its modules: a block's module is addressed under the file, its file is found back from its address, and a position of the file is in the
/// block whose range holds it, or in the file's own module. What is decided by a file is decided by the block's file.
/// </summary>
[TestClass]
public sealed class RDCoreDocumentTests
{
    private static readonly Uri File = new("file:///c:/ws/Backup.vbs");

    [TestMethod]
    public void ABlocksModule_IsAddressedUnderItsFile_AndItsFileIsFoundBack()
    {
        var block = RDCoreDocument.BlockUri(File, "Logger");

        Assert.AreEqual(new Uri("file:///c:/ws/Backup.vbs/Logger"), block);
        Assert.AreEqual(File, RDCoreDocument.FileOf(block));
        Assert.AreEqual(File, RDCoreDocument.FileOf(File));
    }

    [TestMethod]
    [DataRow("c:/ws/Backup.vbs/Logger", "c:/ws/Backup.vbs", DisplayName = "a block of a script")]
    [DataRow("c:\\ws\\Class1.cls\\Logger", "c:\\ws\\Class1.cls", DisplayName = "a block of a class module, Windows separators")]
    [DataRow("c:/ws/Module1.bas", "c:/ws/Module1.bas", DisplayName = "a file")]
    [DataRow("c:/ws/Notes/README", "c:/ws/Notes/README", DisplayName = "a file without an extension in a folder")]
    [DataRow("c:/ws/v1.2/Main", "c:/ws/v1.2/Main", DisplayName = "a file in a folder whose extension is no language's")]
    public void TheFileOfAPath_IsItsFolderOnlyWhenThatIsASourceFile(string path, string expected)
        => Assert.AreEqual(expected, RDCoreDocument.FileOf(path));

    [TestMethod]
    public void ABlocksModule_IsWrittenInTheLanguageOfItsFile()
    {
        var block = RDCoreDocument.BlockUri(File, "Logger").AbsolutePath;

        Assert.IsTrue(SupportedLanguages.VBScript.IsSourceFile(block));
        Assert.IsTrue(SupportedLanguages.VBScript.IsModuleFile(block));
        Assert.IsFalse(SupportedLanguages.RDVBA.IsSourceFile(block));
    }

    [TestMethod]
    public void APositionOfTheFile_IsInTheBlockThatHoldsIt_OrInTheFilesOwnModule()
    {
        var own = Module(File, File, 0, 9);
        var logger = RDCoreDocument.BlockUri(File, "Logger");
        var block = new BlockModule("Logger", ModuleType.ClassModule, logger, new SourceRange(2, 0, 5, 9), Module(logger, File, 3, 4));
        var document = new RDCoreDocument(File, ModuleParseResult.Success(own) with { BlockModules = [block] });

        Assert.AreEqual(File, document.ModuleAt(new SourcePosition(1, 0)));
        Assert.AreEqual(logger, document.ModuleAt(new SourcePosition(2, 0)));
        Assert.AreEqual(logger, document.ModuleAt(new SourcePosition(5, 9)));
        Assert.AreEqual(File, document.ModuleAt(new SourcePosition(6, 0)));
        Assert.AreSame(own, document.SyntaxTreeOf(File));
        Assert.AreSame(block.SyntaxTree, document.SyntaxTreeOf(logger));
        CollectionAssert.AreEqual(new[] { File, logger }, document.ModuleUris.ToArray());
    }

    [TestMethod]
    public void EachModulesParse_HasTheFilesItemsThatStartOnItsLines_AndNoBoundaryOfABlock()
    {
        // 0 own comment, and a #If the file's own text leaves open, to the end; 1 the header, and its comment; 2 a comment of the block; 3 a directive and an
        // error of the block; 4 the end; 5 an error of the file's own module.
        var logger = RDCoreDocument.BlockUri(File, "Logger");
        var block = new BlockModule("Logger", ModuleType.ClassModule, logger, new SourceRange(1, 0, 4, 9), Module(logger, File, 2, 3));
        var ownComment = Comment(File, 0, 0);
        var headerComment = Comment(File, 1, 14);
        var blockComment = Comment(logger, 2, 4);
        var openIf = Directive(File, new SourceRange(0, 20, 9, 0));
        var blockDirective = Directive(logger, new SourceRange(3, 0, 3, 12));
        var blockError = Error(3, 4);
        var ownError = Error(5, 0);
        var parse = ModuleParseResult.Success(Module(File, File, 0, 9)) with
        {
            Trivia = [ownComment, Boundary(logger, 1, false), headerComment, blockComment, Boundary(logger, 4, true)],
            PrecompilerTrivia = [openIf, blockDirective],
            SyntaxErrors = [blockError, ownError],
            LineEnding = "\n",
            BlockModules = [block],
        };
        var document = new RDCoreDocument(File, parse);

        var own = document.ParseOf(File)!;
        Assert.AreSame(parse.SyntaxTree, own.SyntaxTree);
        CollectionAssert.AreEqual(new SyntaxNode[] { ownComment, headerComment }, own.Trivia.ToArray(), "a comment of a header line is the file's own");
        CollectionAssert.AreEqual(new SyntaxNode[] { openIf }, own.PrecompilerTrivia.ToArray(), "the module it starts in, however far it runs");
        CollectionAssert.AreEqual(new[] { ownError }, own.SyntaxErrors.ToArray());
        Assert.IsEmpty(own.BlockModules);

        var ofTheBlock = document.ParseOf(logger)!;
        Assert.AreSame(block.SyntaxTree, ofTheBlock.SyntaxTree);
        CollectionAssert.AreEqual(new SyntaxNode[] { blockComment }, ofTheBlock.Trivia.ToArray());
        CollectionAssert.AreEqual(new SyntaxNode[] { blockDirective }, ofTheBlock.PrecompilerTrivia.ToArray());
        CollectionAssert.AreEqual(new[] { blockError }, ofTheBlock.SyntaxErrors.ToArray());
        Assert.AreEqual(("\n", 0), (ofTheBlock.LineEnding, ofTheBlock.BlockModules.Length));
    }

    [TestMethod]
    public void AnAddressThatIsNoModuleOfTheFile_HasNoParse_AndAFileWithoutBlocksIsItsOwnModulesParse()
    {
        var logger = RDCoreDocument.BlockUri(File, "Logger");
        var withoutBlocks = ModuleParseResult.Success(Module(File, File, 0, 3));
        var withABlock = withoutBlocks with
        {
            BlockModules = [new BlockModule("Logger", ModuleType.StdModule, logger, new SourceRange(0, 0, 2, 10), Module(logger, File, 1, 1))],
        };

        Assert.AreSame(withoutBlocks, new RDCoreDocument(File, withoutBlocks).ParseOf(File));
        Assert.IsNull(new RDCoreDocument(File, withoutBlocks).ParseOf(logger));
        Assert.IsNull(new RDCoreDocument(File, withABlock).ParseOf(RDCoreDocument.BlockUri(File, "Other")));
        Assert.IsNull(new RDCoreDocument(File, withABlock).ParseOf(new Uri("file:///c:/ws/Other.vbs")));
    }

    private static CommentTriviaNode Comment(Uri module, int line, int character)
        => new(new SyntaxNodeId(module.AbsolutePath, [line, character]),
            new SourceLocation(File, new SourceRange(line, character, line, character + 5)), "' x");

    private static PrecompilerTriviaNode Directive(Uri module, SourceRange range)
        => new(new SyntaxNodeId($"{module}/conditional", [range.Start.Line]), new SourceLocation(File, range), [], "#Const");

    private static BlockBoundaryTriviaNode Boundary(Uri module, int line, bool isEnd)
        => new(new SyntaxNodeId(module.AbsolutePath, [isEnd ? -2 : -1]), new SourceLocation(File, new SourceRange(line, 0, line, 9)), FileBlockKind.Class,
            isEnd, isEnd ? "End Class" : "Class Logger");

    private static VBSyntaxErrorInfo Error(int line, int character)
        => new(1, new SourceLocation(File, new SourceRange(line, character, line, character + 1)), "Syntax error", "Syntax error");

    // a module's tree: its identity is keyed by the module's address, its location is in the file.
    private static ModuleNode Module(Uri module, Uri file, int startLine, int endLine)
        => new(new SyntaxNodeId(module.AbsolutePath, [0]), new SourceLocation(file, new SourceRange(startLine, 0, endLine, 0)), []);
}
