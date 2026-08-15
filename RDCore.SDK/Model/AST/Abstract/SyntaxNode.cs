using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Model.AST.Abstract;

/// <summary>
/// A node in the <em>abstract syntax tree</em> (AST).
/// </summary>
/// <remarks>
/// This is the base abstract node type every AST node is derived from.
/// </remarks>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of this node.</param>
[JsonPolymorphic]
[JsonDerivedType(typeof(AttributeDirectiveNode))]
[JsonDerivedType(typeof(CallStatement))]
[JsonDerivedType(typeof(CaseExpressionStatement))]
[JsonDerivedType(typeof(ConstantDeclarationNode))]
[JsonDerivedType(typeof(DoLoopStatement))]
[JsonDerivedType(typeof(DoLoopUntilStatement))]
[JsonDerivedType(typeof(DoLoopWhileStatement))]
[JsonDerivedType(typeof(DoUntilLoopStatement))]
[JsonDerivedType(typeof(DoWhileLoopStatement))]
[JsonDerivedType(typeof(ElseIfBlockStatement))]
[JsonDerivedType(typeof(ErrorStatement))]
[JsonDerivedType(typeof(ExternalMemberDeclarationNode))]
[JsonDerivedType(typeof(ForEachStatement))]
[JsonDerivedType(typeof(ForStatement))]
[JsonDerivedType(typeof(GoSubStatement))]
[JsonDerivedType(typeof(GoToStatement))]
[JsonDerivedType(typeof(IfBlockStatement))]
[JsonDerivedType(typeof(ImplementsDirectiveNode))]
[JsonDerivedType(typeof(InlineIfStatement))]
[JsonDerivedType(typeof(LineLabelNode))]
[JsonDerivedType(typeof(LineNumberNode))]
[JsonDerivedType(typeof(MemberDeclarationNode))]
[JsonDerivedType(typeof(ModuleNode))]
[JsonDerivedType(typeof(DoLoopStatement))]
[JsonDerivedType(typeof(ModuleOptionDirectiveNode))]
[JsonDerivedType(typeof(OnErrorGoToStatement))]
[JsonDerivedType(typeof(OnErrorResumeStatement))]
[JsonDerivedType(typeof(ParameterDeclarationNode))]
[JsonDerivedType(typeof(ResumeNextStatement))]
[JsonDerivedType(typeof(ResumeStatement))]
[JsonDerivedType(typeof(ReturnStatement))]
[JsonDerivedType(typeof(SelectCaseStatement))]
[JsonDerivedType(typeof(TypeDefDirectiveNode))]
[JsonDerivedType(typeof(VariableDeclarationNode))]
[JsonDerivedType(typeof(VBAsTypeExpression))]
[JsonDerivedType(typeof(VBAttributeExpression))]
[JsonDerivedType(typeof(VBLiteralExpression))]
[JsonDerivedType(typeof(VBMemberAccessOperatorExpression))]
[JsonDerivedType(typeof(VBSimpleNameExpression))]
[JsonDerivedType(typeof(VBTypedDeclarationExpression))]
[JsonDerivedType(typeof(VBBinaryOperatorExpression))]
public abstract record class SyntaxNode(SyntaxNodeId Identity, SourceLocation SourceLocation, ImmutableArray<SyntaxNode> Children)
{
    /// <summary>
    /// A unique identifier encoding the node's position in the syntax tree.
    /// </summary>
    public SyntaxNodeId Identity { get; init; } = Identity;
    /// <summary>
    /// The location of the node in the source document.
    /// </summary>
    public SourceLocation SourceLocation { get; init; } = SourceLocation;
    /// <summary>
    /// The child syntax nodes.
    /// </summary>
    public ImmutableArray<SyntaxNode> Children { get; init; } = Children;
}