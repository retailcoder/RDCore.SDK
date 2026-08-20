using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Abstract;

public record class PrecompilerTriviaNode(SyntaxNodeId Identity, SourceLocation SourceLocation, ImmutableArray<SyntaxNode> Children, string Source)
    : SyntaxNode(Identity, SourceLocation, Children);

public record class CommentTriviaNode(SyntaxNodeId Identity, SourceLocation SourceLocation, string Value)
    : SyntaxNode(Identity, SourceLocation, []);

public record class AnnotationTriviaNode(SyntaxNodeId Identity, SourceLocation SourceLocation, string Name, ImmutableArray<SyntaxNode> Children)
    : SyntaxNode(Identity, SourceLocation, Children);
