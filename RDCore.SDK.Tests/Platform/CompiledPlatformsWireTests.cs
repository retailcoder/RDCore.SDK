using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Runtime.Abstract.Execution;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// The platforms a <c>Declare</c> statement is declared on, and those of a call to what it declares, travel with the facts from the environment host to an
/// analyzer.
/// </summary>
[TestClass]
public sealed class CompiledPlatformsWireTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");

    private static DeclarationFactDto Declare(CompiledPlatforms? compiledOn)
        => new(new Uri("file:///c:/ws/Module1.bas#Module1.GetTickCount"), "GetTickCount", DeclarationKind.Procedure, AccessModifier.Private, false,
            new SourceLocation(Module, new SourceRange(2, 0, 2, 60)), null, CompiledOn: compiledOn);

    private static ExpressionFactDto Call(NativeCallPlatforms? platforms)
        => ExpressionFactDto.From(new ExpressionFact(new SyntaxNodeId("/Module1.bas", [5, 0]), new SourceLocation(Module, new SourceRange(5, 8, 5, 20)),
            VBLongType.TypeInfo, ExpressionClassification.Function, null, default) { NativeCall = platforms });

    [TestMethod]
    [DataRow(CompiledPlatforms.All)]
    [DataRow(CompiledPlatforms.Windows32 | CompiledPlatforms.Windows64)]
    [DataRow(CompiledPlatforms.MacOS)]
    [DataRow(CompiledPlatforms.None)]
    public void ThePlatformsADeclareIsDeclaredOn_AreReadBackAsTheyWereWritten(CompiledPlatforms platforms)
        => Assert.AreEqual(platforms, PlatformJson.Deserialize<DeclarationFactDto>(PlatformJson.Serialize(Declare(platforms))).CompiledOn);

    [TestMethod]
    public void ADeclarationThatIsNoDeclare_HasNoPlatforms()
        => Assert.IsNull(PlatformJson.Deserialize<DeclarationFactDto>(PlatformJson.Serialize(Declare(null))).CompiledOn);

    [TestMethod]
    public void ThePlatformsOfANativeCall_AreReadBackAsTheyWereWritten()
    {
        var written = new NativeCallPlatforms(CompiledPlatforms.All, CompiledPlatforms.All, [CompiledPlatforms.MacOS, ~CompiledPlatforms.MacOS & CompiledPlatforms.All]);

        var read = PlatformJson.Deserialize<ExpressionFactDto>(PlatformJson.Serialize(Call(written))).NativeCall;

        Assert.AreEqual(written, read);
        CollectionAssert.AreEqual(written.DeclaredByStatement.ToArray(), read!.Value.DeclaredByStatement.ToArray());
    }

    [TestMethod]
    public void ThePlatformsOfTwoCalls_AreEqual_WhenTheyStateTheSamePlatformsForEachStatement()
    {
        var one = new NativeCallPlatforms(CompiledPlatforms.All, CompiledPlatforms.All, [CompiledPlatforms.MacOS, CompiledPlatforms.Windows64]);
        var other = new NativeCallPlatforms(CompiledPlatforms.All, CompiledPlatforms.All, [CompiledPlatforms.MacOS, CompiledPlatforms.Windows64]);

        Assert.AreEqual(one, other);
        Assert.AreEqual(one.GetHashCode(), other.GetHashCode());
        Assert.AreNotEqual(one, other with { DeclaredByStatement = [CompiledPlatforms.Windows64, CompiledPlatforms.MacOS] }, "in the order they are written");
    }

    [TestMethod]
    public void ACallGivenNoStatements_HasNone()
    {
        var call = new NativeCallPlatforms(CompiledPlatforms.All, CompiledPlatforms.All, default);

        Assert.IsEmpty(call.DeclaredByStatement);
        Assert.AreEqual(new NativeCallPlatforms(CompiledPlatforms.All, CompiledPlatforms.All, []), call);
    }

    [TestMethod]
    public void TheStatementsOfADeclare_AreReadBackAsTheyWereWritten_InOrder()
    {
        var declaration = new DeclarationFact(new SemanticId(new Uri("file:///c:/ws/Module1.bas#Module1.Ticks")), "Ticks", DeclarationKind.Procedure,
            AccessModifier.Private, false, new SourceLocation(Module, new SourceRange(2, 0, 2, 60)), null)
        {
            CompiledOn = CompiledPlatforms.All,
            StatementsCompiledOn = [CompiledPlatforms.MacOS, CompiledPlatforms.Windows32 | CompiledPlatforms.Windows64 | CompiledPlatforms.Linux32 | CompiledPlatforms.Linux64],
        };

        var read = PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(new ModuleSemanticModel(Module, [], []) { Declarations = [declaration] })])));

        CollectionAssert.AreEqual(declaration.StatementsCompiledOn.ToArray(), read.Modules.Single().Declarations.Single().StatementsCompiledOn!.Value.ToArray());
    }

    [TestMethod]
    public void ADeclarationThatIsNoDeclare_HasNoStatementPlatforms_OnTheWire()
    {
        var declaration = new DeclarationFact(new SemanticId(new Uri("file:///c:/ws/Module1.bas#Module1.x")), "x", DeclarationKind.Variable,
            AccessModifier.Private, false, new SourceLocation(Module, new SourceRange(2, 0, 2, 10)), null);

        var read = PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(new ModuleSemanticModel(Module, [], []) { Declarations = [declaration] })])));

        Assert.IsNull(read.Modules.Single().Declarations.Single().StatementsCompiledOn);
    }

    [TestMethod]
    public void AnExpressionBoundToNoDeclare_HasNoNativeCall()
        => Assert.IsNull(PlatformJson.Deserialize<ExpressionFactDto>(PlatformJson.Serialize(Call(null))).NativeCall);

    [TestMethod]
    public void ADeclareOfTheModelOfAModule_KeepsItsPlatforms_OnTheWire()
    {
        var declaration = new DeclarationFact(new SemanticId(new Uri("file:///c:/ws/Module1.bas#Module1.GetTickCount")), "GetTickCount", DeclarationKind.Procedure,
            AccessModifier.Private, false, new SourceLocation(Module, new SourceRange(2, 0, 2, 60)), null) { CompiledOn = CompiledPlatforms.MacOS };

        var read = PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(new ModuleSemanticModel(Module, [], []) { Declarations = [declaration] })])));

        Assert.AreEqual(CompiledPlatforms.MacOS, read.Modules.Single().Declarations.Single().CompiledOn);
    }

    [TestMethod]
    public void EachPlatform_IsOneFlag_AndTogetherTheyAreAll()
    {
        var flags = CompiledPlatformProfiles.Each.Select(profile => profile.Platform).ToArray();

        Assert.IsTrue(flags.All(flag => flag != CompiledPlatforms.None && (flag & (flag - 1)) == 0), "one bit each");
        Assert.AreEqual(CompiledPlatforms.All, flags.Aggregate(CompiledPlatforms.None, (all, flag) => all | flag));
        Assert.AreEqual(flags.Length, flags.Distinct().Count());
    }

    [TestMethod]
    [DataRow(CompiledPlatforms.Windows32, VBOperatingSystem.Windows, false)]
    [DataRow(CompiledPlatforms.Windows64, VBOperatingSystem.Windows, true)]
    [DataRow(CompiledPlatforms.MacOS, VBOperatingSystem.MacOS, true)]
    [DataRow(CompiledPlatforms.Linux32, VBOperatingSystem.Linux, false)]
    [DataRow(CompiledPlatforms.Linux64, VBOperatingSystem.Linux, true)]
    public void EachPlatform_IsTheEnvironmentItsConstantsAreThoseOf(CompiledPlatforms platform, VBOperatingSystem system, bool is64Bit)
        => Assert.AreEqual((platform, system, is64Bit), CompiledPlatformProfiles.Each.Single(profile => profile.Platform == platform));
}
