using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Errors;
using RDCore.SDK.Model.Symbols.Abstract;
using System.Collections.Immutable;

namespace RDCore.SDK.Semantics;

/// <summary>
/// What the semantic analysis pass found out about one procedure: the facts a pass established about its code, which analyzers read to decide what
/// becomes a diagnostic.
/// </summary>
/// <remarks>
/// A model is immutable, and built by the pass that analyzed the procedure; it is never written back onto the syntax tree or onto a value. Today it
/// holds the compile errors of the static pass (<strong>RD-VBAL §5.0.1</strong>); the facts of the passes that follow (binding, types, flow) are
/// added to it as they come online, so that whoever consumes a model keeps consuming the same type.
/// </remarks>
/// <param name="Procedure">The identity of the procedure the model describes.</param>
/// <param name="CompileErrors">
/// Every compile error the static pass found in the procedure's body, in traversal order, followed by the errors that can only be told once the
/// whole body has been walked (a jump to a label that no line defines).
/// </param>
public sealed record class ProcedureSemanticModel(SemanticId Procedure, ImmutableArray<VBCompileErrorInfo> CompileErrors)
{
    /// <summary>
    /// What the static pass found out about each expression of the procedure's body, by the expression: its declared type, what it names and is
    /// bound to, and how it is written. Empty when the pass had no workspace to resolve names in.
    /// </summary>
    public ImmutableDictionary<SyntaxNodeId, ExpressionFact> Expressions { get; init; } = ImmutableDictionary<SyntaxNodeId, ExpressionFact>.Empty;

    /// <summary>
    /// Whether <see cref="Expressions"/> has a fact for every place the body refers to a name, and nothing was found wrong with it: the references the facts
    /// say there are in the body are all the references there are. It is <see langword="false"/> whenever that could not be established - a procedure with an
    /// error, an expression the pass does not look into, or no workspace to resolve names in - and what is counted from the facts is not known then, which is not
    /// to say that it is none.
    /// </summary>
    public bool IsFullyAnalyzed { get; init; }

    /// <summary>
    /// On how many of its code paths a function or a property getter assigns its return value, or <see langword="null"/> when the procedure returns none or the pass
    /// could not tell (<c>ReturnValueAnalysis</c>). A <see langword="null"/> says nothing about the procedure's paths.
    /// </summary>
    public Flow.ReturnValueFact? ReturnValue { get; init; }

    /// <summary>
    /// The reads of the procedure's own <c>For Each</c> control variables that see them emptied by the end of the loop (<c>ForEachControlAnalysis</c>),
    /// in the order they are written in. The list is empty when the pass could not tell, so an empty list says nothing about the procedure's reads.
    /// </summary>
    public ImmutableArray<Flow.EmptiedControlReadFact> EmptiedControlReads { get; init; } = [];

    /// <summary>
    /// The reads of the procedure's automatic local variables that the variable's initial value may reach (<see cref="Flow.InitialValueReadFact"/>), in the
    /// order they are written in. The list is empty when the language core could not tell, so an empty list says nothing about the procedure's reads.
    /// </summary>
    public ImmutableArray<Flow.InitialValueReadFact> InitialValueReads { get; init; } = [];

    /// <summary>
    /// The assignments of the procedure whose value no code path reads (<see cref="Flow.UnreadAssignmentFact"/>), in the order they are written in. The
    /// list is empty when the language core could not tell, so an empty list says nothing about the procedure's assignments.
    /// </summary>
    public ImmutableArray<Flow.UnreadAssignmentFact> UnreadAssignments { get; init; } = [];

    /// <summary>
    /// The arguments of the calls of the procedure's body that the pass mapped to a parameter, each with what the procedure called declares of it
    /// (<see cref="ArgumentFact"/>), in the order the pass met them. An argument that is not in the list was mapped to no parameter the pass knows.
    /// </summary>
    public ImmutableArray<ArgumentFact> Arguments { get; init; } = [];

    /// <summary>
    /// What the language core states about the conversions and operations of the procedure's code, or <see langword="null"/> when the procedure has not been
    /// evaluated. A <see langword="null"/> says nothing about the code.
    /// </summary>
    public Facts.RuntimeFacts? Runtime { get; init; }

    /// <summary>
    /// Whether the static pass found nothing wrong with the procedure.
    /// </summary>
    public bool IsValid => CompileErrors.IsEmpty;
}

/// <summary>
/// What the semantic analysis pass found out about one module: what is wrong with what it declares, and the model of each procedure it declares.
/// </summary>
/// <remarks>
/// A module is valid when its declarations are, and every procedure is: a module whose procedures are all valid may still declare a name twice.
/// </remarks>
/// <param name="Module">The address of the module the model describes.</param>
/// <param name="DeclarationErrors">The compile errors of the module's own declarations (<c>DeclarationStaticSemanticsEvaluator</c>), which no procedure body shows.</param>
/// <param name="Procedures">The model of each procedure the module declares, in declaration order.</param>
public sealed record class ModuleSemanticModel(
    Uri Module, ImmutableArray<VBCompileErrorInfo> DeclarationErrors, ImmutableArray<ProcedureSemanticModel> Procedures)
{
    /// <summary>
    /// Whether the module states <c>Option Explicit</c> (<strong>MS-VBAL §5.2.1.3</strong>): without it, a name that refers to nothing declared is not an
    /// error, but a variable that comes into being (<see cref="DeclarationFact.IsImplicit"/>).
    /// </summary>
    /// <remarks>
    /// The fact is not issued - it is <see langword="null"/> - for a module of a language that has no such directive
    /// (<see cref="Workspace.SupportedLanguage.HasOptionExplicit"/>): there is nothing it could state, and a <see langword="false"/> would say that it
    /// does not when it cannot.
    /// </remarks>
    public bool? OptionExplicit { get; init; }

    /// <summary>
    /// The identifier of the language the module is loaded as (<see cref="Workspace.SupportedLanguage.Id"/>): what is idiomatic in a module depends on it, and the
    /// host is what knows it. <see langword="null"/> when the host has not said.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// What the objects of the class are, for a class module (<see cref="Flags.ClassModuleSemanticFlags"/>): whether the class has a default instance, and
    /// whether its objects hold state. <see langword="null"/> for a module that is not a class module.
    /// </summary>
    public Flags.ClassModuleSemanticFlags? Class { get; init; }

    /// <summary>
    /// What the module is the code of, for a document module (<see cref="DocumentModuleFact"/>); <see langword="null"/> for any other module.
    /// </summary>
    public DocumentModuleFact? Document { get; init; }

    /// <summary>
    /// How each declaration of the module is used by the module's own code (<c>DeclarationUsage</c>): its variables, constants, parameters,
    /// procedures, properties and events, and the variables that were never declared. Empty when the pass had no workspace to resolve names in.
    /// </summary>
    public ImmutableArray<DeclarationFact> Declarations { get; init; } = [];

    /// <summary>
    /// Each class whose objects the module's code creates, with <c>New</c> or a variable declared <c>As New</c> (<see cref="ClassFact"/>), each listed once.
    /// </summary>
    public ImmutableArray<ClassFact> Classes { get; init; } = [];

    /// <summary>
    /// The names of the document modules a name written in the module can refer to: those of its project, and of the projects it references. In MS-VBA, the
    /// host makes such a name refer to its document, an object of the host's class; RD-VBA has no host to do so (<see cref="DocumentModuleFact"/>).
    /// </summary>
    public ImmutableArray<string> DocumentModules { get; init; } = [];

    /// <summary>
    /// The names of the conditional-compilation constants that the module's directives read and nothing defines: no <c>#Const</c> of the module, no constant
    /// of the project or of the command line, and no predefined constant (<see cref="SDK.Runtime.PredefinedConditionalConstants.Names"/>). Each is
    /// <c>0</c> where it is read, as in MS-VBA, which makes a condition that tests it <c>False</c>.
    /// </summary>
    /// <remarks>
    /// Each name once, as the first directive that reads it writes it, in the order they are written. Empty when every name is defined, and when the host
    /// does not say.
    /// </remarks>
    public ImmutableArray<string> UndefinedConditionalConstants { get; init; } = [];

    /// <summary>
    /// The conditional-compilation constants that the project defines, by a <c>.rdproj</c> <c>#Const</c> or a <c>--define</c> argument, with the value the
    /// host's session gives each (<see cref="ConditionalConstantFact"/>): what a directive of the module reads of a name the module does not define itself.
    /// </summary>
    /// <remarks>
    /// A <c>--define</c> argument wins over the <c>.rdproj</c>, and either wins over a predefined constant
    /// (<see cref="SDK.Runtime.PredefinedConditionalConstants"/>): a predefined constant that the project defines is listed, with the project's value.
    /// Empty when the project defines none, or when the host does not say.
    /// </remarks>
    public ImmutableArray<ConditionalConstantFact> ProjectConditionalConstants { get; init; } = [];

    /// <summary>
    /// Whether <c>Win64</c> is <c>1</c> on 64-bit Windows only in the host's sessions
    /// (<see cref="SDK.Runtime.Abstract.Execution.IRuntimeEnvironmentProfile.Win64OnlySetOnWindows"/>): the one value of the installation, as the host
    /// states it. <see langword="false"/> when the host does not say.
    /// </summary>
    public bool Win64OnlySetOnWindows { get; init; }

    /// <summary>
    /// What the static pass found out about each expression of the module's declarations outside any procedure (a constant's value, an array's
    /// bounds), by the expression, as <see cref="ProcedureSemanticModel.Expressions"/> holds those of a procedure's body. A name there is bound as it is
    /// in a procedure (<see cref="ExpressionFact.Binding"/>), to a declaration of the module or of another (<c>Const C = Globals.N + 1</c>). Empty when
    /// the pass had no workspace to resolve names in, and when the host does not say.
    /// </summary>
    public ImmutableDictionary<SyntaxNodeId, ExpressionFact> Expressions { get; init; } = ImmutableDictionary<SyntaxNodeId, ExpressionFact>.Empty;

    /// <summary>
    /// Every compile error of the module: those of its declarations, then those of each procedure.
    /// </summary>
    public ImmutableArray<VBCompileErrorInfo> CompileErrors => [.. DeclarationErrors, .. Procedures.SelectMany(procedure => procedure.CompileErrors)];

    /// <summary>
    /// Whether the static pass found nothing wrong with the module's declarations, nor with any procedure of the module.
    /// </summary>
    public bool IsValid => DeclarationErrors.IsEmpty && Procedures.All(procedure => procedure.IsValid);
}
