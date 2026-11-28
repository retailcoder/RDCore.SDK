using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;

namespace RDCore.Tests.Platform;

/// <summary>
/// The names a procedure uses that nothing declares travel with the procedure's model, from the environment host to the language server and an analyzer:
/// each part of each fact is read back as it was written.
/// </summary>
[TestClass]
public sealed class DeferredSymbolFactWireTests
{
    private static readonly Uri Procedure = new UriBuilder("file://rdcore-test") { Fragment = "Module1.Work" }.Uri;
    private static readonly Uri Total = new UriBuilder("file://rdcore-test") { Fragment = "Module1.Work.total" }.Uri;
    private static readonly Uri Document = new("file://rdcore-test/Module1.bas");

    private static SourceLocation At(int line, int start, int end)
        => new(Document, new SourceRange(new SourcePosition(line, start), new SourcePosition(line, end)));

    private static ProcedureSemanticsDto RoundTrip(ProcedureSemanticModel procedure)
    {
        var model = new ModuleSemanticModel(Procedure, [], [procedure]);
        return PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single()
            .Procedures.Single();
    }

    [TestMethod]
    public void ANameNothingDeclares_IsReadBackWithItsUsesAndCandidates()
    {
        var let = new DeferredUseFact(DeferredUseKind.Let, At(3, 4, 9));
        var call = new DeferredUseFact(DeferredUseKind.Call, At(4, 4, 9)) { ArgumentTypes = ["Integer", "String"] };
        var fact = new DeferredSymbolFact("total", At(3, 4, 9), [let, call], ["Integer", "Long"]) { Variable = Total, Resolved = "Long" };

        var read = RoundTrip(new ProcedureSemanticModel(new SemanticId(Procedure), []) { DeferredSymbols = [fact] }).DeferredSymbols;

        Assert.IsNotNull(read);
        var symbol = read.Value.Single();
        Assert.AreEqual("total", symbol.Name);
        Assert.AreEqual(fact.Location, symbol.Location);
        CollectionAssert.AreEqual(new[] { "Integer", "Long" }, symbol.Candidates.ToArray());
        Assert.AreEqual("Long", symbol.Resolved);
        // Uri equality ignores the fragment, which is what names the variable.
        Assert.AreEqual(Total.AbsoluteUri, symbol.Variable?.AbsoluteUri);
        CollectionAssert.AreEqual(new[] { DeferredUseKind.Let, DeferredUseKind.Call }, symbol.Uses.Select(use => use.Kind).ToArray());
        CollectionAssert.AreEqual(new[] { let.Location, call.Location }, symbol.Uses.Select(use => use.Location).ToArray());
        Assert.IsTrue(symbol.Uses[0].ArgumentTypes.IsDefaultOrEmpty);
        CollectionAssert.AreEqual(new[] { "Integer", "String" }, symbol.Uses[1].ArgumentTypes.ToArray());
    }

    [TestMethod]
    public void ANameUnderOptionExplicit_IsReadBackWithNoVariable()
    {
        var fact = new DeferredSymbolFact("total", At(3, 4, 9), [new DeferredUseFact(DeferredUseKind.Read, At(3, 4, 9))], []);

        var symbol = RoundTrip(new ProcedureSemanticModel(new SemanticId(Procedure), []) { DeferredSymbols = [fact] }).DeferredSymbols!.Value.Single();

        Assert.IsNull(symbol.Variable);
        Assert.IsNull(symbol.Resolved);
        Assert.IsTrue(symbol.Candidates.IsDefaultOrEmpty);
    }

    [TestMethod]
    public void AProcedureThatUsesNoUndeclaredName_HasNone()
        => Assert.IsNull(RoundTrip(new ProcedureSemanticModel(new SemanticId(Procedure), [])).DeferredSymbols);
}
