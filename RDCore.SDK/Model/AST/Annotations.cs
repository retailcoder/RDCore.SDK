using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Values.Intrinsic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace RDCore.SDK.Model.AST;

/// <summary>
/// The comment annotations of a parsed module (<strong>RD-VBAL §3.0.1.1</strong>), bound to the module and to its declarations by the binding rules of
/// annotations that target a module or a member.
/// </summary>
/// <remarks>
/// An annotation is trivia (<see cref="ModuleParseResult.Trivia"/>), which no node of the tree holds, so these readers take the whole parse. For a file that
/// has blocks, they take the parse of one module (<see cref="Workspace.RDCoreDocument.ParseOf"/>), which holds that module's trivia alone.
/// <para>
/// Which annotations a consumer reads, and what they mean, is the consumer's: the core reads <c>'@Description</c> (<see cref="DocStrings"/>), and an
/// extension or a framework reads annotations of its own. The consumer knows each annotation's target, so it reads a module annotation of the module and a
/// member annotation of a declaration. One annotation can be a candidate for both: written on the last line of the declarations section, right above the
/// first procedure, it is in the declarations section and immediately above that procedure.
/// </para>
/// </remarks>
public static class Annotations
{
    /// <summary>
    /// The annotations written in the module's declarations section, in the order they are written: the annotations that can bind to the module.
    /// </summary>
    /// <param name="parse">The module's parse.</param>
    /// <returns>
    /// Each annotation that no procedure of its file starts at or before; empty when the module has no tree. In a tree that holds the nodes of several
    /// files, each annotation is in the declarations section of its own file, as each node's <see cref="Source.SourceLocation.Uri"/> says.
    /// </returns>
    /// <remarks>
    /// The module of a block of a file (<see cref="BlockModule"/>) has module annotations outside it too: those on the comment lines directly above its
    /// header (<see cref="GetHeaderAnnotations"/>), which its parse holds (<see cref="Workspace.RDCoreDocument.ParseOf"/>). They come first, as they are
    /// written first, and they are never a member's.
    /// </remarks>
    public static ImmutableArray<AnnotationTriviaNode> GetModuleAnnotations(this ModuleParseResult parse)
    {
        if (parse.SyntaxTree is not { } module)
        {
            return [];
        }

        var procedures = module.Children.OfType<MemberDeclarationNode>().Where(IsProcedure).Select(procedure => procedure.SourceLocation).ToArray();
        return [.. parse.Trivia.OfType<AnnotationTriviaNode>().Where(annotation => !procedures.Any(procedure => procedure.Uri == annotation.SourceLocation.Uri
            && procedure.Range.Start <= annotation.SourceLocation.Range.Start))];
    }

    /// <summary>
    /// The annotations written on the comment lines directly above the header of a block's module (<c>Class Name</c>, <c>Module Name</c>), in the order
    /// they are written: the module annotations of the block that are outside it.
    /// </summary>
    /// <param name="parse">The parse of the block's module (<see cref="Workspace.RDCoreDocument.ParseOf"/>), which holds those comment lines.</param>
    /// <returns>
    /// The annotations of the module's file written before the start of the module's tree; empty for a module that is not a block's, whose tree starts where
    /// its file starts, and when the module has no tree.
    /// </returns>
    public static ImmutableArray<AnnotationTriviaNode> GetHeaderAnnotations(this ModuleParseResult parse)
    {
        if (parse.SyntaxTree is not { } module)
        {
            return [];
        }

        var start = module.SourceLocation;
        return [.. parse.Trivia.OfType<AnnotationTriviaNode>()
            .Where(annotation => annotation.SourceLocation.Uri == start.Uri && annotation.SourceLocation.Range.Start < start.Range.Start)];
    }

    /// <summary>
    /// The annotations written immediately above a declaration of the module, in the order they are written: the annotations that can bind to it as a member.
    /// </summary>
    /// <param name="parse">The module's parse.</param>
    /// <param name="declaration">
    /// A declaration of the module's tree: one of the children of its <see cref="ModuleNode"/>, such as a <see cref="MemberDeclarationNode"/> or a
    /// <see cref="VariableDeclarationNode"/>.
    /// </param>
    /// <returns>
    /// The annotations on the comment lines that end on the line before the declaration's first line, with no blank line, no code and no directive written
    /// between them, and no code before the comment on its line. Empty for a node that is not a child of the module's tree, and for a variable declared after
    /// another in one statement (<c>b</c> of <c>Dim a, b</c>), which has no lines above it of its own.
    /// </returns>
    /// <remarks>
    /// In a tree that holds the nodes of several files, an annotation and a declaration match only in one file, as each node's
    /// <see cref="Source.SourceLocation.Uri"/> says.
    /// </remarks>
    public static ImmutableArray<AnnotationTriviaNode> GetAnnotations(this ModuleParseResult parse, SyntaxNode declaration)
        => AnnotationsAbove(parse).TryGetValue(declaration, out var annotations) ? annotations : [];

    /// <summary>
    /// The value of a string literal argument of the annotation: <c>"/orders"</c> of <c>'@HttpGet("/orders")</c>.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="index">The zero-based position of the argument in the annotation's argument list.</param>
    /// <returns>
    /// The string the literal writes; <see langword="null"/> when the annotation has no argument at that position, or when the argument is not a string
    /// literal.
    /// </returns>
    public static string? GetStringArgument(this AnnotationTriviaNode annotation, int index = 0)
        => index >= 0 && index < annotation.Children.Length && annotation.Children[index] is LiteralExpressionNode { StaticValue: VBStringValue value }
            ? value.Value
            : null;

    // the annotations above each child of the module, gathered once for each parse: a consumer reads them for each declaration, and reading the trivia anew
    // for each is quadratic in their number. A parse is immutable, so what is gathered of it stays true of it. The children are keyed by reference: a node is
    // a record, and two declarations written alike are still two.
    private static readonly ConditionalWeakTable<ModuleParseResult, Dictionary<SyntaxNode, ImmutableArray<AnnotationTriviaNode>>> _above = [];

    private static Dictionary<SyntaxNode, ImmutableArray<AnnotationTriviaNode>> AnnotationsAbove(ModuleParseResult parse)
        => _above.GetValue(parse, static parse =>
        {
            var above = new Dictionary<SyntaxNode, ImmutableArray<AnnotationTriviaNode>>(ReferenceEqualityComparer.Instance);
            if (parse.SyntaxTree is not { } module)
            {
                return above;
            }

            var comments = new HashSet<(Uri File, int Line)>();
            foreach (var comment in parse.Trivia.OfType<CommentTriviaNode>())
            {
                var range = comment.SourceLocation.Range;
                for (var line = range.Start.Line; line <= range.End.Line; line++)
                {
                    comments.Add((comment.SourceLocation.Uri, line));
                }
            }

            var annotations = parse.Trivia.OfType<AnnotationTriviaNode>()
                .ToLookup(annotation => (annotation.SourceLocation.Uri, annotation.SourceLocation.Range.Start.Line));

            // the children of each file in the order they are written, and the last line of the code before each one.
            foreach (var file in module.Children.GroupBy(child => child.SourceLocation.Uri))
            {
                var codeEnds = -1;
                foreach (var child in file.OrderBy(child => child.SourceLocation.Range.Start))
                {
                    var first = child.SourceLocation.Range.Start.Line;
                    while (first - 1 > codeEnds && comments.Contains((file.Key, first - 1)))
                    {
                        first--;
                    }

                    var bound = Enumerable.Range(first, child.SourceLocation.Range.Start.Line - first)
                        .SelectMany(line => annotations[(file.Key, line)].OrderBy(annotation => annotation.SourceLocation.Range.Start))
                        .ToImmutableArray();
                    if (!bound.IsEmpty)
                    {
                        above[child] = bound;
                    }

                    codeEnds = Math.Max(codeEnds, child.SourceLocation.Range.End.Line);
                }
            }

            return above;
        });

    private static bool IsProcedure(MemberDeclarationNode member)
        => member.MemberKind is MemberKind.Procedure or MemberKind.Function or MemberKind.PropertyGet or MemberKind.PropertyLet or MemberKind.PropertySet;
}
