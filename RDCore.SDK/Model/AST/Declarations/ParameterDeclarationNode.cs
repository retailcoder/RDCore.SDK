using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Declarations;

/// <summary>
/// An AST node representing a parameter (child of a member node).
/// </summary>
/// <param name="SemanticId">The semantic ID of this AST node.</param>
/// <param name="Location">The source location of this module; the <c>SourceRange</c> is invalid.</param>
/// <param name="Name">The declared identifier name of the member.</param>
/// <param name="ParameterKind">The kind (ByRef/ByVal) of parameter.</param>
/// <param name="IsOptional">An indicator that is <c>true</c> if the parameter is optional.</param>
/// <param name="IsParamArray">An indicator that is <c>true</c> if the parameter is a parameter array.</param>
public record class ParameterDeclarationNode(Uri SemanticId, SourceLocation Location, string Name, ParameterKind ParameterKind = ParameterKind.ImplicitByRef, bool IsOptional = false, bool IsParamArray = false, ImmutableArray<BoundNode> Children = default)
    : BoundNode(SemanticId, Location, Children);
