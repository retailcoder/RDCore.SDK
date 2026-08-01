using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Declarations;

/// <summary>
/// An AST root node, representing an entire module.
/// </summary>
/// <param name="SemanticId">The semantic ID of this AST node.</param>
/// <param name="Location">The source location of this module; the <c>SourceRange</c> is invalid.</param>
/// <param name="Children">An immutable array containing all AST nodes of this module.</param>
/// <param name="ModuleType">The type of module this AST is for.</param>
public record class ModuleNode(Uri SemanticId, SourceLocation Location, ImmutableArray<SyntaxNode> Children, ModuleType ModuleType)
    : SyntaxNode(SemanticId, Location, Children);
