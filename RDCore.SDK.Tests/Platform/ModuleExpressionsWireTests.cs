using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;
using System.Collections.Immutable;

namespace RDCore.Tests.Platform;

/// <summary>
/// The facts of the expressions of a module's declarations, outside any procedure, travel with the module's model, from the environment host to an analyzer.
/// </summary>
[TestClass]
public sealed class ModuleExpressionsWireTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");

    private static ModuleSemanticsDto RoundTrip(ModuleSemanticModel model)
        => PlatformJson.Deserialize<SemanticsPayload>(PlatformJson.Serialize(new SemanticsPayload([ModuleSemanticsDto.From(model)]))).Modules.Single();

    // `Private Const C = Globals.N + 1`: the member access is bound to the constant of the other module.
    [TestMethod]
    public void AnExpressionOfAConstantsValue_IsReadBackWithItsBinding()
    {
        var node = new SyntaxNodeId("/Module1.bas", [1, 0]);
        var bound = new Uri("file:///c:/ws/Globals.bas#Globals.N");
        var fact = new ExpressionFact(node, new SourceLocation(Module, new SourceRange(1, 18, 1, 27)), VBIntegerType.TypeInfo,
            ExpressionClassification.Value, new SemanticId(bound), default);

        var read = RoundTrip(new ModuleSemanticModel(Module, [], [])
        {
            Expressions = ImmutableDictionary<SyntaxNodeId, ExpressionFact>.Empty.Add(node, fact),
        });

        Assert.IsNotNull(read.Expressions);
        var expression = read.Expressions.Value.Single();
        Assert.AreEqual(node, expression.Node);
        Assert.AreEqual(bound, expression.Binding);
        Assert.AreEqual(new SourceRange(1, 18, 1, 27), expression.Location.Range);
    }

    [TestMethod]
    public void AModuleWithNoExpressionOutsideAProcedure_HasNone()
        => Assert.IsNull(RoundTrip(new ModuleSemanticModel(Module, [], [])).Expressions);
}
