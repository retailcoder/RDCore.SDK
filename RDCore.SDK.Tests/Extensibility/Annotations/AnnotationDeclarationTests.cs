using System.Globalization;
using System.Text.Json;
using RDCore.SDK.Extensibility.Annotations;
using RDCore.SDK.Server.Configuration;
using RDCore.SDK.Tests.Extensibility.Annotations;

[assembly: ExtensionAnnotations(typeof(AnnotationFixtures), "RDCore.SDK.Tests.Extensibility.Annotations.AnnotationFixtureDescriptions")]

namespace RDCore.SDK.Tests.Extensibility.Annotations;

// the annotations this assembly declares as an extension would: one with a described parameter, described in English, and in French without the parameter.
public sealed class AnnotationFixtures : IAnnotationDeclarations
{
    public IReadOnlyList<AnnotationDeclaration> Declarations { get; } =
    [
        new AnnotationDeclaration
        {
            Name = "Fixture",
            Targets = AnnotationTargets.Member,
            Parameters = [new AnnotationParameter { Name = "Label", Takes = AnnotationArgumentKind.Text }],
        },
    ];
}

/// <summary>
/// What an annotation is (<see cref="AnnotationDeclaration"/>), how an extension declares its own (<see cref="ExtensionAnnotationsAttribute"/>), and how the
/// platform finds one among every extension's (<see cref="AnnotationCatalogue"/>).
/// </summary>
[TestClass]
public sealed class AnnotationDeclarationTests
{
    private static AnnotationDeclaration Declared(string name, AnnotationTargets targets = AnnotationTargets.Member,
        AnnotationModuleKinds kinds = AnnotationModuleKinds.All, params string[] aliases)
        => new() { Name = name, Targets = targets, ModuleKinds = kinds, Aliases = aliases };

    private static AnnotationSet Set(params AnnotationDeclaration[] declarations) => new() { Declarations = declarations };

    private static AnnotationParameter Parameter(string name, bool optional = false, bool repeats = false)
        => new() { Name = name, Takes = AnnotationArgumentKind.Text, IsOptional = optional, Repeats = repeats };

    [TestMethod]
    public void AnExtensionsAnnotations_AreTheOnesItsAttributeNames_DescribedInEachLanguage()
    {
        var set = ExtensionAnnotationsAttribute.Read(typeof(AnnotationDeclarationTests).Assembly);

        Assert.IsNotNull(set);
        var fixture = Assert.ContainsSingle(set.Declarations);
        Assert.AreEqual("Fixture", fixture.Name);
        CollectionAssert.AreEquivalent(SettingsSchema.Languages.ToArray(), set.Descriptions.Keys.ToArray());
        Assert.AreEqual("Marks a fixture.", set.Descriptions["en"]["Fixture"]);
        Assert.AreEqual("The fixture's label.", set.Descriptions["en"]["Fixture_Label"]);
        Assert.AreEqual("Marque un montage.", set.Descriptions["fr"]["Fixture"]);
        // what the French resources do not describe is what they fall back on.
        Assert.AreEqual("The fixture's label.", set.Descriptions["fr"]["Fixture_Label"]);
    }

    [TestMethod]
    public void AnAssemblyThatDeclaresNoAnnotations_HasNone()
        => Assert.IsNull(ExtensionAnnotationsAttribute.Read(typeof(object).Assembly));

    [TestMethod]
    [DataRow("Description", DisplayName = "its name")]
    [DataRow("DESCRIPTION", DisplayName = "its name, in another case")]
    [DataRow("MemberDescription", DisplayName = "an alias")]
    [DataRow("memberdescription", DisplayName = "an alias, in another case")]
    public void AnAnnotation_IsFoundByItsNameOrAnAlias_InAnyCase(string written)
    {
        var description = Declared("Description", aliases: "MemberDescription");
        var catalogue = new AnnotationCatalogue([Set(Declared("Folder"), description)]);

        Assert.AreSame(description, catalogue.Find(written));
    }

    [TestMethod]
    public void ANameNoExtensionDeclares_IsFoundNowhere()
        => Assert.IsNull(new AnnotationCatalogue([Set(Declared("Description"))]).Find("Descriptions"));

    [TestMethod]
    [DataRow("Shared", "Other", DisplayName = "the same name")]
    [DataRow("Other", "Shared", DisplayName = "a name the first one has as an alias")]
    public void AnAnnotationTwoExtensionsDeclare_IsTheFirstOnes(string secondName, string secondAlias)
    {
        var first = Declared("Shared", aliases: "Alias");
        var second = Declared(secondName, aliases: secondAlias);
        var catalogue = new AnnotationCatalogue([Set(first), Set(second)]);

        Assert.AreSame(first, catalogue.Find("Shared"));
        Assert.AreSame(first, Assert.ContainsSingle(catalogue.Declarations));
    }

    [TestMethod]
    public void TheAnnotationsLegalAtAPlace_AreOfItsTarget_InItsKindOfModule()
    {
        var moduleOnly = Declared("ModuleOnly", AnnotationTargets.Module);
        var member = Declared("Member", AnnotationTargets.Member | AnnotationTargets.Variable);
        var classMember = Declared("ClassMember", AnnotationTargets.Member, AnnotationModuleKinds.Class | AnnotationModuleKinds.Form);
        var catalogue = new AnnotationCatalogue([Set(moduleOnly, member, classMember)]);

        CollectionAssert.AreEqual(new[] { member }, catalogue.LegalAt(AnnotationTargets.Member, AnnotationModuleKinds.Standard).ToArray());
        CollectionAssert.AreEqual(new[] { member, classMember }, catalogue.LegalAt(AnnotationTargets.Member, AnnotationModuleKinds.Form).ToArray());
        CollectionAssert.AreEqual(new[] { moduleOnly }, catalogue.LegalAt(AnnotationTargets.Module, AnnotationModuleKinds.Class).ToArray());
    }

    [TestMethod]
    public void ADescription_IsInTheUsersLanguage_OrThePlatformsFirstWhenItHasNone()
    {
        var label = Parameter("Label");
        var annotation = Declared("Fixture") with { Parameters = [label] };
        var set = Set(annotation) with
        {
            Descriptions = new()
            {
                ["en"] = new() { ["Fixture"] = "Marks a fixture.", ["Fixture_Label"] = "The label." },
                ["fr"] = new() { ["Fixture"] = "Marque un montage." },
            },
        };
        var catalogue = new AnnotationCatalogue([set]);
        var french = CultureInfo.GetCultureInfo("fr-CA");

        Assert.AreEqual("Marque un montage.", catalogue.DescriptionOf(annotation, french));
        Assert.AreEqual("The label.", catalogue.DescriptionOf(annotation, label, french));
        Assert.AreEqual("Marks a fixture.", catalogue.DescriptionOf(annotation, CultureInfo.GetCultureInfo("de-DE")));
        Assert.IsNull(catalogue.DescriptionOf(Declared("Unknown"), french));
    }

    [TestMethod]
    [DataRow(0, 0, 0, false, false, DisplayName = "no parameter")]
    [DataRow(2, 0, 2, false, false, DisplayName = "two required")]
    [DataRow(1, 1, 2, false, false, DisplayName = "one required, one optional")]
    [DataRow(1, 0, -1, true, false, DisplayName = "one required, then an optional list")]
    [DataRow(0, 0, -1, true, true, DisplayName = "a required list")]
    public void TheArgumentsAnAnnotationTakes_AreCountedFromItsParameters(int required, int optional, int max, bool list, bool listRequired)
    {
        AnnotationParameter[] parameters =
        [
            .. Enumerable.Range(0, required).Select(index => Parameter($"R{index}")),
            .. Enumerable.Range(0, optional).Select(index => Parameter($"O{index}", optional: true)),
            .. list ? [Parameter("Items", optional: !listRequired, repeats: true)] : Array.Empty<AnnotationParameter>(),
        ];
        var annotation = Declared("Counted") with { Parameters = parameters };

        Assert.AreEqual(required + (listRequired ? 1 : 0), annotation.MinArguments);
        Assert.AreEqual(max < 0 ? null : max, annotation.MaxArguments);
        Assert.IsTrue(annotation.IsWellFormed);
    }

    [TestMethod]
    public void ARequiredParameterAfterAnOptionalOne_IsNotWellFormed()
        => Assert.IsFalse((Declared("Bad") with { Parameters = [Parameter("A", optional: true), Parameter("B")] }).IsWellFormed);

    [TestMethod]
    public void AListThatIsNotTheLastParameter_IsNotWellFormed()
        => Assert.IsFalse((Declared("Bad") with { Parameters = [Parameter("Items", repeats: true), Parameter("B", optional: true)] }).IsWellFormed);

    // the shape an extension's manifest carries and the language server reads: both sides rely on it.
    [TestMethod]
    public void ADeclaration_IsWrittenInAManifestAsItsFields_AndReadBackAsItself()
    {
        var annotation = new AnnotationDeclaration
        {
            Name = "IgnoreModule",
            Aliases = ["IgnoreModuleAlias"],
            Targets = AnnotationTargets.Module,
            ModuleKinds = AnnotationModuleKinds.Standard | AnnotationModuleKinds.Class,
            Repeats = true,
            Parameters = [new AnnotationParameter { Name = "Codes", Takes = AnnotationArgumentKind.DiagnosticCode, IsOptional = true, Repeats = true }],
            IsDeprecated = true,
            ReplacedBy = "Ignore",
        };

        var json = JsonSerializer.Serialize(annotation);

        Assert.AreEqual(
            """{"Name":"IgnoreModule","Aliases":["IgnoreModuleAlias"],"Targets":"Module","ModuleKinds":"Standard, Class","Repeats":true,"Parameters":"""
            + """[{"Name":"Codes","Takes":"DiagnosticCode","IsOptional":true,"Repeats":true}],"IsDeprecated":true,"ReplacedBy":"Ignore"}""",
            json);
        var read = JsonSerializer.Deserialize<AnnotationDeclaration>(json)!;
        Assert.AreEqual(json, JsonSerializer.Serialize(read));
    }
}
