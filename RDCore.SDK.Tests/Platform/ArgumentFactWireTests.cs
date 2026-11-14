using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// The parameter each argument of a procedure's calls is mapped to travels with the procedure's model, from the environment host to an analyzer, as the
/// platform writes the semantics: each part of each fact is read back as it was written.
/// </summary>
[TestClass]
public sealed class ArgumentFactWireTests
{
    private static readonly Uri Procedure = new UriBuilder("file://rdcore-test") { Fragment = "Module1.Caller" }.Uri;
    private static readonly Uri Parameter = new UriBuilder("file://rdcore-test") { Fragment = "Module1.Callee.n" }.Uri;

    private static ModuleSemanticsDto RoundTrip(params ArgumentFact[] arguments)
    {
        var model = new ModuleSemanticModel(Procedure, [], [new ProcedureSemanticModel(new SemanticId(Procedure), []) { Arguments = [.. arguments] }]);
        return PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single();
    }

    [TestMethod]
    public void TheArgumentsOfAProcedure_AreReadBackAsTheyWereWritten()
    {
        var written = new ArgumentFact(new SyntaxNodeId("/Module1.bas", [3, 0, 1]), Parameter, ParameterKind.ExplicitByVal, "Long",
            IsOptional: true, HasDefaultValue: true, IsParamArray: false, IsExternal: true, IsLibrary: false);
        var array = written with { Argument = new SyntaxNodeId("/Module1.bas", [3, 0, 2]), Mechanism = ParameterKind.ImplicitByRef, ParameterType = "Variant",
            IsOptional = false, HasDefaultValue = false, IsParamArray = true, IsExternal = false, IsLibrary = true };

        var read = RoundTrip(written, array).Procedures.Single().Arguments;

        Assert.IsNotNull(read);
        CollectionAssert.AreEqual(new[] { written, array }, read.Value.ToArray());
        // Uri equality ignores the fragment, which is what names the parameter.
        Assert.AreEqual(Parameter.AbsoluteUri, read.Value[0].Parameter.AbsoluteUri);
    }

    [TestMethod]
    public void AProcedureWithNoArgumentMapped_HasNone()
        => Assert.IsNull(RoundTrip().Procedures.Single().Arguments);
}
