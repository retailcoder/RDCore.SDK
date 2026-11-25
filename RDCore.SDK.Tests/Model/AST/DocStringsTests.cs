using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.Tests.Model.AST;

/// <summary>
/// The descriptions a module writes (<see cref="DocStrings"/>): its attributes, else its annotations, each bound where RD-VBAL §3.0.1.1 binds it.
/// The trees are built as the parser builds them: an attribute of a member in the member's body, a variable's after its declaration, each annotation inside
/// the comment line that holds it.
/// </summary>
[TestClass]
public sealed class DocStringsTests
{
    private static readonly Uri File = new("file:///c:/ws/Module1.cls");
    private static readonly Uri OtherFile = new("file:///c:/ws/Module1.Part.cls");
    private static int _next;

    private static SyntaxNodeId Id() => new(File.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation Lines(int first, int last, Uri? file = null, int startCharacter = 0, int endCharacter = 40)
        => new() { Uri = file ?? File, Range = new SourceRange(first, startCharacter, last, endCharacter) };

    private static AttributeDirectiveNode Attribute(int line, string name, string value, string? binding = null)
        => new(Id(), Lines(line, line), name, value, binding);

    private static MemberDeclarationNode Procedure(string name, int first, int last, Uri? file = null, SyntaxNode[]? body = null)
        => new(Id(), Lines(first, last, file), [.. body ?? []], name, MemberKind.Procedure);

    private static VariableDeclarationNode Variable(string name, int line, int endCharacter = 40)
        => new(Id(), Lines(line, line, endCharacter: endCharacter), name, []);

    private static CommentTriviaNode Comment(int line, Uri? file = null, int startCharacter = 0)
        => new(Id(), Lines(line, line, file, startCharacter), "' a comment");

    // a comment line that holds one annotation, as the parser keeps it: the comment, then the annotation inside its range.
    private static SyntaxNode[] Annotated(int line, string name, VBTypedValue? argument, Uri? file = null, int startCharacter = 0)
    {
        var location = Lines(line, line, file, startCharacter);
        SyntaxNode[] arguments = argument is null ? [] : [new LiteralExpressionNode(Id(), location, argument)];
        return [new CommentTriviaNode(Id(), location, $"'@{name}"), new AnnotationTriviaNode(Id(), location, name, [.. arguments])];
    }

    private static SyntaxNode[] Annotated(int line, string name, string text, Uri? file = null, int startCharacter = 0)
        => Annotated(line, name, new VBStringValue(text), file, startCharacter);

    private static ModuleParseResult Parse(SyntaxNode[] children, params SyntaxNode[][] trivia)
        => new() { SyntaxTree = new ModuleNode(Id(), Lines(0, 30), [.. children]), Trivia = [.. trivia.SelectMany(nodes => nodes)] };

    [TestMethod]
    public void AModule_IsDescribedByItsAttribute_AsAStringIsWritten()
    {
        var parse = Parse([Attribute(0, Tokens.VB_Name, "\"Module1\""), Attribute(1, Tokens.VB_Description, "\"Counts \"\"things\"\".\"")]);

        Assert.AreEqual("Counts \"things\".", parse.SyntaxTree!.GetModuleDescription());
        Assert.AreEqual("Counts \"things\".", parse.GetModuleDescription());
    }

    [TestMethod]
    public void AModuleThatWritesNoAttribute_IsDescribedByItsModuleDescriptionAnnotation()
    {
        var parse = Parse([Attribute(0, Tokens.VB_Name, "\"Module1\"")], Annotated(1, DocStrings.ModuleDescriptionAnnotation, "Counts things."));

        Assert.IsNull(parse.SyntaxTree!.GetModuleDescription());
        Assert.AreEqual("Counts things.", parse.GetModuleDescription());
    }

    [TestMethod]
    public void TheModulesAttribute_IsItsDescription_WhenAnAnnotationSaysAnother()
    {
        var parse = Parse([Attribute(1, Tokens.VB_Description, "\"The attribute.\"")],
            Annotated(2, DocStrings.ModuleDescriptionAnnotation, "The annotation."));

        Assert.AreEqual("The attribute.", parse.GetModuleDescription());
        Assert.IsInstanceOfType<AttributeDirectiveNode>(parse.GetModuleDescriptionNode());
    }

    [TestMethod]
    public void AModuleDescriptionAnnotation_RightAboveTheFirstProcedure_DescribesTheModule()
    {
        var parse = Parse([Procedure("Main", 3, 5)], Annotated(2, DocStrings.ModuleDescriptionAnnotation, "Counts things."));

        Assert.AreEqual("Counts things.", parse.GetModuleDescription());
        Assert.IsNull(parse.GetMemberDescription("Main"));
    }

    [TestMethod]
    public void AModuleDescriptionAnnotation_AfterTheFirstProcedureStarts_DescribesNothing()
    {
        var parse = Parse([Procedure("Main", 1, 3), Procedure("Other", 5, 7)], Annotated(4, DocStrings.ModuleDescriptionAnnotation, "Counts things."));

        Assert.IsNull(parse.GetModuleDescription());
    }

    [TestMethod]
    public void AMember_IsDescribedByTheAttributeInItsBody()
    {
        var parse = Parse([Procedure("Main", 1, 4, body: [Attribute(2, Tokens.VB_Description, "\"Runs.\"", "Main")])]);

        Assert.AreEqual("Runs.", parse.GetMemberDescription("main"));
    }

    [TestMethod]
    public void AMemberThatWritesNoAttribute_IsDescribedByTheDescriptionAnnotationAboveIt_ThroughTheCommentLinesBetween()
    {
        var parse = Parse([Procedure("Main", 4, 6)], Annotated(2, DocStrings.MemberDescriptionAnnotation, "Runs."), [Comment(3)]);

        Assert.AreEqual("Runs.", parse.GetMemberDescription("Main"));
    }

    [TestMethod]
    public void TheMembersAttribute_IsItsDescription_WhenAnAnnotationSaysAnother()
    {
        var parse = Parse([Procedure("Main", 3, 6, body: [Attribute(4, Tokens.VB_Description, "\"The attribute.\"", "Main")])],
            Annotated(2, DocStrings.MemberDescriptionAnnotation, "The annotation."));

        Assert.AreEqual("The attribute.", parse.GetMemberDescription("Main"));
    }

    [TestMethod]
    public void OfTwoAnnotationsAboveAMember_TheFirstWrittenDescribesIt()
    {
        var parse = Parse([Procedure("Main", 4, 6)],
            Annotated(2, DocStrings.MemberDescriptionAnnotation, "The first."),
            Annotated(3, DocStrings.MemberDescriptionAnnotation, "The second."));

        Assert.AreEqual("The first.", parse.GetMemberDescription("Main"));
    }

    [TestMethod]
    public void ADescriptionAnnotation_WithABlankLineBetweenItAndTheMember_DescribesNothing()
    {
        var parse = Parse([Procedure("Main", 4, 6)], Annotated(2, DocStrings.MemberDescriptionAnnotation, "Runs."));

        Assert.IsNull(parse.GetMemberDescription("Main"));
    }

    [TestMethod]
    public void ADescriptionAnnotation_WithCodeBetweenItAndTheMember_DescribesNothing()
    {
        var parse = Parse([Variable("Count", 3), Procedure("Main", 4, 6)], Annotated(2, DocStrings.MemberDescriptionAnnotation, "Runs."));

        Assert.IsNull(parse.GetMemberDescription("Main"));
        Assert.IsNull(parse.GetMemberDescription("Count"));
    }

    [TestMethod]
    public void ADescriptionAnnotation_AfterCodeOnItsLine_IsThatCodesComment_AndDescribesNothingBelow()
    {
        var parse = Parse([Variable("Count", 3, endCharacter: 10), Procedure("Main", 4, 6)],
            Annotated(3, DocStrings.MemberDescriptionAnnotation, "Runs.", startCharacter: 12));

        Assert.IsNull(parse.GetMemberDescription("Main"));
    }

    [TestMethod]
    public void AModuleVariable_IsDescribedByItsVarDescriptionAttribute_ElseByTheVariableDescriptionAnnotationAboveIt()
    {
        var parse = Parse([Variable("Count", 1), Attribute(2, Tokens.VB_VarDescription, "\"How many.\"", "Count"), Variable("Total", 4)],
            Annotated(3, DocStrings.VariableDescriptionAnnotation, "How much."));

        Assert.AreEqual("How many.", parse.SyntaxTree!.GetVariableDescription("count"));
        Assert.AreEqual("How many.", parse.GetMemberDescription("Count"));
        Assert.IsNull(parse.SyntaxTree!.GetVariableDescription("Total"));
        Assert.AreEqual("How much.", parse.GetMemberDescription("Total"));
    }

    [TestMethod]
    public void AnAnnotationForOneKindOfDeclaration_DoesNotDescribeTheOtherKind()
    {
        var parse = Parse([Variable("Count", 2), Procedure("Main", 5, 7)],
            Annotated(1, DocStrings.MemberDescriptionAnnotation, "Runs."), Annotated(4, DocStrings.VariableDescriptionAnnotation, "How many."));

        Assert.IsNull(parse.GetMemberDescription("Count"));
        Assert.IsNull(parse.GetMemberDescription("Main"));
    }

    [TestMethod]
    public void InATreeOfSeveralFiles_AnAnnotationDescribesADeclarationOfItsOwnFileOnly()
    {
        // the two parts write their lines at the same positions: only the file tells the annotation's member apart.
        var parse = Parse([Procedure("Main", 5, 7), Procedure("Other", 5, 7, OtherFile)],
            Annotated(4, DocStrings.MemberDescriptionAnnotation, "Of the other part.", OtherFile));

        Assert.IsNull(parse.GetMemberDescription("Main"));
        Assert.AreEqual("Of the other part.", parse.GetMemberDescription("Other"));
    }

    [TestMethod]
    public void InATreeOfSeveralFiles_AModuleAnnotationIsInTheDeclarationsSectionOfItsOwnFile()
    {
        var parse = Parse([Procedure("Main", 1, 3), Procedure("Other", 5, 7, OtherFile)],
            Annotated(4, DocStrings.ModuleDescriptionAnnotation, "Of the other part.", OtherFile));

        Assert.AreEqual("Of the other part.", parse.GetModuleDescription());
    }

    [TestMethod]
    public void AnEmptyString_DescribesNothing()
    {
        var parse = Parse([Attribute(1, Tokens.VB_Description, "\"\""), Procedure("Main", 3, 5)],
            Annotated(2, DocStrings.MemberDescriptionAnnotation, ""));

        Assert.IsNull(parse.SyntaxTree!.GetModuleDescription());
        Assert.IsNull(parse.GetModuleDescription());
        Assert.IsNull(parse.GetMemberDescription("Main"));
    }

    [TestMethod]
    public void AnAnnotationWhoseArgumentIsNotAString_DescribesNothing()
    {
        var parse = Parse([Procedure("Main", 3, 5)], Annotated(2, DocStrings.MemberDescriptionAnnotation, new VBLongValue(1)));

        Assert.IsNull(parse.GetMemberDescription("Main"));
    }
}
