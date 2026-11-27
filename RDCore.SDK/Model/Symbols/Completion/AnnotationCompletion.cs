using System.Globalization;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Annotations;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Workspace;

namespace RDCore.SDK.Model.Symbols.Completion;

/// <summary>
/// An annotation being written at the end of a line (<strong>RD-VBAL §3.0.1.1</strong>): its name right after the <c>'@</c> that starts a comment, or
/// one of its arguments.
/// </summary>
/// <param name="CommentStart">The offset of the comment's <c>'</c> in the line.</param>
/// <param name="Name">
/// What is typed of the annotation's name: the whole name once its arguments are being written, and an empty string right after the <c>@</c>.
/// </param>
/// <param name="ArgumentIndex">The position of the argument being written, from 0; <see langword="null"/> while the name is being written.</param>
/// <param name="IsOnItsOwnLine">Whether nothing but white space is written before the comment on its line.</param>
public sealed record class AnnotationSite(int CommentStart, string Name, int? ArgumentIndex, bool IsOnItsOwnLine)
{
    /// <summary>
    /// Reads the annotation being written at the end of <paramref name="text"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An annotation starts a comment: its <c>@</c> is written right after the <c>'</c> that starts it. A <c>@</c> anywhere else starts none: the type
    /// hint of a <c>Currency</c> (<c>x@</c>), a character of a string literal or of the text of a comment, and a <c>'@</c> inside a <c>Rem</c> comment
    /// or after the text of a comment.
    /// </para>
    /// <para>
    /// The arguments follow the name in parentheses (<c>'@Description("Adds two numbers.")</c>), or after a space
    /// (<c>'@IgnoreModule RDC01001, RDC01002</c>), separated by commas; a comma in a string literal separates none. Once the parentheses are closed, no
    /// argument is being written.
    /// </para>
    /// </remarks>
    /// <param name="text">The text of the line, up to the cursor.</param>
    /// <returns>The annotation; <see langword="null"/> where none is being written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static AnnotationSite? Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var comment = CommentStartOf(text);
        if (comment < 0 || comment + 1 >= text.Length || text[comment + 1] != '@')
        {
            return null;
        }

        var nameStart = comment + 2;
        var nameEnd = nameStart;
        while (nameEnd < text.Length && (char.IsLetterOrDigit(text[nameEnd]) || text[nameEnd] == '_'))
        {
            nameEnd++;
        }

        var name = text[nameStart..nameEnd];
        if (name.Length > 0 && !char.IsLetter(name[0]))
        {
            return null;
        }

        var isOnItsOwnLine = string.IsNullOrWhiteSpace(text[..comment]);
        if (nameEnd == text.Length)
        {
            return new(comment, name, null, isOnItsOwnLine);
        }

        return name.Length > 0 && ArgumentIndexOf(text[nameEnd..]) is { } index ? new(comment, name, index, isOnItsOwnLine) : null;
    }

    // the offset of the `'` that starts the line's comment, outside a string literal; -1 for a line with no such comment, and for a Rem comment, whose
    // text a `'` is part of.
    private static int CommentStartOf(string text)
    {
        var inString = false;
        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '"':
                    inString = !inString;
                    break;
                case '\'' when !inString:
                    return index;
                default:
                    if (!inString && CompletionSite.IsRemAt(text, index))
                    {
                        return -1;
                    }

                    break;
            }
        }

        return -1;
    }

    // the position of the argument being written in what follows the name: the commas before it, outside string literals and nested parentheses; null
    // when an argument list does not follow, or the one that follows is closed.
    private static int? ArgumentIndexOf(string arguments)
    {
        var parenthesized = arguments[0] == '(';
        if (!parenthesized && !char.IsWhiteSpace(arguments[0]))
        {
            return null;
        }

        var depth = parenthesized ? 1 : 0;
        var inString = false;
        var commas = 0;
        foreach (var character in arguments[1..])
        {
            switch (character)
            {
                case '"':
                    inString = !inString;
                    break;
                case '(' when !inString:
                    depth++;
                    break;
                case ')' when !inString:
                    if (--depth == 0 && parenthesized)
                    {
                        return null;
                    }

                    break;
                case ',' when !inString && depth == (parenthesized ? 1 : 0):
                    commas++;
                    break;
            }
        }

        return commas;
    }
}

/// <summary>
/// Where an annotation is written: the module it is in, and what an annotation written there can annotate (<see cref="AnnotationCompletion.PlaceOf"/>).
/// </summary>
/// <param name="Module">
/// The address of the module: the file's own (<see cref="RDCoreDocument.File"/>), or a block's (<see cref="RDCoreDocument.BlockUri"/>).
/// </param>
/// <param name="Kind">The kind of the module.</param>
/// <param name="Targets">What an annotation written there annotates; <see cref="AnnotationTargets.None"/> where it annotates nothing.</param>
public sealed record class AnnotationPlace(Uri Module, AnnotationModuleKinds Kind, AnnotationTargets Targets);

/// <summary>
/// What completion offers for an annotation being written (<see cref="AnnotationSite"/>): the annotations of the catalogue that are legal where it is
/// written, each with its description, and the signature of the one whose arguments are being written, as a procedure call's
/// (<see cref="SignatureDescription"/>).
/// </summary>
/// <remarks>
/// Where an annotation is legal is read from the module with the line being typed made an annotation of its own (<see cref="ProbeLine"/>), parsed: what
/// that annotation binds to by the binding rules of annotations (<see cref="Annotations"/>) is what an annotation written there would bind to, whatever is
/// typed of it so far.
/// </remarks>
public static class AnnotationCompletion
{
    /// <summary>
    /// The name of the annotation that stands for the one being typed (<see cref="ProbeLine"/>): a name no extension declares.
    /// </summary>
    public const string Probe = "RDCoreAnnotationProbe";

    /// <summary>
    /// The line being typed, its comment replaced by the annotation <see cref="Probe"/> with no argument: the code before the comment and the line end are
    /// kept.
    /// </summary>
    /// <param name="line">The line, as it is written, its <c>\r</c> included when it has one.</param>
    /// <param name="site">The annotation being written on the line (<see cref="AnnotationSite.Read"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> or <paramref name="site"/> is <see langword="null"/>.</exception>
    public static string ProbeLine(string line, AnnotationSite site)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(site);
        return $"{line[..site.CommentStart]}'@{Probe}{(line.EndsWith('\r') ? "\r" : string.Empty)}";
    }

    /// <summary>
    /// Where the annotation <see cref="Probe"/> is written on a line of a file: the module whose parse holds it (<see cref="RDCoreDocument.ParseOf"/>), and
    /// what it binds to there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It annotates the module where it is written in the module's declarations section, or on the comment lines directly above a block's header
    /// (<see cref="Annotations.GetModuleAnnotations"/>); a member where it is directly above the declaration of a procedure, a <c>Declare</c> or an
    /// <c>Event</c>, and a variable or a constant where it is directly above one of the module's (<see cref="Annotations.GetAnnotations"/>). Directly
    /// above an <c>Enum</c> or a <c>Type</c>, it annotates no member and no variable. Right above the first procedure it annotates both the module
    /// and the procedure. It annotates the line below it where nothing but white space is before its comment on its line.
    /// </para>
    /// <para>
    /// The file's own module is of <paramref name="fileKind"/>, a form when the file has a designer (<see cref="ModuleParseResult.Designer"/>), and a
    /// block's module is of its block's kind (<see cref="BlockModule.Kind"/>).
    /// </para>
    /// </remarks>
    /// <param name="document">The file, parsed with the line made the probe's (<see cref="ProbeLine"/>).</param>
    /// <param name="fileKind">The kind of the file's own module.</param>
    /// <param name="site">The annotation being written, as it was typed.</param>
    /// <param name="line">The line, in the file.</param>
    /// <returns>The place; <see langword="null"/> when no module of the file holds the probe on that line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="site"/> is <see langword="null"/>.</exception>
    public static AnnotationPlace? PlaceOf(RDCoreDocument document, ModuleType fileKind, AnnotationSite site, int line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(site);
        foreach (var module in document.ModuleUris)
        {
            if (document.ParseOf(module) is not { SyntaxTree: not null } parse
                || parse.Trivia.OfType<AnnotationTriviaNode>()
                    .FirstOrDefault(annotation => annotation.SourceLocation.Range.Start.Line == line && annotation.IsNamed(Probe)) is not { } probe)
            {
                continue;
            }

            var block = document.Parse.BlockModules.FirstOrDefault(each => each.Uri == module);
            var kind = block is null ? KindOf(fileKind, document.Parse.Designer is not null) : KindOf(block.Kind, hasDesigner: false);
            var below = site.IsOnItsOwnLine ? AnnotationTargets.Line : AnnotationTargets.None;
            return new AnnotationPlace(module, kind, TargetsOf(parse, probe) | below);
        }

        return null;
    }

    /// <summary>
    /// The kind of module a module is, as annotations are legal in it (<see cref="AnnotationDeclaration.ModuleKinds"/>).
    /// </summary>
    /// <param name="type">The type of the module.</param>
    /// <param name="hasDesigner">Whether the module has a designer: a class module with one is a form.</param>
    public static AnnotationModuleKinds KindOf(ModuleType type, bool hasDesigner) => type switch
    {
        ModuleType.StdModule => AnnotationModuleKinds.Standard,
        ModuleType.ClassModule => hasDesigner ? AnnotationModuleKinds.Form : AnnotationModuleKinds.Class,
        ModuleType.DocumentModule => AnnotationModuleKinds.Document,
        _ => AnnotationModuleKinds.None,
    };

    /// <summary>
    /// The annotations a completion list offers at a place: those of the catalogue that are legal there (<see cref="AnnotationCatalogue.LegalAt"/>), each
    /// with its signature as its detail (<see cref="SignatureOf"/>) and its description in the language of the user interface as its documentation.
    /// </summary>
    /// <remarks>
    /// A deprecated annotation is left out of the list: it is still read, and reported where it is written. The annotation that replaces it is offered
    /// where it is legal.
    /// </remarks>
    /// <param name="catalogue">Every annotation the platform knows.</param>
    /// <param name="place">Where the annotation is written (<see cref="PlaceOf"/>).</param>
    /// <param name="culture">The language of the user interface.</param>
    /// <returns>The candidates, in the order of the catalogue.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<CompletionCandidate> CandidatesAt(AnnotationCatalogue catalogue, AnnotationPlace place, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(place);
        ArgumentNullException.ThrowIfNull(culture);
        return [.. catalogue.LegalAt(place.Targets, place.Kind)
            .Where(annotation => !annotation.IsDeprecated)
            .Select(annotation => new CompletionCandidate(annotation.Name, CompletionItemKind.Function,
                SignatureOf(catalogue, annotation, culture).Label, catalogue.DescriptionOf(annotation, culture)))];
    }

    /// <summary>
    /// What an annotation takes, as a signature help shows it: a label such as <c>@Description(Text)</c>, each parameter with its place in the label and
    /// its description, and the annotation's description as the signature's, both in the language of the user interface.
    /// </summary>
    /// <remarks>
    /// A parameter that may be left out is written <c>Optional</c>, and the last one, when it takes a list of arguments, <c>ParamArray</c>, as a
    /// procedure's are (<c>@IgnoreModule(ParamArray Codes())</c>): every argument from where it is on is given to it
    /// (<see cref="SignatureDescription.ActiveParameterOf"/>).
    /// </remarks>
    /// <param name="catalogue">Every annotation the platform knows, which describes the annotation.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="culture">The language of the user interface.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static SignatureDescription SignatureOf(AnnotationCatalogue catalogue, AnnotationDeclaration annotation, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(culture);
        var label = new System.Text.StringBuilder($"@{annotation.Name}(");
        var parameters = new List<SignatureParameter>();
        foreach (var parameter in annotation.Parameters)
        {
            if (parameters.Count > 0)
            {
                label.Append(", ");
            }

            var start = label.Length;
            label.Append(parameter.Repeats ? $"ParamArray {parameter.Name}()" : parameter.IsOptional ? $"Optional {parameter.Name}" : parameter.Name);
            parameters.Add(new SignatureParameter(parameter.Name, start, label.Length, parameter.Repeats,
                catalogue.DescriptionOf(annotation, parameter, culture), IsOptional: parameter.IsOptional));
        }

        label.Append(')');
        return new SignatureDescription(label.ToString(), parameters, catalogue.DescriptionOf(annotation, culture));
    }

    // what an annotation of a module's parse binds to, by the binding rules of annotations: the module, and the declaration it is directly above.
    private static AnnotationTargets TargetsOf(ModuleParseResult parse, AnnotationTriviaNode annotation)
    {
        var targets = parse.GetModuleAnnotations().Contains(annotation) ? AnnotationTargets.Module : AnnotationTargets.None;
        foreach (var declaration in parse.SyntaxTree!.Children.Where(child => parse.GetAnnotations(child).Contains(annotation)))
        {
            targets |= declaration switch
            {
                MemberDeclarationNode { MemberKind: MemberKind.ModuleField } or VariableDeclarationNode or ConstantDeclarationNode
                    => AnnotationTargets.Variable,
                MemberDeclarationNode { MemberKind: MemberKind.Enum or MemberKind.UserDefinedType or MemberKind.UserDefinedTypeField }
                    => AnnotationTargets.None,
                MemberDeclarationNode => AnnotationTargets.Member,
                _ => AnnotationTargets.None,
            };
        }

        return targets;
    }
}
