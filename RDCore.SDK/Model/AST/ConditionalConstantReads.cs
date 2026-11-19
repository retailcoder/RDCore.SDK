using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Statements;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST;

/// <summary>
/// A conditional-compilation constant that a directive reads (<strong>MS-VBAL §5.6.10</strong>, the conditional compilation binding context): a name in
/// the condition of a <c>#If</c> or a <c>#ElseIf</c>, or in the value of a <c>#Const</c>.
/// </summary>
/// <param name="Name">The name, where it is written.</param>
/// <param name="IsWholeExpression">
/// Whether the name is the whole condition, or the whole value: what replaces it needs no parentheses then, where it does as the operand of an operator.
/// </param>
public sealed record class ConditionalConstantRead(PrecompilerNameExpressionNode Name, bool IsWholeExpression);

/// <summary>
/// The conditional-compilation constants that the directives of a module read and declare, off its <see cref="ModuleParseResult.PrecompilerTrivia"/>.
/// </summary>
/// <remarks>
/// <strong>MS-VBAL §3.4.1</strong>: every <c>#Const</c> of a module is processed, those of an excluded block too, and every directive is read here, in a
/// live branch or not. The name a <c>#Const</c> declares is not a read of it.
/// </remarks>
public static class ConditionalConstantReads
{
    /// <summary>
    /// Each name the directives of a module read, in the order they are written.
    /// </summary>
    /// <param name="precompilerTrivia">A module's <see cref="ModuleParseResult.PrecompilerTrivia"/>.</param>
    public static IReadOnlyList<ConditionalConstantRead> Of(ImmutableArray<SyntaxNode> precompilerTrivia)
    {
        if (precompilerTrivia.IsDefaultOrEmpty)
        {
            return [];
        }

        var reads = new List<ConditionalConstantRead>();
        foreach (var expression in ExpressionsOf(Nodes(precompilerTrivia)))
        {
            reads.AddRange(NamesIn(expression).Select(name => new ConditionalConstantRead(name, ReferenceEquals(name, expression))));
        }

        return [.. reads.DistinctBy(read => read.Name.Identity)
            .OrderBy(read => read.Name.Location.Range.Start.Line).ThenBy(read => read.Name.Location.Range.Start.Character)];
    }

    /// <summary>
    /// The names the <c>#Const</c> directives of a module declare.
    /// </summary>
    /// <param name="precompilerTrivia">A module's <see cref="ModuleParseResult.PrecompilerTrivia"/>.</param>
    /// <returns>The names, compared without regard to case, as a directive reads them.</returns>
    public static IReadOnlySet<string> DeclaredBy(ImmutableArray<SyntaxNode> precompilerTrivia)
        => (precompilerTrivia.IsDefaultOrEmpty ? [] : Nodes(precompilerTrivia))
            .OfType<PrecompilerConstantDeclarationNode>()
            .Select(declaration => declaration.Children.FirstOrDefault())
            .OfType<PrecompilerNameExpressionNode>()
            .Select(name => name.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<SyntaxNode> Nodes(ImmutableArray<SyntaxNode> precompilerTrivia)
        => precompilerTrivia.SelectMany(node => node.Descendants().Prepend(node));

    // the condition of each #If and #ElseIf, and the value of each #Const.
    private static IEnumerable<ExpressionNode> ExpressionsOf(IEnumerable<SyntaxNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case PrecompilerConstantDeclarationNode { Children: [PrecompilerNameExpressionNode, ExpressionNode value, ..] }:
                    yield return value;
                    break;
                case PrecompilerInlineIfStatementNode or PrecompilerElseIfBlockStatementNode
                    when node.Children.OfType<ExpressionNode>().FirstOrDefault() is { } condition:
                    yield return condition;
                    break;
            }
        }
    }

    private static IEnumerable<PrecompilerNameExpressionNode> NamesIn(ExpressionNode expression)
        => expression.Descendants().Prepend(expression).OfType<PrecompilerNameExpressionNode>();
}
