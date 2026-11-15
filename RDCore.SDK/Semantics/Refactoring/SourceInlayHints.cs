using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Completion;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Server.Configuration;

namespace RDCore.SDK.Semantics.Refactoring;

/// <summary>
/// A place of the code an inlay hint is shown at, which a setting turns on or off (<see cref="SdkInlayHintOptions"/>).
/// </summary>
public enum InlayHintSite
{
    /// <summary>
    /// Before a positional argument, the name of the parameter it is given to: <c>Wheel.MoveTo [X:]10, [Y:]20</c> (<strong>MS-VBAL §5.6.13.1</strong>).
    /// </summary>
    Arguments,

    /// <summary>
    /// After the statement that closes a procedure, the procedure's name: <c>End Sub [DoSomething]</c>.
    /// </summary>
    BlockEnds,

    /// <summary>
    /// After a <c>Next</c> that names no variable, the loop's: <c>Next [i]</c> (<strong>MS-VBAL §5.4.2.3</strong>, <strong>§5.4.2.4</strong>).
    /// </summary>
    NextVariables,

    /// <summary>
    /// Before a procedure declared without an access modifier, the one it has: <c>[Public] Sub Work</c> (<strong>MS-VBAL §5.3.1.1</strong>). Before the name of
    /// a parameter declared without <c>ByVal</c> or <c>ByRef</c>, how it is passed: <c>([ByRef]Total As Long)</c> (<strong>MS-VBAL §5.3.1.5</strong>).
    /// </summary>
    ImplicitModifiers,

    /// <summary>
    /// After the name of a variable or a parameter declared without a type, the one it has: <c>Dim First [As Variant]</c> (<strong>MS-VBAL §5.2.3.1.5</strong>).
    /// </summary>
    ImplicitTypes,
}

/// <summary>
/// An inlay hint of a module: what it says, in square brackets, and the place of the module's source it is shown at, before the character there.
/// </summary>
/// <param name="Position">Where the hint is shown, in the module's source.</param>
/// <param name="Label">What the hint says, in its brackets: <c>[X:]</c>, <c>[DoSomething]</c>, <c>[As Variant]</c>.</param>
/// <param name="Site">The kind of place it is shown at.</param>
/// <param name="PaddingLeft">Whether a space is shown before it: a hint after a word is apart from it.</param>
/// <param name="PaddingRight">Whether a space is shown after it: a hint before a keyword is apart from it.</param>
public readonly record struct SourceInlayHint(SourcePosition Position, string Label, InlayHintSite Site, bool PaddingLeft = false, bool PaddingRight = false)
{
    /// <summary>
    /// The hint as an LSP inlay hint (<strong>LSP 3.17</strong> §Inlay Hint Request), at a position of the client's document. It is complete: there is
    /// nothing to resolve.
    /// </summary>
    /// <remarks>
    /// The hint of an argument is of kind <see cref="InlayHintKind.Parameter"/>, and that of a type of kind <see cref="InlayHintKind.Type"/>; the others are of
    /// no kind.
    /// </remarks>
    /// <param name="position">Where the hint is in the client's document, which for a document that is part of a module is not where it is in the module.</param>
    public InlayHint ToInlayHint(Position position) => new()
    {
        Position = position,
        Label = new StringOrInlayHintLabelParts(Label),
        Kind = Site switch
        {
            InlayHintSite.Arguments => InlayHintKind.Parameter,
            InlayHintSite.ImplicitTypes => InlayHintKind.Type,
            _ => null,
        },
        PaddingLeft = PaddingLeft,
        PaddingRight = PaddingRight,
    };
}

/// <summary>
/// The inlay hints of a module: what its code means without saying it, each in square brackets, at the place it describes - never at the end of a line it
/// does not describe, where it would read like code.
/// </summary>
public static class SourceInlayHints
{
    /// <summary>
    /// The hints of the places of a range of a module that the settings show (<see cref="SdkInlayHintOptions"/>), in the order of their positions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An argument's hint is the name of the parameter its position gives it to, before it, for each positional argument up to the first named one
    /// (<strong>MS-VBAL §5.6.13.1</strong>): none for an omitted argument, for an argument that is a name and the parameter's, nor for an argument there is no
    /// parameter for. A <c>ParamArray</c> takes the first of the arguments left over, which is shown its name, and the others are not. A call is hinted when
    /// it overlaps the range.
    /// </para>
    /// <para>
    /// A procedure without an access modifier is <c>[Public]</c> (<strong>MS-VBAL §5.3.1.1</strong>), and the statement that closes it shows its name. A
    /// parameter written without <c>ByVal</c> or <c>ByRef</c> is <c>[ByRef]</c> (<strong>MS-VBAL §5.3.1.5</strong>), or <c>[ByVal]</c> for the value of a
    /// <c>Property Let</c> or <c>Set</c>, which is passed by value (<see cref="ParameterKind.ImplicitByVal"/>); a <c>ParamArray</c> is neither. Each header
    /// of a procedure written once in each branch of a <c>#If</c> block has its own (<see cref="MemberDeclarationNode.HeaderAlternatives"/>).
    /// </para>
    /// <para>
    /// A variable or a parameter declared without a type is a <c>Variant</c> unless a <c>Def</c><em>type</em> statement covers its name
    /// (<strong>MS-VBAL §5.2.3.1.5</strong>): only a <c>Variant</c> that nothing says is hinted. An array parameter (<c>Items()</c>) is not: there is nowhere
    /// to show the type where it would be written.
    /// </para>
    /// </remarks>
    /// <param name="module">The module.</param>
    /// <param name="range">The range of the module's source the hints are asked of.</param>
    /// <param name="sites">Which places are hinted.</param>
    /// <param name="signatureOf">
    /// The signature of what a call calls, given the expression it calls (<c>Wheel.MoveTo</c>); <see langword="null"/> when it is not known, and the call's
    /// arguments are not hinted. What a name binds to is the platform's to say.
    /// </param>
    public static IReadOnlyList<SourceInlayHint> In(ModuleNode module, SourceRange range, SdkInlayHintOptions sites, Func<ExpressionNode, SignatureDescription?> signatureOf)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(signatureOf);

        var directives = module.Children.OfType<TypeDefDirectiveNode>().ToList();
        var hints = new List<SourceInlayHint>();
        foreach (var node in NodesOf(module))
        {
            switch (node)
            {
                case IndexExpressionNode index when sites.Arguments && Intersects(index.Location.Range, range):
                    AddArguments(hints, index.Callee, index.Arguments, signatureOf);
                    break;
                case CallStatementNode call when sites.Arguments && Intersects(call.SourceLocation.Range, range):
                    AddArguments(hints, call.Callee, call.Arguments, signatureOf);
                    break;
                case MemberDeclarationNode member and not ExternalMemberDeclarationNode when IsProcedure(member.MemberKind):
                    if (sites.ImplicitModifiers && member.AccessModifier == AccessModifier.Implicit)
                    {
                        hints.Add(new SourceInlayHint(member.SourceLocation.Range.Start, "[Public]", InlayHintSite.ImplicitModifiers, PaddingRight: true));
                    }

                    if (sites.BlockEnds && member.EndRange is { } end)
                    {
                        hints.Add(new SourceInlayHint(end.End, $"[{member.Name}]", InlayHintSite.BlockEnds, PaddingLeft: true));
                    }

                    break;
                case ParameterDeclarationNode parameter:
                    if (sites.ImplicitModifiers && parameter is { PassingRange: null, IsParamArray: false, NameRange: { } nameRange }
                        && parameter.ParameterKind is ParameterKind.ImplicitByRef or ParameterKind.ImplicitByVal)
                    {
                        hints.Add(new SourceInlayHint(nameRange.Start, parameter.ParameterKind == ParameterKind.ImplicitByVal ? "[ByVal]" : "[ByRef]",
                            InlayHintSite.ImplicitModifiers));
                    }

                    if (sites.ImplicitTypes && parameter is { IsArray: false, NameRange: { } typed } && IsImplicitVariant(parameter, parameter.Name, parameter.TypeHint, directives))
                    {
                        hints.Add(new SourceInlayHint(typed.End, $"[As {VBTypeNames.VBVariant}]", InlayHintSite.ImplicitTypes, PaddingLeft: true));
                    }

                    break;
                case VariableDeclarationNode variable when sites.ImplicitTypes && IsImplicitVariant(variable, variable.Name, variable.TypeHint, directives):
                    hints.Add(new SourceInlayHint(variable.SourceLocation.Range.End, $"[As {VBTypeNames.VBVariant}]", InlayHintSite.ImplicitTypes, PaddingLeft: true));
                    break;
                case ForStatementNode { NextExpression: null, ControlExpression: SimpleNameExpressionNode counter } loop when sites.NextVariables:
                    hints.Add(new SourceInlayHint(loop.SourceLocation.Range.End, $"[{counter.IdentifierName}]", InlayHintSite.NextVariables, PaddingLeft: true));
                    break;
                case ForEachStatementNode { NextExpression: null, ControlExpression: SimpleNameExpressionNode element } loop when sites.NextVariables:
                    hints.Add(new SourceInlayHint(loop.SourceLocation.Range.End, $"[{element.IdentifierName}]", InlayHintSite.NextVariables, PaddingLeft: true));
                    break;
            }
        }

        return [.. hints
            .Where(hint => hint.Site == InlayHintSite.Arguments || hint.Position >= range.Start && hint.Position <= range.End)
            .OrderBy(hint => hint.Position)];
    }

    // the nodes of the module, and the headers a procedure has in the other branches of a #If block, with their parameters.
    private static IEnumerable<SyntaxNode> NodesOf(ModuleNode module)
    {
        foreach (var node in module.Descendants())
        {
            yield return node;
            if (node is MemberDeclarationNode { HeaderAlternatives.IsDefaultOrEmpty: false } member)
            {
                foreach (var alternative in member.HeaderAlternatives)
                {
                    yield return alternative with { EndRange = null };
                    foreach (var child in alternative.HeaderChildren)
                    {
                        yield return child;
                    }
                }
            }
        }
    }

    private static void AddArguments(List<SourceInlayHint> hints, ExpressionNode callee, IReadOnlyList<ExpressionNode> arguments, Func<ExpressionNode, SignatureDescription?> signatureOf)
    {
        if (arguments.Count == 0 || signatureOf(callee) is not { Parameters.Count: > 0 } signature)
        {
            return;
        }

        for (var position = 0; position < arguments.Count; position++)
        {
            var argument = arguments[position];
            if (argument is NamedArgumentNode)
            {
                break;
            }

            // past the parameters, an argument is one of the ParamArray's after its first, or one there is no parameter for.
            if (position >= signature.Parameters.Count)
            {
                if (!signature.Parameters[^1].IsParamArray)
                {
                    break;
                }

                continue;
            }

            var parameter = signature.Parameters[position];
            if (argument.Location.Range.IsEmpty
                || argument is SimpleNameExpressionNode { IdentifierName: var name } && string.Equals(name, parameter.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hints.Add(new SourceInlayHint(argument.Location.Range.Start, $"[{parameter.Name}:]", InlayHintSite.Arguments));
        }
    }

    private static bool IsProcedure(MemberKind kind)
        => kind is MemberKind.Procedure or MemberKind.Function or MemberKind.PropertyGet or MemberKind.PropertyLet or MemberKind.PropertySet;

    private static bool IsImplicitVariant(SyntaxNode declaration, string name, string? typeHint, IReadOnlyList<TypeDefDirectiveNode> directives)
        => string.IsNullOrEmpty(typeHint) && !declaration.Children.OfType<AsTypeExpressionNode>().Any()
            && (TypeDefDirectiveNode.Covering(directives, name) is not { } directive || directive.TypeName == VBTypeNames.VBVariant);

    private static bool Intersects(SourceRange node, SourceRange range) => node.Start <= range.End && range.Start <= node.End;
}
