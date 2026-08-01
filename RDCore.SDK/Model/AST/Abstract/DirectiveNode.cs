using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Abstract;


/// <summary>
/// A <see cref="SyntaxNode"/> representing a <em>module directive</em>, which is module metadata that is neither typed, nor executable.
/// </summary>
/// <param name="SemanticId">A semantic <c>Uri</c> uniquely identifying this specific node.</param>
/// <param name="SourceLocation">The document location (<c>Uri</c>+<c>Range</c>) of the bound expression.</param>
public abstract record class DirectiveNode(Uri SemanticId, SourceLocation SourceLocation, ImmutableArray<SyntaxNode> Children) 
    : SyntaxNode(SemanticId, SourceLocation, Children);
