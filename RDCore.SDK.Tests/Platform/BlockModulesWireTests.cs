using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Platform;

/// <summary>
/// The modules of a file's blocks travel with the file's parse, as the platform writes it: each block's name, kind, address, range and tree, whose nodes
/// are keyed by the block's address and located in the file.
/// </summary>
[TestClass]
public sealed class BlockModulesWireTests
{
    private static readonly Uri File = new("file:///c:/ws/Backup.vbs");

    [TestMethod]
    public void TheModulesOfAFilesBlocks_AreReadBackAsTheyWereWritten()
    {
        var logger = RDCoreDocument.BlockUri(File, "Logger");
        var member = new MemberDeclarationNode(new SyntaxNodeId(logger.AbsolutePath, [0, 0]), new SourceLocation(File, new SourceRange(3, 0, 4, 7)), [],
            "Write", MemberKind.Procedure, AccessModifier.Public);
        var block = new BlockModule("Logger", ModuleType.ClassModule, logger, new SourceRange(2, 0, 5, 9),
            new ModuleNode(new SyntaxNodeId(logger.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(3, 0, 5, 0)), [member]));
        var own = new ModuleNode(new SyntaxNodeId(File.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(0, 0, 7, 0)), []);

        var read = PlatformJson.Deserialize<ModuleParseResult>(PlatformJson.Serialize(ModuleParseResult.Success(own) with { BlockModules = [block] }));

        var readBlock = read.BlockModules.Single();
        Assert.AreEqual(("Logger", ModuleType.ClassModule, logger, block.Range), (readBlock.Name, readBlock.Kind, readBlock.Uri, readBlock.Range));
        Assert.AreEqual(block.SyntaxTree.Identity, readBlock.SyntaxTree.Identity);
        Assert.AreEqual(block.SyntaxTree.SourceLocation, readBlock.SyntaxTree.SourceLocation);
        var readMember = (MemberDeclarationNode)readBlock.SyntaxTree.Children.Single();
        Assert.AreEqual((member.Identity, member.SourceLocation, "Write"), (readMember.Identity, readMember.SourceLocation, readMember.Name));
        Assert.AreEqual(own.Identity, read.SyntaxTree!.Identity);
        Assert.AreEqual(own.SourceLocation, read.SyntaxTree.SourceLocation);
    }

    [TestMethod]
    public void TheHeaderAndTheEndOfABlock_AreReadBackAsTheyWereWritten_AsBlockBoundaryTrivia()
    {
        var logger = RDCoreDocument.BlockUri(File, "Logger");
        BlockBoundaryTriviaNode[] boundaries =
        [
            new(new SyntaxNodeId(logger.AbsolutePath, [1]), new SourceLocation(File, new SourceRange(2, 0, 2, 13)),
                FileBlockKind.Module, false, "Module Logger"),
            new(new SyntaxNodeId(logger.AbsolutePath, [2]), new SourceLocation(File, new SourceRange(5, 0, 5, 12)),
                FileBlockKind.Module, true, "  End Module"),
        ];
        var own = new ModuleNode(new SyntaxNodeId(File.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(0, 0, 7, 0)), []);

        var json = PlatformJson.Serialize(ModuleParseResult.Success(own) with { Trivia = [.. boundaries] });
        var read = PlatformJson.Deserialize<ModuleParseResult>(json);

        StringAssert.Contains(json, "\"BlockBoundaryTrivia\"", "the name the node is written under");
        var readBoundaries = read.Trivia.Cast<BlockBoundaryTriviaNode>().ToArray();
        CollectionAssert.AreEqual(boundaries.Select(node => (node.Identity, node.SourceLocation, node.Kind, node.IsEnd, node.Source)).ToArray(),
            readBoundaries.Select(node => (node.Identity, node.SourceLocation, node.Kind, node.IsEnd, node.Source)).ToArray());
    }

    [TestMethod]
    public void AFileWithoutBlocks_HasNone()
    {
        var own = new ModuleNode(new SyntaxNodeId(File.AbsolutePath, [0]), new SourceLocation(File, new SourceRange(0, 0, 1, 0)), []);

        Assert.AreEqual(0, PlatformJson.Deserialize<ModuleParseResult>(PlatformJson.Serialize(ModuleParseResult.Success(own))).BlockModules.Length);
    }
}
