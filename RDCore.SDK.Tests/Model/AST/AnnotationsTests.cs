using RDCore.SDK.Model;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.Tests.Model.AST;

/// <summary>
/// The annotations of a module bound to the module and to its declarations (<see cref="Annotations"/>), by the binding rules of RD-VBAL §3.0.1.1. The trees
/// are built as the parser builds them: each annotation inside the comment line that holds it.
/// </summary>
[TestClass]
public sealed class AnnotationsTests
{
    private static readonly Uri File = new("file:///c:/ws/Module1.cls");
    private static readonly Uri OtherFile = new("file:///c:/ws/Module1.Part.cls");
    private static int _next;

    private static SyntaxNodeId Id() => new(File.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation Lines(int first, int last, Uri? file = null, int startCharacter = 0, int endCharacter = 40)
        => new() { Uri = file ?? File, Range = new SourceRange(first, startCharacter, last, endCharacter) };

    private static MemberDeclarationNode Procedure(string name, int first, int last, Uri? file = null, MemberKind kind = MemberKind.Function)
        => new(Id(), Lines(first, last, file), [], name, kind);

    private static VariableDeclarationNode Variable(string name, int line, int startCharacter = 0, int endCharacter = 40)
        => new(Id(), Lines(line, line, startCharacter: startCharacter, endCharacter: endCharacter), name, []);

    private static AnnotationTriviaNode Annotation(int line, string name, VBTypedValue? argument = null, Uri? file = null, int startCharacter = 0)
    {
        var location = Lines(line, line, file, startCharacter);
        SyntaxNode[] arguments = argument is null ? [] : [new LiteralExpressionNode(Id(), location, argument)];
        return new AnnotationTriviaNode(Id(), location, name, [.. arguments]);
    }

    // a comment line, as the parser keeps it: the comment, then each annotation it holds, inside its range.
    private static SyntaxNode[] Comment(int line, params AnnotationTriviaNode[] annotations)
        => Comment(line, null, 0, annotations);

    private static SyntaxNode[] Comment(int line, Uri? file, int startCharacter, params AnnotationTriviaNode[] annotations)
        => [new CommentTriviaNode(Id(), Lines(line, line, file, startCharacter), "'@..."), .. annotations];

    private static ModuleParseResult Parse(SyntaxNode[] children, params SyntaxNode[][] trivia)
        => new() { SyntaxTree = new ModuleNode(Id(), Lines(0, 30), [.. children]), Trivia = [.. trivia.SelectMany(nodes => nodes)] };

    private static string[] Names(IEnumerable<AnnotationTriviaNode> annotations) => [.. annotations.Select(annotation => annotation.Name)];

    [TestMethod]
    public void TheModuleAnnotations_AreTheOnesInTheDeclarationsSection_InTheOrderWritten()
    {
        var parse = Parse([Procedure("Orders", 5, 7)],
            Comment(0, Annotation(0, "Controller", new VBStringValue("/orders")), Annotation(0, "Folder", null, null, 20)),
            Comment(2, Annotation(2, "ModuleDescription")),
            Comment(6, Annotation(6, "Ignore")));

        CollectionAssert.AreEqual(new[] { "Controller", "Folder", "ModuleDescription" }, Names(parse.GetModuleAnnotations()));
    }

    [TestMethod]
    public void AnAnnotationRightAboveTheFirstProcedure_IsAModuleAnnotation_AndAnAnnotationOfThatProcedure()
    {
        var orders = Procedure("Orders", 3, 5);
        var parse = Parse([orders], Comment(2, Annotation(2, "HttpGet")));

        CollectionAssert.AreEqual(new[] { "HttpGet" }, Names(parse.GetModuleAnnotations()));
        CollectionAssert.AreEqual(new[] { "HttpGet" }, Names(parse.GetAnnotations(orders)));
    }

    [TestMethod]
    public void InATreeOfSeveralFiles_EachFileHasADeclarationsSectionOfItsOwn()
    {
        // the first file's procedure starts before the other file's declarations section ends: it does not end that section.
        var parse = Parse([Procedure("Orders", 0, 1), Procedure("Items", 4, 6, OtherFile)],
            Comment(2, OtherFile, 0, Annotation(2, "Controller", file: OtherFile)),
            Comment(5, OtherFile, 0, Annotation(5, "Ignore", file: OtherFile)));

        CollectionAssert.AreEqual(new[] { "Controller" }, Names(parse.GetModuleAnnotations()));
    }

    [TestMethod]
    public void TheAnnotationsOfADeclaration_AreTheOnesOnTheCommentLinesRightAboveIt_InTheOrderWritten()
    {
        var orders = Procedure("Orders", 4, 6);
        var parse = Parse([Variable("count", 0), orders],
            Comment(1, Annotation(1, "Unrelated")),
            Comment(2, Annotation(2, "HttpGet", new VBStringValue("/orders")), Annotation(2, "Authorize", null, null, 30)),
            Comment(3, Annotation(3, "Description", new VBStringValue("Lists the orders."))));

        CollectionAssert.AreEqual(new[] { "Unrelated", "HttpGet", "Authorize", "Description" }, Names(parse.GetAnnotations(orders)));
    }

    [TestMethod]
    public void ABlankLine_EndsTheCommentLinesAboveADeclaration()
    {
        var orders = Procedure("Orders", 4, 6);
        var parse = Parse([orders], Comment(1, Annotation(1, "Detached")), Comment(3, Annotation(3, "HttpGet")));

        CollectionAssert.AreEqual(new[] { "HttpGet" }, Names(parse.GetAnnotations(orders)));
    }

    [TestMethod]
    public void AnAnnotationAfterCodeOnItsLine_IsNotAboveTheNextDeclaration()
    {
        var orders = Procedure("Orders", 3, 5);
        var parse = Parse([Variable("count", 2, endCharacter: 15), orders], Comment(2, null, 16, Annotation(2, "HttpGet", startCharacter: 16)));

        Assert.IsEmpty(parse.GetAnnotations(orders));
    }

    [TestMethod]
    public void TheCodeOfTheProcedureAbove_EndsTheCommentLinesAboveADeclaration()
    {
        var orders = Procedure("Orders", 0, 3);
        var items = Procedure("Items", 5, 7);
        var parse = Parse([orders, items], Comment(2, Annotation(2, "InsideOrders")), Comment(4, Annotation(4, "HttpPost")));

        CollectionAssert.AreEqual(new[] { "HttpPost" }, Names(parse.GetAnnotations(items)));
        Assert.IsEmpty(parse.GetAnnotations(orders));
    }

    [TestMethod]
    public void AVariableDeclaredAfterAnotherInOneStatement_HasNoAnnotationsOfItsOwn()
    {
        var first = Variable("a", 2, endCharacter: 9);
        var second = Variable("b", 2, startCharacter: 11);
        var parse = Parse([first, second], Comment(1, Annotation(1, "VariableDescription", new VBStringValue("The count."))));

        CollectionAssert.AreEqual(new[] { "VariableDescription" }, Names(parse.GetAnnotations(first)));
        Assert.IsEmpty(parse.GetAnnotations(second));
    }

    [TestMethod]
    public void AnAnnotationInAnotherFile_IsNotAboveADeclaration()
    {
        var orders = Procedure("Orders", 3, 5);
        var parse = Parse([orders], Comment(2, OtherFile, 0, Annotation(2, "HttpGet", file: OtherFile)));

        Assert.IsEmpty(parse.GetAnnotations(orders));
    }

    [TestMethod]
    public void ANodeThatIsNotAChildOfTheModule_HasNoAnnotations()
    {
        var parse = Parse([Procedure("Orders", 3, 5)], Comment(2, Annotation(2, "HttpGet")));

        Assert.IsEmpty(parse.GetAnnotations(Procedure("Orders", 3, 5)));
    }

    [TestMethod]
    public void ABlocksModule_HasTheAnnotationsAboveItsHeader_FirstOfItsModuleAnnotations_AndNoneIsAMembers()
    {
        // 0-1 above the header; 2 the header, which the block's parse does not hold; 3 the block's declarations section; 4-6 a procedure.
        var hello = Procedure("Hello", 4, 6);
        var parse = new ModuleParseResult
        {
            SyntaxTree = new ModuleNode(Id(), Lines(3, 7), [hello]),
            Trivia =
            [
                .. Comment(0, Annotation(0, "Controller", new VBStringValue("api"))),
                .. Comment(1, Annotation(1, "Folder")),
                .. Comment(3, Annotation(3, "ModuleDescription")),
            ],
        };

        CollectionAssert.AreEqual(new[] { "Controller", "Folder", "ModuleDescription" }, Names(parse.GetModuleAnnotations()));
        CollectionAssert.AreEqual(new[] { "Controller", "Folder" }, Names(parse.GetHeaderAnnotations()));
        CollectionAssert.AreEqual(new[] { "ModuleDescription" }, Names(parse.GetAnnotations(hello)));
    }

    [TestMethod]
    public void TheFilesOwnModule_HasNoHeaderAnnotations()
    {
        var parse = Parse([Procedure("Hello", 3, 5)], Comment(0, Annotation(0, "Controller")), Comment(2, Annotation(2, "HttpGet")));

        Assert.IsEmpty(parse.GetHeaderAnnotations());
    }

    [TestMethod]
    public void AStringArgument_IsTheStringTheLiteralWrites()
    {
        var annotation = new AnnotationTriviaNode(Id(), Lines(0, 0), "HttpGet",
            [new LiteralExpressionNode(Id(), Lines(0, 0), new VBStringValue("/orders/{id}")),
                new LiteralExpressionNode(Id(), Lines(0, 0), new VBLongValue(42))]);

        Assert.AreEqual("/orders/{id}", annotation.GetStringArgument());
        Assert.IsNull(annotation.GetStringArgument(1), "a Long literal is not a string");
        Assert.IsNull(annotation.GetStringArgument(2), "there is no third argument");
        Assert.IsNull(annotation.GetStringArgument(-1));
    }

    [TestMethod]
    public void TheTextOfAnArgument_IsTheNameAsWritten_OrTheStringTheLiteralWrites()
    {
        var annotation = new AnnotationTriviaNode(Id(), Lines(0, 0), "Ignore",
            [new SimpleNameExpressionNode(Id(), Lines(0, 0), "ProcedureNotUsed"),
                new LiteralExpressionNode(Id(), Lines(0, 0), new VBStringValue("RDC00315")),
                new LiteralExpressionNode(Id(), Lines(0, 0), new VBLongValue(42))]);

        Assert.AreEqual("ProcedureNotUsed", annotation.GetArgumentText());
        Assert.AreEqual("RDC00315", annotation.GetArgumentText(1));
        Assert.IsNull(annotation.GetArgumentText(2), "a Long literal is neither a name nor a string");
        Assert.IsNull(annotation.GetArgumentText(3), "there is no fourth argument");
        Assert.IsNull(annotation.GetArgumentText(-1));
    }

    [TestMethod]
    [DataRow("IgnoreModule", true, DisplayName = "as written")]
    [DataRow("ignoremodule", true, DisplayName = "in another case")]
    [DataRow("Ignore", false, DisplayName = "a name it starts with")]
    [DataRow("IgnoreModules", false, DisplayName = "a longer name")]
    public void AnAnnotation_IsNamed_WhateverTheCaseOfTheName(string name, bool expected)
        => Assert.AreEqual(expected, Annotation(0, "IgnoreModule").IsNamed(name));
}
