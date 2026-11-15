using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using RDCore.SDK.Assistant;
using RDCore.SDK.Client;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics;
using RDCore.SDK.Semantics.Flags;
using RDCore.SDK.Server.Assistant;
using RDCore.SDK.Server.Serialization;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmniSharpSerializer = OmniSharp.Extensions.JsonRpc.ISerializer;

namespace RDCore.Tests.Platform;

/// <summary>
/// The answers of <c>rdcore/semantics/describe</c> and <c>/outline</c> travel with LSP's 0-based positions and their enumerations by name, as the Protocol
/// options and the platform's serializers write them; the assistant tools answer the same facts 1-based, with paths from the workspace root.
/// </summary>
[TestClass]
public sealed class SemanticsDescribeWireTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "rdcore-describe", "workspace");
    private static readonly Uri Module = new(Path.Combine(Root, "src", "Module1.bas"));
    private static readonly string ModuleJson = JsonSerializer.Serialize(Module.OriginalString);

    private static IEnumerable<OmniSharpSerializer> PlatformSerializers() => [new LspSerializer(), new PullDiagnosticsLspSerializer()];

    private static DescribeResult Described() => new()
    {
        Symbol = new SymbolDescription
        {
            Id = new Uri("vba://project/Module1/count"),
            Name = "count",
            Kind = DescribedSymbolKind.Variable,
            Declaration = "Private count As Long",
            Container = "Module1",
            DeclaredType = "Long",
            TypeKind = DeclaredTypeKind.Intrinsic,
            Access = AccessModifier.Private,
            Scope = DescribedScope.Module,
            Location = new SourceLocation(Module, new SourceRange(1, 8, 1, 13)),
            IsImplicit = false,
            Role = DeclarationRole.None,
            References = new DeclarationReferences(2, 1, 0),
        },
        Expression = new ExpressionDescription
        {
            Range = new SourceRange(4, 4, 4, 9),
            Classification = ExpressionClassification.Variable,
            DeclaredType = "Long",
            TypeKind = DeclaredTypeKind.Intrinsic,
            Binding = new Uri("vba://project/Module1/count"),
            ConstantValue = new RunValue("Long", "42"),
        },
        Range = new SourceRange(4, 4, 4, 9),
    };

    private static string DescribedJson() => """{"symbol":{"location":{"uri":""" + ModuleJson + ""","range":{"start":{"line":1,"character":8},"end":{"line":1,"character":13},"isEmpty":false}},"id":"vba://project/Module1/count","name":"count","kind":"Variable","declaration":"Private count As Long","container":"Module1","library":null,"declaredType":"Long","typeKind":"Intrinsic","isArray":false,"access":"Private","scope":"Module","isImplicit":false,"role":"None","references":{"reads":2,"writes":1,"passedAsArguments":0,"total":3},"hides":null,"autoInstantiates":null,"documentation":null,"controlArrayOf":null},"expression":{"range":{"start":{"line":4,"character":4},"end":{"line":4,"character":9},"isEmpty":false},"classification":"Variable","declaredType":"Long","typeKind":"Intrinsic","isArray":false,"binding":"vba://project/Module1/count","flags":[],"constantValue":{"typeName":"Long","value":"42"},"accessors":[],"isLetCoerced":false,"defaultMember":null,"creates":null,"error":null},"range":{"start":{"line":4,"character":4},"end":{"line":4,"character":9},"isEmpty":false}}""";

    private static ModuleOutline Outlined() => new()
    {
        Uri = Module,
        Name = "Module1",
        ModuleType = ModuleType.StdModule,
        Language = "vba",
        OptionExplicit = true,
        Entries =
        [
            new OutlineEntry
            {
                Name = "Point", Kind = DescribedSymbolKind.Type, Id = new Uri("vba://project/Module1/Point"), Declaration = "Public Type Point", Access = AccessModifier.Public,
                Range = new SourceRange(2, 0, 5, 8), SelectionRange = new SourceRange(2, 12, 2, 17),
                Children =
                [
                    new OutlineEntry
                    {
                        Name = "X", Kind = DescribedSymbolKind.TypeMember, Id = new Uri("vba://project/Module1/Point/X"), Declaration = "X As Long", DeclaredType = "Long",
                        TypeKind = DeclaredTypeKind.Intrinsic, Range = new SourceRange(3, 4, 3, 13), SelectionRange = new SourceRange(3, 4, 3, 5),
                    },
                ],
            },
        ],
    };

    [TestMethod]
    public void ADescription_IsWrittenAsItsGoldenJsonSays_ByTheProtocolOptionsAndByThePlatform_AndReadBack()
    {
        var json = DescribedJson();

        AssertSameJson(json, JsonSerializer.Serialize(Described(), RDCorePlatformJson.Options), "options");
        foreach (var serializer in PlatformSerializers())
        {
            AssertSameJson(json, serializer.SerializeObject(Described()), serializer.GetType().Name);
            var read = (DescribeResult)serializer.DeserializeObject(json, typeof(DescribeResult));
            Assert.AreEqual(Described().Symbol!.Location, read.Symbol!.Location);
            Assert.AreEqual(DeclarationRole.None, read.Symbol.Role);
            Assert.AreEqual(Described().Expression!.ConstantValue, read.Expression!.ConstantValue);
        }
    }

    [TestMethod]
    public void AnOutline_NamesItsKindsAndTypes_AndReadsBackTheSame()
    {
        var written = JsonSerializer.Serialize(Outlined(), RDCorePlatformJson.Options);
        var node = JsonNode.Parse(written)!;

        Assert.AreEqual("StdModule", (string?)node["moduleType"]);
        Assert.AreEqual("Type", (string?)node["entries"]![0]!["kind"]);
        Assert.AreEqual("TypeMember", (string?)node["entries"]![0]!["children"]![0]!["kind"]);
        Assert.AreEqual("Public", (string?)node["entries"]![0]!["access"]);
        foreach (var serializer in PlatformSerializers())
        {
            AssertSameJson(written, serializer.SerializeObject(Outlined()), serializer.GetType().Name);
            var read = (ModuleOutline)serializer.DeserializeObject(written, typeof(ModuleOutline));
            AssertSameJson(written, JsonSerializer.Serialize(read, RDCorePlatformJson.Options), serializer.GetType().Name + ", read back");
        }
    }

    [TestMethod]
    public void TheDescriptionOfAnExpressionFact_NamesItsFlagsAndAccessors_AndWritesItsConstantAndError()
    {
        var fact = new ExpressionFactDto(
            new SyntaxNodeId(Module.ToString(), [0, 1]),
            new SourceLocation(Module, new SourceRange(4, 4, 4, 9)),
            "Long",
            ExpressionClassification.Property,
            new Uri("vba://project/Class1/Size"),
            ValueExpressionSemanticFlags.LateBound | ValueExpressionSemanticFlags.DefaultMember,
            new CompileErrorDto(new RDCoreDiagnosticCode(RDCoreDiagnosticCode.CompilePrefix, 13), new SourceLocation(Module, new SourceRange(4, 4, 4, 9)), "Type mismatch", "v"),
            TypeKind: DeclaredTypeKind.Intrinsic,
            ConstantValue: new VBLongValue(42),
            Accessors: PropertyAccessors.Get | PropertyAccessors.Let);

        var described = ExpressionDescription.Of(fact);

        CollectionAssert.AreEqual(new[] { "LateBound", "DefaultMember" }, described.Flags.ToArray());
        CollectionAssert.AreEqual(new[] { "Get", "Let" }, described.Accessors.ToArray());
        Assert.AreEqual(new RunValue("Long", "42"), described.ConstantValue);
        Assert.AreEqual(new SourceRange(4, 4, 4, 9), described.Range);
        Assert.AreEqual(ExpressionClassification.Property, described.Classification);
        Assert.AreEqual(new RDCoreDiagnosticCode(RDCoreDiagnosticCode.CompilePrefix, 13).ToString(), described.Error!.Code);
        Assert.AreEqual("Type mismatch", described.Error.Description);
    }

    [TestMethod]
    public void TheToolsDescription_IsOneBased_WithItsPathFromTheWorkspaceRoot()
    {
        var tool = DescribeToolResult.Of(Root, Described());

        Assert.AreEqual(new TextLocation("src/Module1.bas", new TextRange(new(2, 9), new(2, 14))), tool.Symbol!.Location);
        Assert.AreEqual(new TextRange(new(5, 5), new(5, 10)), tool.Expression!.Range);
        Assert.AreEqual(new TextRange(new(5, 5), new(5, 10)), tool.Range);
        Assert.AreEqual("count", tool.Symbol.Name);
        Assert.AreEqual(new DeclarationReferences(2, 1, 0), tool.Symbol.References);
        Assert.AreEqual(new RunValue("Long", "42"), tool.Expression.ConstantValue);

        var json = JsonNode.Parse(JsonSerializer.Serialize(tool, RDCorePlatformJson.Options))!;
        Assert.AreEqual("src/Module1.bas", (string?)json["symbol"]!["location"]!["path"]);
        Assert.AreEqual(2, (int)json["symbol"]!["location"]!["range"]!["start"]!["line"]!);
        Assert.AreEqual("Variable", (string?)json["symbol"]!["kind"]);
    }

    [TestMethod]
    public void TheToolsOutline_IsOneBased_AndCutAtItsCap()
    {
        var tool = OutlineToolResult.Of(Root, Outlined());

        Assert.AreEqual("src/Module1.bas", tool.Path);
        Assert.AreEqual(new TextRange(new(3, 13), new(3, 18)), tool.Entries[0].SelectionRange);
        Assert.AreEqual(new TextRange(new(4, 5), new(4, 6)), tool.Entries[0].Children[0].SelectionRange);
        Assert.AreEqual((1, false), (tool.Total, tool.Truncated));

        var many = Outlined() with { Entries = [.. Enumerable.Repeat(Outlined().Entries[0], ToolResultLimits.OutlineEntries + 1)] };
        var cut = OutlineToolResult.Of(Root, many);
        Assert.HasCount(ToolResultLimits.OutlineEntries, cut.Entries);
        Assert.AreEqual((ToolResultLimits.OutlineEntries + 1, true), (cut.Total, cut.Truncated));
    }

    [TestMethod]
    public void TheToolsResults_HaveSchemas_ThatNameTheirKinds()
    {
        var describe = AssistantToolSchemas.Of(typeof(DescribeToolResult), _ => null);
        var outline = AssistantToolSchemas.Of(typeof(OutlineToolResult), _ => null);

        var kind = describe["properties"]!["symbol"]!["properties"]!["kind"]!;
        CollectionAssert.Contains(kind["enum"]!.AsArray().Select(name => (string?)name).ToArray(), "EnumMember");
        Assert.IsNotNull(outline["properties"]!["entries"]);
    }

    private static void AssertSameJson(string expected, string actual, string what)
        => Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)), $"{what}\nexpected: {expected}\nactual:   {actual}");
}
