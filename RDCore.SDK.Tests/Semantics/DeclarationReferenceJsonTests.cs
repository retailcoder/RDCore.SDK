using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The references the host states of a declaration cross the wire with it, each with its module, its procedure and what it does.
/// </summary>
[TestClass]
public sealed class DeclarationReferenceJsonTests
{
    private static readonly Uri Module1 = new("file:///c:/ws/Module1.bas");
    private static readonly Uri Module2 = new("file:///c:/ws/Module2.bas");

    private static DeclarationFactDto Declaration(DeclarationReferences? references, params DeclarationReference[] sites)
        => new(new Uri("file:///c:/ws/Module1.bas#Module1.Compute"), "Compute", DeclarationKind.Procedure, AccessModifier.Public, false,
            new SourceLocation(Module1, new SourceRange(1, 16, 1, 23)), references, ReferencedFrom: references is null ? null : [.. sites]);

    [TestMethod]
    public void TheReferencesOfADeclaration_AreReadBackAsTheyWereWritten()
    {
        var written = Declaration(new DeclarationReferences(2, 0, 1),
            new DeclarationReference(new SourceLocation(Module2, new SourceRange(3, 4, 3, 11)), new Uri("file:///c:/ws/Module2.bas#Module2.Main"), DeclarationReferenceKind.Read)
            {
                DiscardsResult = true,
            },
            new DeclarationReference(new SourceLocation(Module2, new SourceRange(4, 10, 4, 17)), new Uri("file:///c:/ws/Module2.bas#Module2.Main"), DeclarationReferenceKind.Read),
            new DeclarationReference(new SourceLocation(Module1, new SourceRange(0, 25, 0, 32)), null, DeclarationReferenceKind.PassedAsArgument));

        var read = PlatformJson.Deserialize<DeclarationFactDto>(PlatformJson.Serialize(written));

        Assert.AreEqual(written.References, read.References);
        CollectionAssert.AreEqual(written.ReferencedFrom!.Value.ToArray(), read.ReferencedFrom!.Value.ToArray());
    }

    [TestMethod]
    public void ADeclarationWhoseReferencesAreNotKnown_HasNoneListed()
        => Assert.IsNull(PlatformJson.Deserialize<DeclarationFactDto>(PlatformJson.Serialize(Declaration(null))).ReferencedFrom);
}
