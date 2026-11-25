using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
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

    // a module's tree: its identity is keyed by the module's address, its location is in the file.
    private static ModuleNode Module(Uri module, Uri file, int startLine, int endLine)
        => new(new SyntaxNodeId(module.AbsolutePath, [0]), new SourceLocation(file, new SourceRange(startLine, 0, endLine, 0)), []);
}
