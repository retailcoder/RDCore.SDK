using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Operators;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.Tests.Model.AST;

/// <summary>
/// An expression written back as VBA code, and two expressions compared: what the tree keeps of how an expression is written is written as it was, and
/// two expressions are the same when they are made of the same operations on the same operands.
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §5.6 Expressions")]
public sealed class ExpressionWriterTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");
    private static int _next;

    private static SyntaxNodeId Id() => new(Module.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation Nowhere => new() { Uri = Module, Range = SourceRange.Empty };

    private static SimpleNameExpressionNode Name(string name, string? hint = null, bool bracketed = false) => new(Id(), Nowhere, name, hint, bracketed);

    private static LiteralExpressionNode Literal(VBTypedValue value, string? spelling = null) => new(Id(), Nowhere, value) { Spelling = spelling };

    private static VBUnaryOperatorExpressionNode Unary(string token, ExpressionNode operand) => new(token, Id(), Nowhere, operand);

    private static VBBinaryOperatorExpressionNode Binary(string token, ExpressionNode left, ExpressionNode right) => new(token, Id(), Nowhere, left, right);

    private static IndexExpressionNode Index(ExpressionNode callee, params ExpressionNode[] arguments) => new(Id(), Nowhere, callee, [.. arguments]);

    private static MemberAccessExpressionNode Member(ExpressionNode? owner, SimpleNameExpressionNode member) => new(Id(), Nowhere, owner, member);

    [TestMethod]
    public void AName_IsWrittenWithItsTypeCharacterAndItsBrackets()
    {
        Assert.AreEqual("Left$", ExpressionWriter.Write(Name("Left", "$")));
        Assert.AreEqual("[Some Name]", ExpressionWriter.Write(Name("Some Name", bracketed: true)));
    }

    [TestMethod]
    public void AMemberOfAWithBlock_IsWrittenWithItsDotAlone()
        => Assert.AreEqual(".Enabled", ExpressionWriter.Write(Member(null, Name("Enabled"))));

    [TestMethod]
    public void AChainOfCallsAndMembers_IsWrittenAsItReads()
    {
        var range = Index(Name("Range"), Literal(VBStringValue.FromLiteralToken("\"A1\"")));

        Assert.AreEqual("Range(\"A1\").Locked", ExpressionWriter.Write(Member(range, Name("Locked"))));
    }

    [TestMethod]
    public void ParenthesesAreWritten_WhereverTheyAre()
    {
        var sum = Binary(Tokens.AdditionOp, Name("a"), Name("b"));

        Assert.AreEqual("(a + b) * c", ExpressionWriter.Write(Binary(Tokens.MultiplicationOp, Unary(OperatorSymbolNames.UnaryArithmeticPrecedenceOp, sum), Name("c"))));
        Assert.AreEqual("Foo((x))", ExpressionWriter.Write(Index(Name("Foo"), Unary(OperatorSymbolNames.UnaryLetCoerceOp, Name("x")))));
        Assert.AreEqual("Foo(((x)))", ExpressionWriter.Write(Index(Name("Foo"), Unary(OperatorSymbolNames.UnaryLetCoerceOp, Unary(OperatorSymbolNames.UnaryArithmeticPrecedenceOp, Name("x"))))));
    }

    [TestMethod]
    public void UnaryOperators_AreWrittenBeforeTheirOperand()
    {
        Assert.AreEqual("-x", ExpressionWriter.Write(Unary(Tokens.NegationOp, Name("x"))));
        Assert.AreEqual("Not IsMissing(p)", ExpressionWriter.Write(Unary(Tokens.LogicalNotOp, Index(Name("IsMissing"), Name("p")))));
    }

    [TestMethod]
    public void ArgumentsThatAreNamedOrOmitted_AreWrittenAsTheyAre()
    {
        var named = new NamedArgumentNode(Id(), Nowhere, "Arg", Literal(new VBIntegerValue(1), "1"));

        Assert.AreEqual("Name(Arg:=1)", ExpressionWriter.Write(Index(Name("Name"), named)));
        Assert.AreEqual("Foo(, b)", ExpressionWriter.Write(Index(Name("Foo"), new MissingArgumentNode(Id(), Nowhere), Name("b"))));
        Assert.AreEqual("Foo()", ExpressionWriter.Write(Index(Name("Foo"), new MissingArgumentNode(Id(), Nowhere))));
    }

    [TestMethod]
    public void ALiteral_IsWrittenAsItIsSpelled()
        => Assert.AreEqual("&HFF", ExpressionWriter.Write(Literal(new VBIntegerValue(255), "&HFF")));

    [TestMethod]
    public void ALiteralOfNoSpelling_IsWrittenWithTheTypeCharacterThatKeepsItsType()
    {
        Assert.AreEqual("1#", ExpressionWriter.Write(Literal(new VBDoubleValue(1))));
        Assert.AreEqual("5&", ExpressionWriter.Write(Literal(new VBLongValue(5))));
        Assert.AreEqual("40000", ExpressionWriter.Write(Literal(new VBLongValue(40000))));
        Assert.AreEqual("\"say \"\"hi\"\"\"", ExpressionWriter.Write(Literal(VBStringValue.FromLiteralToken("\"say \"\"hi\"\"\""))));
        Assert.AreEqual("True", ExpressionWriter.Write(Literal(VBBooleanValue.True)));
        Assert.AreEqual("Nothing", ExpressionWriter.Write(Literal(VBObjectValue.Nothing)));
    }

    [TestMethod]
    public void AnExpressionThePartsOfWhichAreNotAllWritten_IsNotWritten()
    {
        var unbuilt = new UnbuiltExpressionTriviaNode(Id(), Nowhere, "1 +", []);

        Assert.IsNull(ExpressionWriter.Write(Binary(Tokens.AdditionOp, Name("a"), unbuilt)));
    }

    [TestMethod]
    public void TwoNames_AreTheSameWhateverTheirCase()
        => Assert.IsTrue(ExpressionEquivalence.AreEquivalent(Index(Member(Name("A"), Name("B")), Literal(new VBIntegerValue(1), "1")),
            Index(Member(Name("a"), Name("b")), Literal(new VBIntegerValue(1), "1"))));

    [TestMethod]
    public void TwoLiteralsOfOneValue_AreTheSameWhateverTheirSpelling()
        => Assert.IsTrue(ExpressionEquivalence.AreEquivalent(Literal(new VBIntegerValue(255), "&HFF"), Literal(new VBIntegerValue(255), "255")));

    [TestMethod]
    public void AnExpressionInParentheses_IsNotTheExpression()
        => Assert.IsFalse(ExpressionEquivalence.AreEquivalent(Unary(OperatorSymbolNames.UnaryLetCoerceOp, Name("x")), Name("x")));

    [TestMethod]
    public void TheTwoParentheses_AreNotTheSameOperation()
        => Assert.IsFalse(ExpressionEquivalence.AreEquivalent(Unary(OperatorSymbolNames.UnaryLetCoerceOp, Name("x")),
            Unary(OperatorSymbolNames.UnaryArithmeticPrecedenceOp, Name("x"))));

    [TestMethod]
    public void ANameWithATypeCharacter_IsNotTheNameWithout()
        => Assert.IsFalse(ExpressionEquivalence.AreEquivalent(Name("s", "$"), Name("s")));

    [TestMethod]
    public void AMemberOfAWithBlock_IsNotTheMemberOfANamedObject()
        => Assert.IsFalse(ExpressionEquivalence.AreEquivalent(Member(null, Name("Enabled")), Member(Name("b"), Name("Enabled"))));

    [TestMethod]
    public void TwoLiteralsOfTwoTypes_AreNotTheSame()
        => Assert.IsFalse(ExpressionEquivalence.AreEquivalent(Literal(new VBIntegerValue(1)), Literal(new VBLongValue(1))));
}
