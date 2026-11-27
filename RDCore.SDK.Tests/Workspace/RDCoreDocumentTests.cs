using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.AST.Statements;
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
        // 0 own comment, and a #If the file's own text leaves open, to the end; 1 blank; 2 the header, and its comment; 3 a comment of the block; 4 a
        // directive and an error of the block; 5 the end; 6 an error of the file's own module.
        var logger = RDCoreDocument.BlockUri(File, "Logger");
        var block = new BlockModule("Logger", ModuleType.ClassModule, logger, new SourceRange(2, 0, 5, 9), Module(logger, File, 3, 4));
        var ownComment = Comment(File, 0, 0);
        var headerComment = Comment(File, 2, 14);
        var blockComment = Comment(logger, 3, 4);
        var openIf = Directive(File, new SourceRange(0, 20, 9, 0));
        var blockDirective = Directive(logger, new SourceRange(4, 0, 4, 12));
        var blockError = Error(4, 4);
        var ownError = Error(6, 0);
        var parse = ModuleParseResult.Success(Module(File, File, 0, 9)) with
        {
            Trivia = [ownComment, Boundary(logger, 2, false), headerComment, blockComment, Boundary(logger, 5, true)],
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
    public void TheCommentLinesDirectlyAboveABlocksHeader_AreThatBlocksModules_AndEveryOtherCommentOutsideTheBlocksIsTheFilesOwn()
    {
        // 0 own comment; 1 blank; 2-3 comments above Logger's header; 4 Logger's header; 5 a comment of Logger; 6 Logger's end, and its comment;
        // 7 a comment above Other's header; 8 Other's header; 9 Other's end; 10 own code, and its comment; 11 Third's header; 12 Third's end.
        var logger = RDCoreDocument.BlockUri(File, "Logger");
        var other = RDCoreDocument.BlockUri(File, "Other");
        var third = RDCoreDocument.BlockUri(File, "Third");
        var ownComment = Comment(File, 0, 0);
        var aboveLogger = new[] { Comment(File, 2, 0), Comment(File, 3, 4) };
        var loggerComment = Comment(logger, 5, 4);
        var endComment = Comment(File, 6, 12);
        var aboveOther = Comment(File, 7, 0);
        var codeComment = Comment(File, 10, 12);
        var ownCode = new VariableDeclarationNode(
            new SyntaxNodeId(File.AbsolutePath, [10]), new SourceLocation(File, new SourceRange(10, 0, 10, 10)), "x", []);
        var parse = ModuleParseResult.Success(new ModuleNode(new SyntaxNodeId(File.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(0, 0, 12, 0)),
            [ownCode])) with
        {
            Trivia =
            [
                ownComment, .. aboveLogger, Boundary(logger, 4, false), loggerComment, Boundary(logger, 6, true), endComment, aboveOther,
                Boundary(other, 8, false), Boundary(other, 9, true), codeComment, Boundary(third, 11, false), Boundary(third, 12, true),
            ],
            BlockModules =
            [
                new BlockModule("Logger", ModuleType.ClassModule, logger, new SourceRange(4, 0, 6, 9), Module(logger, File, 5, 5)),
                new BlockModule("Other", ModuleType.StdModule, other, new SourceRange(8, 0, 9, 9), Module(other, File, 9, 9)),
                new BlockModule("Third", ModuleType.StdModule, third, new SourceRange(11, 0, 12, 9), Module(third, File, 12, 12)),
            ],
        };
        var document = new RDCoreDocument(File, parse);

        CollectionAssert.AreEqual(new SyntaxNode[] { ownComment, endComment, codeComment }, document.ParseOf(File)!.Trivia.ToArray(),
            "a blank line, an end line and code each end the comment lines above a header");
        CollectionAssert.AreEqual(new SyntaxNode[] { aboveLogger[0], aboveLogger[1], loggerComment }, document.ParseOf(logger)!.Trivia.ToArray());
        CollectionAssert.AreEqual(new SyntaxNode[] { aboveOther }, document.ParseOf(other)!.Trivia.ToArray());
        Assert.IsEmpty(document.ParseOf(third)!.Trivia);
    }

    [TestMethod]
    public void AScriptWithNoCode_WritesNoLine_SoACommentOnTheFirstLineAboveAHeaderIsTheBlocks()
    {
        // 0 a comment above Shape's header; 1 Shape's header; 2 Shape's end. The script has no code: its procedure is of no width, where the file starts.
        var shape = RDCoreDocument.BlockUri(File, "Shape");
        var aboveShape = Comment(File, 0, 0);
        var script = new MemberDeclarationNode(new SyntaxNodeId(File.AbsolutePath, [1]), new SourceLocation(File, new SourceRange(0, 0, 0, 0)), [],
            ProgramEntryPoint.ScriptName, MemberKind.Procedure, AccessModifier.Public);
        var document = new RDCoreDocument(File, Script([script], [aboveShape, Boundary(shape, 1, false), Boundary(shape, 2, true)],
            new BlockModule("Shape", ModuleType.ClassModule, shape, new SourceRange(1, 0, 2, 9), Module(shape, File, 2, 2))));

        CollectionAssert.AreEqual(new SyntaxNode[] { aboveShape }, document.ParseOf(shape)!.Trivia.ToArray());
        Assert.IsEmpty(document.ParseOf(File)!.Trivia);
    }

    [TestMethod]
    public void AScriptsCode_IsOnItsStatementsLines_SoACommentBetweenThemAboveAHeaderIsTheBlocks()
    {
        // 0 the script's code; 1 a comment above Shape's header; 2 Shape's header; 3 Shape's end; 4 the script's code. The script's procedure runs from
        // its first statement to its last, across the block.
        var shape = RDCoreDocument.BlockUri(File, "Shape");
        var aboveShape = Comment(File, 1, 0);
        DebugPrintStatementNode Statement(int line) => new(new SyntaxNodeId(File.AbsolutePath, [1, line]), new SourceLocation(File, new SourceRange(line, 0,
            line, 11)), []);
        var script = new MemberDeclarationNode(new SyntaxNodeId(File.AbsolutePath, [1]), new SourceLocation(File, new SourceRange(0, 0, 4, 11)),
            [Statement(0), Statement(4)], ProgramEntryPoint.ScriptName, MemberKind.Procedure, AccessModifier.Public);
        var document = new RDCoreDocument(File, Script([script], [aboveShape, Boundary(shape, 2, false), Boundary(shape, 3, true)],
            new BlockModule("Shape", ModuleType.ClassModule, shape, new SourceRange(2, 0, 3, 9), Module(shape, File, 3, 3))));

        CollectionAssert.AreEqual(new SyntaxNode[] { aboveShape }, document.ParseOf(shape)!.Trivia.ToArray());
        Assert.IsEmpty(document.ParseOf(File)!.Trivia);
    }

    // a script file's parse: the file's own tree, its trivia, and its blocks.
    private static ModuleParseResult Script(SyntaxNode[] children, SyntaxNode[] trivia, params BlockModule[] blocks)
        => ModuleParseResult.Success(new ModuleNode(new SyntaxNodeId(File.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(0, 0, 5, 0)),
            [.. children])) with { Trivia = [.. trivia], BlockModules = [.. blocks] };

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

    [TestMethod]
    public void EachModule_IsNamedAsRDVBANamesIt_TheFilesOwnByItsVBNameOrItsFile_AndABlocksByItsHeader()
    {
        var logger = RDCoreDocument.BlockUri(File, "Logger");
        var block = new BlockModule("Logger", ModuleType.ClassModule, logger, new SourceRange(2, 0, 5, 9), Module(logger, File, 3, 4));
        var vbName = new AttributeDirectiveNode(new SyntaxNodeId(File.AbsolutePath, [1]), new SourceLocation(File, new SourceRange(0, 0, 0, 28)),
            Tokens.VB_Name, "\"Housekeeping\"");
        var named = new ModuleNode(new SyntaxNodeId(File.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(0, 0, 9, 0)), [vbName]);
        var document = new RDCoreDocument(File, ModuleParseResult.Success(named) with { BlockModules = [block] });

        Assert.AreEqual("Housekeeping", document.NameOf(File));
        Assert.AreEqual("Logger", document.NameOf(logger));
        Assert.AreEqual("Backup", new RDCoreDocument(File, ModuleParseResult.Success(Module(File, File, 0, 9))).NameOf(File), "it says no VB_Name");
        Assert.AreEqual("Backup", new RDCoreDocument(File, new ModuleParseResult()).NameOf(File), "it could not be read");
        Assert.IsNull(document.NameOf(new Uri("file:///c:/ws/Other.vbs")));
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
