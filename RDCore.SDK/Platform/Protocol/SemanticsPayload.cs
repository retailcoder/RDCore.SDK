using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Client;
using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Errors;
using RDCore.SDK.Model.Errors.Abstract;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Platform.Components;
using RDCore.SDK.Semantics;
using RDCore.SDK.Semantics.Facts;
using RDCore.SDK.Semantics.Flags;
using RDCore.SDK.Semantics.Flow;
using RDCore.SDK.Semantics.Runtime.Operators;
using System.Collections.Immutable;

namespace RDCore.SDK.Platform.Protocol;

/// <summary>
/// The semantic facts of one or more modules, as they travel (<see cref="HostSemanticsResult.Json"/>).
/// </summary>
/// <param name="Modules">The model of each module asked for that the host has one of.</param>
public record class SemanticsPayload(ImmutableArray<ModuleSemanticsDto> Modules)
{
    /// <summary>
    /// The model of the module at <paramref name="module"/>, if there is one.
    /// </summary>
    /// <param name="module">The address of the module.</param>
    public ModuleSemanticsDto? Of(Uri module)
        => Modules.IsDefault ? null : Modules.FirstOrDefault(candidate => candidate.Module.AbsoluteUri == module.AbsoluteUri);
}

/// <summary>
/// A compile error, as it travels: what a diagnostics extension needs to say it.
/// </summary>
/// <param name="Code">The diagnostic code of the error: a <see cref="RDCoreDiagnosticCode.CompilePrefix"/> code.</param>
/// <param name="Location">Where it is.</param>
/// <param name="Description">What the error is: its message.</param>
/// <param name="Verbose">What is wrong, in particular.</param>
public record class CompileErrorDto(RDCoreDiagnosticCode Code, SourceLocation Location, string Description, string Verbose)
{
    /// <summary>
    /// Describes a compile error for the wire.
    /// </summary>
    /// <param name="error">The error.</param>
    public static CompileErrorDto From(VBCompileErrorInfo error)
        => new(new RDCoreDiagnosticCode(RDCoreDiagnosticCode.CompilePrefix, error.ErrorId), error.Location, error.Description, error.Verbose);

    /// <summary>
    /// The compile error.
    /// </summary>
    public VBCompileErrorInfo ToInfo() => new(Code.Number, Location, Description, Verbose);
}

/// <summary>
/// An <see cref="ExpressionFact"/>, as it travels.
/// </summary>
/// <param name="Node">The expression.</param>
/// <param name="Location">Where it is written.</param>
/// <param name="DeclaredType">The name of its declared type, or <see langword="null"/> when it is an error.</param>
/// <param name="Classification">What it names.</param>
/// <param name="Binding">The address of the symbol it refers to, when it resolved to one.</param>
/// <param name="Flags">What else is the case of it.</param>
/// <param name="Error">Its compile error, when it has one.</param>
/// <param name="Creates">The class of the object it creates by name, when that is known (<see cref="ExpressionFact.Creates"/>): for tooling only.</param>
/// <param name="TypeKind">
/// The kind of its declared type, or of its elements' type for an array (<see cref="DeclaredTypeKinds.Of"/>); <see langword="null"/> when it has no type a
/// value can have, or one that did not resolve.
/// </param>
/// <param name="IsArray">Whether its declared type is an array.</param>
/// <param name="ConstantValue">
/// The constant binding of the constant it names, when that is known (<see cref="ExpressionFact.ConstantValue"/>); <see langword="null"/> otherwise.
/// </param>
/// <param name="Accessors">The accessors of the property it names (<see cref="ExpressionFact.Accessors"/>); <see cref="PropertyAccessors.None"/> otherwise.</param>
/// <param name="LetValueType">
/// The name of the declared type of the value parameter of the <c>Property Let</c> of the property it names (<see cref="ExpressionFact.LetValueType"/>);
/// <see langword="null"/> when it names no property with one.
/// </param>
/// <param name="DefaultMember">
/// The default member of the class its declared type is (<see cref="ExpressionFact.DefaultMember"/>); <see langword="null"/> otherwise.
/// </param>
/// <param name="IsLetCoerced">Whether its value is taken and Let-coerced to a declared type (<see cref="ExpressionFact.IsLetCoerced"/>).</param>
/// <param name="LetValueTypeKind">
/// The kind of the declared type of the value parameter of the <c>Property Let</c> of the property it names (<see cref="DeclaredTypeKinds.Of"/>), which
/// <paramref name="LetValueType"/> names; <see langword="null"/> when it names no property with one, or one whose type did not resolve.
/// </param>
/// <param name="NativeCall">
/// For an expression bound to a procedure a <c>Declare</c> statement declares, the platforms it is compiled on and those the procedure is declared on
/// (<see cref="ExpressionFact.NativeCall"/>); <see langword="null"/> otherwise.
/// </param>
/// <param name="SetCoercion">
/// The Set-coercion of its value to a class of the workspace's projects (<see cref="ExpressionFact.SetCoercion"/>): the value of a <c>Set</c> statement
/// (<strong>MS-VBAL §5.4.3.9</strong>), or an argument given to a parameter declared as a class (<strong>MS-VBAL §5.3.1.11</strong>);
/// <see langword="null"/> otherwise.
/// </param>
/// <param name="StringForm">
/// For an expression bound to a function of a library that returns a <c>Variant</c>, the name of the member that is functionally identical to it except
/// that it returns a <c>String</c> (<see cref="ExpressionFact.StringForm"/>); <see langword="null"/> otherwise.
/// </param>
public record class ExpressionFactDto(
    SyntaxNodeId Node,
    SourceLocation Location,
    string? DeclaredType,
    ExpressionClassification Classification,
    Uri? Binding,
    ValueExpressionSemanticFlags Flags,
    CompileErrorDto? Error,
    ObjectCreationFact? Creates = null,
    DeclaredTypeKind? TypeKind = null,
    bool IsArray = false,
    VBTypedValue? ConstantValue = null,
    PropertyAccessors Accessors = PropertyAccessors.None,
    string? LetValueType = null,
    DefaultMemberFact? DefaultMember = null,
    bool IsLetCoerced = false,
    DeclaredTypeKind? LetValueTypeKind = null,
    NativeCallPlatforms? NativeCall = null,
    SetCoercionFact? SetCoercion = null,
    string? StringForm = null)
{
    /// <summary>
    /// Describes a fact for the wire.
    /// </summary>
    /// <param name="fact">The fact of an expression.</param>
    public static ExpressionFactDto From(ExpressionFact fact)
    {
        var (kind, isArray) = DeclaredTypeKinds.Of(fact.DeclaredType);
        return new(fact.Node, fact.Location, fact.DeclaredType?.Name, fact.Classification, fact.Binding?.Uri, fact.Flags,
            fact.Error is null ? null : CompileErrorDto.From(fact.Error), fact.Creates, kind, isArray, fact.ConstantValue,
            fact.Accessors, fact.LetValueType?.Name, fact.DefaultMember, fact.IsLetCoerced, DeclaredTypeKinds.Of(fact.LetValueType).Kind, fact.NativeCall,
            fact.SetCoercion, fact.StringForm);
    }
}

/// <summary>
/// A run-time error that code is guaranteed to raise, as it travels.
/// </summary>
/// <param name="ErrorId">The number of the error.</param>
/// <param name="Location">Where it is raised.</param>
/// <param name="Description">What the error is.</param>
/// <param name="Verbose">What is wrong, in particular.</param>
/// <param name="IsApplicationError">
/// Whether the code raises the error for itself (<c>Error</c>, <c>Err.Raise</c>): a <see cref="VBApplicationErrorInfo"/>, rather than one the language
/// raises (<see cref="VBRuntimeErrorInfo"/>).
/// </param>
public record class RuntimeErrorDto(int ErrorId, SourceLocation Location, string Description, string Verbose, bool IsApplicationError = false)
{
    /// <summary>
    /// Describes an error for the wire.
    /// </summary>
    /// <param name="error">The error.</param>
    public static RuntimeErrorDto From(VBErrorInfo error)
        => new(error.ErrorId, error.Location, error.Description, error.Verbose, error is VBApplicationErrorInfo);

    /// <summary>
    /// The error, as the family it belongs to describes it.
    /// </summary>
    public VBErrorInfo ToInfo() => IsApplicationError
        ? new VBApplicationErrorInfo(ErrorId, Location, Description, Verbose)
        : new VBRuntimeErrorInfo(ErrorId, Location, Description, Verbose);
}

/// <summary>
/// A <see cref="ConversionFact"/>, as it travels.
/// </summary>
/// <param name="Node">The syntax node the conversion is evaluated for.</param>
/// <param name="Location">Where the conversion is.</param>
/// <param name="Site">The construct that asks for the conversion.</param>
/// <param name="Source">The name of the declared type of the value that is converted.</param>
/// <param name="Destination">The name of the type it is converted to.</param>
/// <param name="Flags">What the conversion does, and what is known about its source.</param>
/// <param name="IsValueKnown">Whether the value that is converted is known, as opposed to a value its type merely allows.</param>
/// <param name="Error">The run-time error the conversion raises, when it is guaranteed to.</param>
/// <param name="Operand">The operand that is converted: its position in the operation that converts it.</param>
public record class ConversionFactDto(
    SyntaxNodeId Node,
    SourceLocation Location,
    ConversionSite Site,
    string Source,
    string Destination,
    ConversionSemanticFlags Flags,
    bool IsValueKnown,
    RuntimeErrorDto? Error,
    InputIndex Operand = default)
{
    /// <summary>
    /// Describes a conversion for the wire.
    /// </summary>
    /// <param name="fact">The conversion.</param>
    public static ConversionFactDto From(ConversionFact fact) => new(
        fact.Node,
        fact.Location,
        fact.Site,
        fact.Source.Name,
        fact.Destination.Name,
        fact.Flags,
        fact.IsValueKnown,
        fact.Error is null ? null : RuntimeErrorDto.From(fact.Error),
        fact.Operand);
}

/// <summary>
/// An <see cref="OperatorFact"/>, as it travels: an operation of an operator, or the access of a member (<see cref="MemberAccessFact"/>).
/// </summary>
/// <remarks>
/// 👉 One record carries every family: the flags of the family the operation belongs to are set, and those of every other family are <see langword="null"/>.
/// </remarks>
/// <param name="Node">The syntax node the operation is evaluated for.</param>
/// <param name="Location">Where the operation is.</param>
/// <param name="Operator">The operator: one of the <c>Tokens</c> operators, or <c>Tokens.MemberAccess</c> / <c>Tokens.DictionaryAccess</c> for a member access.</param>
/// <param name="EffectiveType">The name of the <em>effective type</em> of the operation, or of the declared type of the member that is accessed, when it has one.</param>
/// <param name="IsValueKnown">Whether every operand is known (the object, for a member access), as opposed to a value its type merely allows.</param>
/// <param name="ArithmeticOpSemanticFlags">What is known about an arithmetic operation (<see cref="ArithmeticOperatorFact"/>).</param>
/// <param name="ConcatOpSemanticFlags">What is known about a concatenation (<see cref="ConcatOperatorFact"/>).</param>
/// <param name="ComparisonOpSemanticFlags">What is known about a comparison (<see cref="ComparisonOperatorFact"/>).</param>
/// <param name="LogicalOpSemanticFlags">What is known about a logical operation (<see cref="LogicalOperatorFact"/>).</param>
/// <param name="MemberAccessOpSemanticFlags">What is known about a member access (<see cref="MemberAccessFact"/>).</param>
/// <param name="Error">The run-time error the operation raises, when it is guaranteed to.</param>
/// <param name="Comparison">How a comparison compares <c>String</c> values where it is evaluated (<c>Option Compare</c>); <see langword="null"/> for any other operation.</param>
/// <param name="ComparisonCulture">The name of the culture a <c>Text</c> comparison compares with, when it is not the invariant one.</param>
/// <param name="Member">The name of the member that is accessed, when it is known (<see cref="MemberAccessFact.Member"/>).</param>
/// <param name="OperatorLocation">Where the operator token is (<see cref="OperatorFact.OperatorLocation"/>), at which a diagnostic about the operator is
/// located; <see langword="null"/> when the operation has no token of its own, or a host does not state it, and a diagnostic is then located at
/// <paramref name="Location"/>.</param>
/// <param name="StringOperand">
/// The <c>String</c> operand of a comparison of a <c>String</c> with a number, Let-coerced both ways (<see cref="ComparisonOperatorFact.StringOperand"/>);
/// <see langword="null"/> for any other operation.
/// </param>
/// <param name="LateBoundCandidates">
/// The members a late-bound access may resolve to when it runs (<see cref="MemberAccessFact.LateBoundCandidates"/>): <see langword="null"/> when the
/// classes its object may be of are not known, and for any other operation; an empty array when they are known and none has an accessible member of
/// the name.
/// </param>
/// <param name="DefaultMembers">
/// The default members an argument list or a dictionary access given to an object goes through, in the order they are called
/// (<see cref="MemberAccessFact.DefaultMembers"/>): <see langword="null"/> when the classes on the way are not all known, and for any other operation.
/// </param>
public record class OperatorFactDto(
    SyntaxNodeId Node,
    SourceLocation Location,
    string Operator,
    string? EffectiveType,
    bool IsValueKnown,
    ArithmeticOperatorSemanticFlags? ArithmeticOpSemanticFlags,
    ConcatOperationSemanticFlags? ConcatOpSemanticFlags,
    ComparisonOperatorSemanticFlags? ComparisonOpSemanticFlags,
    LogicalOperatorSemanticFlags? LogicalOpSemanticFlags,
    MemberAccessOperationSemanticFlags? MemberAccessOpSemanticFlags,
    RuntimeErrorDto? Error,
    OptionCompare? Comparison = null,
    string? ComparisonCulture = null,
    string? Member = null,
    SourceLocation? OperatorLocation = null,
    StringOperandFact? StringOperand = null,
    ImmutableArray<LateBoundCandidate>? LateBoundCandidates = null,
    ImmutableArray<DefaultMemberFact>? DefaultMembers = null)
{
    /// <summary>
    /// Describes an operation for the wire.
    /// </summary>
    /// <param name="fact">The operation.</param>
    public static OperatorFactDto From(OperatorFact fact) => new(
        fact.Node,
        fact.Location,
        fact.Operator,
        fact.EffectiveType?.Name,
        fact.IsValueKnown,
        ArithmeticOpSemanticFlags: (fact as ArithmeticOperatorFact)?.Flags,
        ConcatOpSemanticFlags: (fact as ConcatOperatorFact)?.Flags,
        ComparisonOpSemanticFlags: (fact as ComparisonOperatorFact)?.Flags,
        LogicalOpSemanticFlags: (fact as LogicalOperatorFact)?.Flags,
        MemberAccessOpSemanticFlags: (fact as MemberAccessFact)?.Flags,
        Error: fact.Error is null ? null : RuntimeErrorDto.From(fact.Error),
        Comparison: (fact as ComparisonOperatorFact)?.Comparison.Mode,
        ComparisonCulture: (fact as ComparisonOperatorFact)?.Comparison.Culture is { Name.Length: > 0 } culture ? culture.Name : null,
        Member: (fact as MemberAccessFact)?.Member,
        OperatorLocation: fact.OperatorLocation,
        StringOperand: (fact as ComparisonOperatorFact)?.StringOperand,
        LateBoundCandidates: (fact as MemberAccessFact)?.LateBoundCandidates,
        DefaultMembers: (fact as MemberAccessFact)?.DefaultMembers);
}

/// <summary>
/// A <see cref="StatementFact"/>, as it travels.
/// </summary>
/// <remarks>
/// 👉 One record carries every kind of statement: the flags of the kind the statement is are set, and those of every other kind are <see langword="null"/>.
/// </remarks>
/// <param name="Node">The statement.</param>
/// <param name="Location">Where it is.</param>
/// <param name="Statement">Its keyword: one of the <c>Tokens</c>.</param>
/// <param name="FixedAssignmentFlags">What is known about an <c>LSet</c> or an <c>RSet</c> statement (<see cref="FixedAssignmentStatementFact"/>).</param>
/// <param name="WithStatementFlags">What is known about a <c>With</c> statement (<see cref="WithStatementFact"/>).</param>
/// <param name="IsValueKnown">
/// Whether what the statement works with is known, as opposed to a value its type merely allows; <see langword="true"/> for a statement whose fact does not
/// depend on it.
/// </param>
/// <param name="CaseClauseFlags">What is known about a range clause of a <c>Case</c> (<see cref="CaseClauseStatementFact"/>).</param>
/// <param name="ComparedIn">
/// The name of the type the value of a range clause of a <c>Case</c> is compared with the selector in (<see cref="CaseClauseStatementFact.ComparedIn"/>);
/// <see langword="null"/> for any other statement.
/// </param>
/// <param name="SpecifiedEffectiveType">
/// The name of the effective type of the comparison of the selector with the value of a range clause as an expression
/// (<see cref="CaseClauseStatementFact.SpecifiedEffectiveType"/>); <see langword="null"/> when the table of <strong>MS-VBAL 5.6.9.5</strong> gives none,
/// and for any other statement.
/// </param>
public record class StatementFactDto(
    SyntaxNodeId Node,
    SourceLocation Location,
    string Statement,
    FixedAssignmentSemanticFlags? FixedAssignmentFlags,
    WithStatementSemanticFlags? WithStatementFlags,
    bool IsValueKnown = true,
    CaseClauseSemanticFlags? CaseClauseFlags = null,
    string? ComparedIn = null,
    string? SpecifiedEffectiveType = null)
{
    /// <summary>
    /// Describes a statement for the wire.
    /// </summary>
    /// <param name="fact">The statement.</param>
    public static StatementFactDto From(StatementFact fact) => new(
        fact.Node,
        fact.Location,
        fact.Statement,
        (fact as FixedAssignmentStatementFact)?.Flags,
        (fact as WithStatementFact)?.Flags,
        fact switch
        {
            WithStatementFact with => with.IsValueKnown,
            CaseClauseStatementFact clause => clause.IsValueKnown,
            _ => true,
        },
        (fact as CaseClauseStatementFact)?.Flags,
        (fact as CaseClauseStatementFact)?.ComparedIn?.Name,
        (fact as CaseClauseStatementFact)?.SpecifiedEffectiveType?.Name);
}

/// <summary>
/// A <see cref="RuntimeFacts"/>, as it travels.
/// </summary>
/// <param name="Conversions">Every conversion the code of the procedure asks for.</param>
/// <param name="Operations">Every operation the code of the procedure performs, and every member it accesses.</param>
/// <param name="Statements">What the statements that establish facts of their own are.</param>
/// <param name="Errors">Every run-time error an instruction of the procedure raises whenever it runs (<see cref="RuntimeFacts.Errors"/>).</param>
/// <param name="IsFullyAnalyzed">Whether every instruction of the procedure was evaluated.</param>
public record class RuntimeFactsDto(
    ImmutableArray<ConversionFactDto> Conversions,
    ImmutableArray<OperatorFactDto> Operations,
    ImmutableArray<StatementFactDto> Statements,
    ImmutableArray<RuntimeErrorDto> Errors,
    bool IsFullyAnalyzed)
{
    /// <summary>
    /// Describes the facts of a procedure for the wire.
    /// </summary>
    /// <param name="facts">The facts.</param>
    public static RuntimeFactsDto From(RuntimeFacts facts) => new(
        [.. facts.Conversions.Select(ConversionFactDto.From)],
        [.. facts.Operations.Select(OperatorFactDto.From)],
        [.. facts.Statements.Select(StatementFactDto.From)],
        [.. facts.Errors.Select(RuntimeErrorDto.From)],
        facts.IsFullyAnalyzed);
}

/// <summary>
/// A <see cref="ProcedureSemanticModel"/>, as it travels.
/// </summary>
/// <param name="Procedure">The address of the procedure.</param>
/// <param name="IsFullyAnalyzed">Whether the references the facts say there are in its body are all of them.</param>
/// <param name="CompileErrors">What the static pass found wrong with the body.</param>
/// <param name="Expressions">What it found out about each expression.</param>
/// <param name="ReturnValue">On how many code paths a function or a property getter assigns its return value, when the pass could tell.</param>
/// <param name="Runtime">What the language core states about the code, or <see langword="null"/> when the procedure has not been evaluated.</param>
/// <param name="EmptiedControlReads">
/// The reads of its own <c>For Each</c> control variables that see them emptied by the end of the loop; <see langword="null"/> when there are none the pass could tell.
/// </param>
/// <param name="Arguments">
/// The arguments of its calls that the pass mapped to a parameter, with what the procedure called declares of it; <see langword="null"/> when there are none.
/// </param>
/// <param name="InitialValueReads">
/// The reads of its automatic local variables that the variable's initial value may reach (<see cref="ProcedureSemanticModel.InitialValueReads"/>);
/// <see langword="null"/> when there are none, and when the language core could not tell.
/// </param>
/// <param name="UnreadAssignments">
/// Its assignments whose value no code path reads (<see cref="ProcedureSemanticModel.UnreadAssignments"/>); <see langword="null"/> when there are none,
/// and when the language core could not tell.
/// </param>
/// <param name="JumpsIntoLoops">
/// Its statements that jump into a <c>For</c> or a <c>For Each</c> loop from outside it (<see cref="ProcedureSemanticModel.JumpsIntoLoops"/>);
/// <see langword="null"/> when there are none, and when the language core could not tell.
/// </param>
public record class ProcedureSemanticsDto(
    Uri Procedure, bool IsFullyAnalyzed, ImmutableArray<CompileErrorDto> CompileErrors, ImmutableArray<ExpressionFactDto> Expressions, ReturnValueFact? ReturnValue = null,
    RuntimeFactsDto? Runtime = null, ImmutableArray<EmptiedControlReadFact>? EmptiedControlReads = null, ImmutableArray<ArgumentFact>? Arguments = null,
    ImmutableArray<InitialValueReadFact>? InitialValueReads = null, ImmutableArray<UnreadAssignmentFact>? UnreadAssignments = null,
    ImmutableArray<JumpIntoLoopFact>? JumpsIntoLoops = null);

/// <summary>
/// A <see cref="DeclarationFact"/>, as it travels.
/// </summary>
/// <param name="Symbol">The address of the declared symbol.</param>
/// <param name="Name">The name it is declared with.</param>
/// <param name="Kind">What it declares.</param>
/// <param name="Access">Who can refer to it besides the module's own code.</param>
/// <param name="IsImplicit">Whether it was never declared.</param>
/// <param name="Location">Where it is declared.</param>
/// <param name="References">Every reference to it, when they are known; <see langword="null"/> says they are not.</param>
/// <param name="Role">What it is there for, when it implements an interface member or handles an event.</param>
/// <param name="DeclaredBy">How a local variable came to be declared; <see langword="null"/> for any other declaration.</param>
/// <param name="Hides">What a local or a parameter hides; <see langword="null"/> when it hides nothing, and for any other declaration.</param>
/// <param name="ReferencedFrom">Each reference to it, in any module of the project; <see langword="null"/> exactly when <paramref name="References"/> is.</param>
/// <param name="TypeKind">The kind of type it is declared as, or of its elements' type for an array; <see langword="null"/> when it has no type, or one that did
/// not resolve.</param>
/// <param name="IsArray">Whether it is declared as an array.</param>
/// <param name="AutoInstantiates">
/// The address of the class whose object it is given where code first uses it, for a variable declared <c>As New</c>
/// (<see cref="DeclarationFact.AutoInstantiates"/>); <see langword="null"/> for any other declaration.
/// </param>
/// <param name="CompiledOn">
/// For a procedure a <c>Declare</c> statement declares, the platforms it is declared on (<see cref="DeclarationFact.CompiledOn"/>); <see langword="null"/>
/// for any other declaration.
/// </param>
/// <param name="StatementsCompiledOn">
/// For a procedure a <c>Declare</c> statement declares, the platforms each of its statements is compiled on, in the order they are written
/// (<see cref="DeclarationFact.StatementsCompiledOn"/>); <see langword="null"/> for any other declaration.
/// </param>
/// <param name="Binding">
/// What it is bound to, for a handler or an implementation (<see cref="DeclarationFact.Binding"/>); <see langword="null"/> for any other declaration.
/// </param>
/// <param name="IsUsedByReferencingProjects">
/// Whether it counts as used by the projects that reference a library project (<see cref="DeclarationFact.IsUsedByReferencingProjects"/>).
/// </param>
/// <param name="IsReferredToByABoundMember">
/// For a parameter of an interface member or of an event, whether an implementation or a handler refers to its parameter at the same position
/// (<see cref="DeclarationFact.IsReferredToByABoundMember"/>); <see langword="null"/> when that is not stated.
/// </param>
/// <param name="IsWrittenByABoundMember">
/// For a parameter of an interface member or of an event, whether an implementation or a handler writes its parameter at the same position, or passes it on
/// by reference (<see cref="DeclarationFact.IsWrittenByABoundMember"/>); <see langword="null"/> when that is not stated.
/// </param>
public record class DeclarationFactDto(
    Uri Symbol, string Name, DeclarationKind Kind, AccessModifier Access, bool IsImplicit, SourceLocation Location, DeclarationReferences? References,
    DeclarationRole Role = DeclarationRole.None, LocalDeclarationKind? DeclaredBy = null, HiddenDeclaration? Hides = null,
    ImmutableArray<DeclarationReference>? ReferencedFrom = null, DeclaredTypeKind? TypeKind = null, bool IsArray = false, Uri? AutoInstantiates = null,
    CompiledPlatforms? CompiledOn = null, ImmutableArray<CompiledPlatforms>? StatementsCompiledOn = null, DeclarationBindingDto? Binding = null,
    bool IsUsedByReferencingProjects = false, bool? IsReferredToByABoundMember = null, bool? IsWrittenByABoundMember = null);

/// <summary>
/// A <see cref="DeclarationBinding"/>, as it travels: the member it binds is the declaration it is the binding of, and its role is the declaration's
/// (<see cref="DeclarationFactDto.Role"/>).
/// </summary>
/// <param name="Source">The address of what the part of the member's name before the underscore names (<see cref="DeclarationBinding.Source"/>).</param>
/// <param name="Bound">
/// The address of the event the member handles or the interface member it implements (<see cref="DeclarationBinding.Bound"/>); <see langword="null"/> for an
/// <see cref="DeclarationRole.UnmatchedEventHandler"/>.
/// </param>
/// <param name="BoundLibrary">
/// The library that declares the class of the event or of the interface member (<see cref="DeclarationBinding.BoundLibrary"/>); <see langword="null"/> for a
/// class of the session's own project.
/// </param>
/// <param name="IsBoundInSourceProject">
/// Whether that library is a referenced project of the workspace, whose source it has (<see cref="DeclarationBinding.IsBoundInSourceProject"/>).
/// </param>
/// <param name="IsSourceAControl">Whether the source is a control of the form (<see cref="DeclarationBinding.IsSourceAControl"/>).</param>
public record class DeclarationBindingDto(
    Uri Source, Uri? Bound, string? BoundLibrary = null, bool IsBoundInSourceProject = false, bool IsSourceAControl = false);

/// <summary>
/// A <see cref="ModuleSemanticModel"/>, as it travels.
/// </summary>
/// <param name="Module">The address of the module.</param>
/// <param name="OptionExplicit">Whether the module states <c>Option Explicit</c>; not issued (<see langword="null"/>) in a language that has no such directive.</param>
/// <param name="DeclarationErrors">What is wrong with what the module declares.</param>
/// <param name="Procedures">The model of each procedure of the module.</param>
/// <param name="Declarations">What is known of how the module's declarations are used.</param>
/// <param name="Language">The identifier of the language the module is loaded as, when the host says.</param>
/// <param name="Class">What the objects of the class are, for a class module (<see cref="ClassModuleSemanticFlags"/>); <see langword="null"/> for any other module.</param>
/// <param name="Document">What the module is the code of, for a document module (<see cref="DocumentModuleFact"/>); <see langword="null"/> for any other module.</param>
/// <param name="Classes">
/// Each class whose objects the module's code creates (<see cref="ModuleSemanticModel.Classes"/>); <see langword="null"/> when it creates none.
/// </param>
/// <param name="DocumentModules">
/// The names of the document modules a name written in the module can refer to (<see cref="ModuleSemanticModel.DocumentModules"/>); <see langword="null"/>
/// when there are none.
/// </param>
/// <param name="UndefinedConditionalConstants">
/// The names of the conditional-compilation constants the module's directives read and nothing defines
/// (<see cref="ModuleSemanticModel.UndefinedConditionalConstants"/>); <see langword="null"/> when there are none.
/// </param>
/// <param name="ProjectConditionalConstants">
/// The conditional-compilation constants the project defines, with their values (<see cref="ModuleSemanticModel.ProjectConditionalConstants"/>);
/// <see langword="null"/> when there are none.
/// </param>
/// <param name="Win64OnlySetOnWindows">
/// Whether <c>Win64</c> is <c>1</c> on 64-bit Windows only in the host's sessions (<see cref="ModuleSemanticModel.Win64OnlySetOnWindows"/>).
/// </param>
/// <param name="Expressions">
/// What the static pass found out about each expression of the module's declarations outside any procedure, such as a constant's value or an array's
/// bounds (<see cref="ModuleSemanticModel.Expressions"/>). <see langword="null"/> when there are none, and when the host does not say.
/// </param>
/// <param name="Directives">
/// The directives the module is under, as every part of it states them (<see cref="ModuleSemanticModel.Directives"/>); <see langword="null"/> when the host
/// does not say.
/// </param>
public record class ModuleSemanticsDto(
    Uri Module,
    bool? OptionExplicit,
    ImmutableArray<CompileErrorDto> DeclarationErrors,
    ImmutableArray<ProcedureSemanticsDto> Procedures,
    ImmutableArray<DeclarationFactDto> Declarations,
    string? Language = null,
    ClassModuleSemanticFlags? Class = null,
    DocumentModuleFact? Document = null,
    ImmutableArray<ClassFact>? Classes = null,
    ImmutableArray<string>? DocumentModules = null,
    ImmutableArray<string>? UndefinedConditionalConstants = null,
    ImmutableArray<ConditionalConstantFact>? ProjectConditionalConstants = null,
    bool Win64OnlySetOnWindows = false,
    ImmutableArray<ExpressionFactDto>? Expressions = null,
    ModuleDirectives? Directives = null)
{
    /// <summary>
    /// Describes a model for the wire.
    /// </summary>
    /// <param name="model">The model of a module.</param>
    public static ModuleSemanticsDto From(ModuleSemanticModel model) => new(
        model.Module,
        model.OptionExplicit,
        [.. model.DeclarationErrors.Select(ErrorOf)],
        [.. model.Procedures.Select(procedure => new ProcedureSemanticsDto(
            procedure.Procedure.Uri,
            procedure.IsFullyAnalyzed,
            [.. procedure.CompileErrors.Select(ErrorOf)],
            [.. procedure.Expressions.Values.Select(ExpressionFactDto.From)],
            procedure.ReturnValue,
            procedure.Runtime is null ? null : RuntimeFactsDto.From(procedure.Runtime),
            procedure.EmptiedControlReads.IsDefaultOrEmpty ? null : procedure.EmptiedControlReads,
            procedure.Arguments.IsDefaultOrEmpty ? null : procedure.Arguments,
            procedure.InitialValueReads.IsDefaultOrEmpty ? null : procedure.InitialValueReads,
            procedure.UnreadAssignments.IsDefaultOrEmpty ? null : procedure.UnreadAssignments,
            procedure.JumpsIntoLoops.IsDefaultOrEmpty ? null : procedure.JumpsIntoLoops))],
        [.. model.Declarations.Select(declaration => new DeclarationFactDto(
            declaration.Symbol.Uri, declaration.Name, declaration.Kind, declaration.Access, declaration.IsImplicit, declaration.Location, declaration.References, declaration.Role,
            declaration.DeclaredBy, declaration.Hides, declaration.ReferencedFrom, declaration.TypeKind, declaration.IsArray,
            declaration.AutoInstantiates?.Uri, declaration.CompiledOn, declaration.StatementsCompiledOn.IsDefault ? null : declaration.StatementsCompiledOn,
            declaration.Binding is { } binding
                ? new DeclarationBindingDto(
                    binding.Source.Uri, binding.Bound?.Uri, binding.BoundLibrary, binding.IsBoundInSourceProject, binding.IsSourceAControl)
                : null,
            declaration.IsUsedByReferencingProjects, declaration.IsReferredToByABoundMember, declaration.IsWrittenByABoundMember))],
        model.Language,
        model.Class,
        model.Document,
        model.Classes.IsDefaultOrEmpty ? null : model.Classes,
        model.DocumentModules.IsDefaultOrEmpty ? null : model.DocumentModules,
        model.UndefinedConditionalConstants.IsDefaultOrEmpty ? null : model.UndefinedConditionalConstants,
        model.ProjectConditionalConstants.IsDefaultOrEmpty ? null : model.ProjectConditionalConstants,
        model.Win64OnlySetOnWindows,
        model.Expressions.IsEmpty ? null : ImmutableArray.CreateRange(model.Expressions.Values.Select(ExpressionFactDto.From)),
        model.Directives);

    private static CompileErrorDto ErrorOf(VBCompileErrorInfo error) => CompileErrorDto.From(error);
}
