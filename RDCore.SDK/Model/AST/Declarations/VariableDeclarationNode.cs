using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Declarations;

/// <summary>
/// An AST node representing a local variable (child of a member node).
/// </summary>
/// <param name="Identity">A unique identifier for this specific syntax node.</param>
/// <param name="SourceLocation">The source location of this module; the <c>SourceRange</c> is invalid.</param>
/// <param name="Name">The declared identifier name of the member.</param>
/// <param name="AccessModifier">The access modifier, if one was supplied.</param>
/// <param name="TypeHint">The <em>type hint</em> token, if one was supplied.</param>
/// <param name="IsWithEvents"><c>true</c> if the variable is a <c>WithEvents</c> field.</param>
public record class VariableDeclarationNode(Guid Identity, SourceLocation SourceLocation, string Name, ImmutableArray<SyntaxNode> Children, AccessModifier AccessModifier = AccessModifier.Implicit, string? TypeHint = default, bool IsWithEvents = false) 
    : SyntaxNode(Identity, SourceLocation, Children);
