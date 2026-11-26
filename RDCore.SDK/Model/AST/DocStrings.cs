using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Values.Intrinsic;
using System.Runtime.CompilerServices;

namespace RDCore.SDK.Model.AST;

/// <summary>
/// The descriptions a parsed module writes, which its symbols carry as <see cref="Symbols.Abstract.SymbolProperties.DocString"/>: the short documentation
/// strings an editor shows for the module, its members and its variables in a hover, a completion list or signature help, as the VBE's Object Browser
/// shows them.
/// </summary>
/// <remarks>
/// A description is written with an attribute (<strong>RD-VBAL §3.1.1.7</strong> <c>VB_Description</c>, and <c>VB_VarDescription</c> for a module
/// variable) or with an annotation (<strong>RD-VBAL §3.0.1.1</strong>): <c>'@ModuleDescription("...")</c> for the module, <c>'@Description("...")</c> for a
/// member, <c>'@VariableDescription("...")</c> for a module variable.
/// <list type="bullet">
/// <item>The attribute is the description when the module writes one, whatever an annotation says: it is what the VBE stores and shows.</item>
/// <item>The annotation is the description when the module writes no attribute for it, as in a block of a file, which has no attributes.</item>
/// </list>
/// An annotation is trivia (<see cref="ModuleParseResult.Trivia"/>), which no node of the tree holds, so these readers take the whole parse. For a file that
/// has blocks, they take the parse of one module (<see cref="Workspace.RDCoreDocument.ParseOf"/>), which holds that module's trivia alone.
/// <para>
/// A description is the text of a string: its quotes are not part of it, and a doubled quote in it is one. An empty string, and an annotation whose first
/// argument is not a string, describe nothing.
/// </para>
/// </remarks>
public static class DocStrings
{
    /// <summary>
    /// The name of the annotation that describes a module: <c>'@ModuleDescription("...")</c>.
    /// </summary>
    public const string ModuleDescriptionAnnotation = "ModuleDescription";

    /// <summary>
    /// The name of the annotation that describes a member: <c>'@Description("...")</c>.
    /// </summary>
    public const string MemberDescriptionAnnotation = "Description";

    /// <summary>
    /// The name of the annotation that describes a module variable: <c>'@VariableDescription("...")</c>.
    /// </summary>
    public const string VariableDescriptionAnnotation = "VariableDescription";

    /// <summary>
    /// The description of the module, or <see langword="null"/> when it writes none.
    /// </summary>
    /// <param name="parse">The module's parse.</param>
    /// <returns>The text of the node <see cref="GetModuleDescriptionNode"/> gives.</returns>
    public static string? GetModuleDescription(this ModuleParseResult parse)
        => parse.GetModuleDescriptionNode() is { } node ? TextOf(node) : null;

    /// <summary>
    /// Where the module writes its description: its own <c>Attribute VB_Description</c> directive (an <see cref="AttributeDirectiveNode"/>,
    /// <see cref="ModuleNodeExtensions.GetModuleDescriptionDirective"/>), else its first description annotation (an <see cref="AnnotationTriviaNode"/>):
    /// a <c>'@ModuleDescription</c>, or, above the header of a block's module, a <c>'@Description</c>. <see langword="null"/> when it writes none.
    /// </summary>
    /// <param name="parse">The module's parse.</param>
    /// <remarks>
    /// A module annotation binds in the module's declarations section (<strong>RD-VBAL §3.0.1.1</strong>): one written after the start of the module's
    /// first procedure does not describe the module. One written on the line right above that procedure still does, because <c>@ModuleDescription</c>
    /// binds to a module only. In a tree that holds the nodes of several files, each annotation is in the declarations section of its own file.
    /// <para>
    /// The module of a block of a file also binds the annotations on the comment lines directly above its header
    /// (<see cref="Annotations.GetHeaderAnnotations"/>). A <c>'@Description</c> there describes the block's module, as one above a member describes the
    /// member; inside the block, it is a member's. Of the description annotations of the module, the first written counts.
    /// </para>
    /// </remarks>
    public static SyntaxNode? GetModuleDescriptionNode(this ModuleParseResult parse)
    {
        if (parse.SyntaxTree is not { } module)
        {
            return null;
        }

        if (module.GetModuleDescriptionDirective() is { } attribute)
        {
            return attribute;
        }

        var header = parse.GetHeaderAnnotations();
        return parse.GetModuleAnnotations().FirstOrDefault(annotation
            => (Is(annotation, ModuleDescriptionAnnotation) || (Is(annotation, MemberDescriptionAnnotation) && header.Contains(annotation)))
                && TextOf(annotation) is not null);
    }

    /// <summary>
    /// The description of a member or a module variable of the module, or <see langword="null"/> when the module writes none for it.
    /// </summary>
    /// <param name="parse">The module's parse.</param>
    /// <param name="memberName">The name of the member or the variable, compared without regard to case.</param>
    /// <returns>
    /// The member's <c>Attribute &lt;member&gt;.VB_Description</c> (<see cref="ModuleNodeExtensions.GetMemberDescription"/>) or the variable's
    /// <c>Attribute &lt;variable&gt;.VB_VarDescription</c> (<see cref="ModuleNodeExtensions.GetVariableDescription"/>); else the <c>'@Description</c>
    /// annotation written immediately above a member of that name, or the <c>'@VariableDescription</c> one immediately above a variable of that name.
    /// The first declaration of that name that has one gives it.
    /// </returns>
    /// <remarks>
    /// A member annotation is immediately above its declaration (<strong>RD-VBAL §3.0.1.1</strong>) when it is on one of the comment lines that end on
    /// the line before the declaration's first line: no blank line, no code and no directive is written between them, and no code before the comment on
    /// its line. Of several such annotations, the first written counts. A variable declared after another in one statement (<c>b</c> of
    /// <c>Dim a, b</c>) has no lines above it of its own.
    /// <para>
    /// In a tree that holds the nodes of several files, an annotation and a declaration match only in one file, as each node's
    /// <see cref="Source.SourceLocation.Uri"/> says.
    /// </para>
    /// </remarks>
    public static string? GetMemberDescription(this ModuleParseResult parse, string memberName)
    {
        if (parse.SyntaxTree is not { } module)
        {
            return null;
        }

        return module.GetMemberDescription(memberName) ?? module.GetVariableDescription(memberName)
            ?? (AnnotatedDeclarationsOf(parse).TryGetValue(memberName, out var text) ? text : null);
    }

    // the annotated description of each procedure and module variable that has one, by name, gathered once for each parse: a symbol is made of each
    // declaration, and reading the trivia anew for each is quadratic in their number. A parse is immutable, so what is gathered of it stays true of it.
    private static readonly ConditionalWeakTable<ModuleParseResult, Dictionary<string, string>> _annotatedDeclarations = [];

    private static Dictionary<string, string> AnnotatedDeclarationsOf(ModuleParseResult parse)
        => _annotatedDeclarations.GetValue(parse, static parse =>
        {
            var described = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (parse.SyntaxTree is not { } module)
            {
                return described;
            }

            // the declarations of each file in the order they are written: the first one of a name that has a description gives it.
            foreach (var file in module.Children.GroupBy(child => child.SourceLocation.Uri))
            {
                foreach (var child in file.OrderBy(child => child.SourceLocation.Range.Start))
                {
                    (string? Name, string? Annotation) declared = child switch
                    {
                        MemberDeclarationNode member => (member.Name, MemberDescriptionAnnotation),
                        VariableDeclarationNode variable => (variable.Name, VariableDescriptionAnnotation),
                        _ => (null, null),
                    };

                    if (declared is ({ } name, { } annotationName) && !described.ContainsKey(name)
                        && parse.GetAnnotations(child).Select(annotation => Is(annotation, annotationName) ? TextOf(annotation) : null)
                            .FirstOrDefault(text => text is not null) is { } text)
                    {
                        described[name] = text;
                    }
                }
            }

            return described;
        });

    private static bool Is(AnnotationTriviaNode annotation, string name) => string.Equals(annotation.Name, name, StringComparison.OrdinalIgnoreCase);

    // the text of a description attribute or annotation; null for one that describes nothing.
    private static string? TextOf(SyntaxNode node)
    {
        var text = node switch
        {
            AttributeDirectiveNode attribute => attribute.Value.Trim() is { Length: >= 2 } value && value[0] == '"' && value[^1] == '"'
                ? VBStringValue.UnquoteLiteralToken(value)
                : null,
            AnnotationTriviaNode { Children: [LiteralExpressionNode { StaticValue: VBStringValue value }, ..] } => value.Value,
            _ => null,
        };

        return string.IsNullOrEmpty(text) ? null : text;
    }
}
