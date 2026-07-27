using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;

namespace RDCore.SDK.Model.AST.Declarations;

/// <summary>
/// An AST node representing a declared constant (child of either a member or a module node).
/// </summary>
/// <param name="SemanticId">The semantic ID of this AST node.</param>
/// <param name="Location">The source location of this module; the <c>SourceRange</c> is invalid.</param>
/// <param name="Name">The declared identifier name of the member.</param>
/// <param name="ConstKind">The scope kind of constant declaration.</param>
/// <param name="AccessModifier">An access modifier, if one was supplied.</param>
/// <param name="DeclaredTypeExpression">The <em>declared type</em> expression, if one was supplied.</param>
/// <param name="ValueExpression">An expression node that evaluates to the declared value of the constant, if one was supplied.</param>
public record class ConstantDeclarationNode(Uri SemanticId, SourceLocation Location, string Name, ConstKind ConstKind, AccessModifier AccessModifier = AccessModifier.Implicit, BoundExpression? DeclaredTypeExpression = default, BoundExpression? ValueExpression = default)
    : BoundNode(SemanticId, Location, []);
