using RDCore.SDK.Model.AST.Abstract;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST;

public record struct ModuleNode(Uri SemanticId, ImmutableArray<BoundNode> Children);