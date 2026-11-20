using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Runtime;
using System.Collections.Immutable;

namespace RDCore.SDK.Semantics;

/// <summary>
/// What a declaration declares.
/// </summary>
public enum DeclarationKind
{
    /// <summary>
    /// A variable: a local, or a field of a module.
    /// </summary>
    Variable,

    /// <summary>
    /// A parameter of a procedure.
    /// </summary>
    Parameter,

    /// <summary>
    /// A constant.
    /// </summary>
    Constant,

    /// <summary>
    /// A <c>Sub</c> or a <c>Function</c>.
    /// </summary>
    Procedure,

    /// <summary>
    /// A <c>Property</c>, which its <c>Get</c>, <c>Let</c> and <c>Set</c> accessors declare as one.
    /// </summary>
    Property,

    /// <summary>
    /// An <c>Event</c>.
    /// </summary>
    Event,

    /// <summary>
    /// A member of a user-defined type (<strong>MS-VBAL §5.2.3.3</strong>): <c>Name</c>, declared <c>Name As String</c> in a <c>Type Person</c> block. It is referred to as
    /// the member of a variable of the type (<c>p.Name</c>).
    /// </summary>
    TypeMember,

    /// <summary>
    /// A control of a form, which the form's designer declares as a member of the form (<c>Text1</c> of <c>Form1</c>), and not a statement of its code. It is
    /// referred to by its name in the form's code, and as a member of the form elsewhere (<c>Form1.Text1</c>).
    /// </summary>
    Control,
}

/// <summary>
/// What one reference to a declaration does, counted the same way as <see cref="DeclarationReferences"/>.
/// </summary>
public enum DeclarationReferenceKind
{
    /// <summary>
    /// It refers to the declaration for its value, or to call it (<see cref="DeclarationReferences.Reads"/>).
    /// </summary>
    Read,

    /// <summary>
    /// It writes to it (<see cref="DeclarationReferences.Writes"/>).
    /// </summary>
    Write,

    /// <summary>
    /// It passes it by reference to a call that may assign it (<see cref="DeclarationReferences.PassedAsArguments"/>).
    /// </summary>
    PassedAsArgument,
}

/// <summary>
/// One place the code of the project refers to a declaration (<see cref="DeclarationFact.ReferencedFrom"/>).
/// </summary>
/// <param name="Location">Where the reference is written: the module, and the range of the name.</param>
/// <param name="Procedure">
/// The procedure whose code the reference is in; <see langword="null"/> for a reference in the declarations of a module, such as the value of a constant or
/// the bounds of an array of the module.
/// </param>
/// <param name="Kind">What the reference does.</param>
public sealed record class DeclarationReference(SourceLocation Location, Uri? Procedure, DeclarationReferenceKind Kind)
{
    /// <summary>
    /// Whether the reference calls a <c>Function</c> as a statement, so that nothing uses the value it returns
    /// (<see cref="Flags.ValueExpressionSemanticFlags.ResultDiscarded"/>).
    /// </summary>
    public bool DiscardsResult { get; init; }

    /// <summary>
    /// Whether the reference is an argument (<see cref="DeclarationReferenceKind.PassedAsArgument"/>) that the call is known to assign: the host followed the
    /// procedure, and it assigns the <c>ByRef</c> parameter or passes it on by reference to a procedure that does (<strong>MS-VBAL §5.3.1.11</strong>).
    /// <see langword="false"/> for an argument of a procedure the host does not follow, which may or may not assign it, and for any other reference.
    /// </summary>
    public bool AssignedThroughCall { get; init; }
}

/// <summary>
/// The kind of the declared type of a declaration or of an expression (<strong>MS-VBAL §2.1</strong>, <strong>§5.2.3.1.4</strong>): what a value of it
/// is, which decides how it is assigned and what it holds before it is (<see cref="DeclaredTypeKinds.Of"/>).
/// </summary>
public enum DeclaredTypeKind
{
    /// <summary>
    /// A type of the language other than <c>Variant</c> and <c>Object</c>: a number, <c>Boolean</c>, <c>Date</c>, a <c>String</c> of either length, or a
    /// pointer-sized integer (<c>LongPtr</c>). It is let-assigned, and holds its type's default value until it is (<strong>MS-VBAL §2.3</strong>).
    /// </summary>
    Intrinsic,

    /// <summary>
    /// <c>Variant</c>, which can hold a value of any type, an object, or none (<c>Empty</c>).
    /// </summary>
    Variant,

    /// <summary>
    /// <c>Object</c>: a reference to an object of any class, bound late, assigned with <c>Set</c>, and <c>Nothing</c> until it is.
    /// </summary>
    Object,

    /// <summary>
    /// A specific class, of the workspace or of a library, or an interface: a reference to an object of it, assigned with <c>Set</c>, and <c>Nothing</c>
    /// until it is, unless it is declared <c>As New</c> (<strong>MS-VBAL §2.5.1</strong>).
    /// </summary>
    Class,

    /// <summary>
    /// A user-defined type (<c>Type</c>, <strong>MS-VBAL §5.2.3.3</strong>): a record that holds its fields, each assigned on its own or the record whole.
    /// </summary>
    UserDefinedType,

    /// <summary>
    /// An <c>Enum</c> (<strong>MS-VBAL §5.2.3.4</strong>), whose values are <c>Long</c>: let-assigned, and <c>0</c> until it is.
    /// </summary>
    Enum,
}

/// <summary>
/// Every reference to a declaration there is, counted by what the reference does.
/// </summary>
/// <remarks>
/// These are the references, all of them: a count is only ever stated for a declaration that nothing outside the code the pass analyzed can refer to
/// (<see cref="DeclarationFact.References"/>). An argument that a call may assign is a reference of its own kind, because what it does is not what the
/// expression says (<strong>MS-VBAL §5.3.1.11</strong>): a procedure assigns it through a <c>ByRef</c> parameter that it writes to, or passes on by reference
/// to a procedure that does. Where the host follows the procedure and finds that nothing assigns the parameter, or the parameter is <c>ByVal</c>, the
/// argument is a read.
/// </remarks>
/// <param name="Reads">How many expressions refer to it for its value, or to call it.</param>
/// <param name="Writes">How many expressions write to it (<see cref="Flags.ValueExpressionSemanticFlags.AssignmentTarget"/>).</param>
/// <param name="PassedAsArguments">
/// How many expressions pass it by reference to a call that may assign it: a procedure that assigns the parameter, or passes it on by reference to one that
/// does, or a procedure the host does not follow, such as one of a library or one called through an object
/// (<see cref="Flags.ValueExpressionSemanticFlags.PassedAsArgument"/>).
/// </param>
public readonly record struct DeclarationReferences(int Reads, int Writes, int PassedAsArguments)
{
    /// <summary>
    /// The number of references of any kind.
    /// </summary>
    public int Total => Reads + Writes + PassedAsArguments;
}

/// <summary>
/// What a member of a class module is there for, when it is not there on its own account.
/// </summary>
public enum DeclarationRole
{
    /// <summary>
    /// A member like any other, or one whose role the pass could not tell.
    /// </summary>
    None,

    /// <summary>
    /// It implements a member of an interface the class module <c>Implements</c>, by being named <c>InterfaceName_MemberName</c> (<strong>MS-VBAL §5.3.1.9</strong>).
    /// </summary>
    InterfaceImplementation,

    /// <summary>
    /// It handles an event, which calls it by its name:
    /// <list type="bullet">
    /// <item>an event of a <c>WithEvents</c> variable of the class module, or of a form's control, named <c>VariableName_EventName</c>
    /// (<strong>MS-VBAL §5.3.1.8</strong>);</item>
    /// <item>an event of a form itself, named for the class its designer hosts (<c>UserForm_Initialize</c>, <c>Form_Load</c>);</item>
    /// <item>the class's own <c>Initialize</c> or <c>Terminate</c>: a <c>Class_Initialize</c> or <c>Class_Terminate</c> subroutine with no parameters, of a
    /// class module that is not a form (<strong>MS-VBAL §5.3.1.10</strong>);</item>
    /// <item>an event of a document module's host class, named for that class (<c>Workbook_Open</c>, <c>Worksheet_Change</c>), which the host application
    /// raises.</item>
    /// </list>
    /// </summary>
    EventHandler,

    /// <summary>
    /// It is named as the handler of an event of one of a form's controls (<c>TextBox1_Change</c>), and does not match the event: it is not a subroutine, or
    /// its parameter list is not compatible with the event's. MS-VBA compiles it, and the event never calls it: it is dead code, unless the module's own code
    /// calls it as the procedure it also is.
    /// </summary>
    UnmatchedEventHandler,

    /// <summary>
    /// It is the procedure a run of the project starts with (<see cref="Workspace.ProgramEntryPoint"/>): the procedure the project names, or the
    /// <c>Sub Main</c> of the one standard module that declares one. A run calls it, and the project's code need not. A library project has none.
    /// </summary>
    EntryPoint,
}

/// <summary>
/// What the static pass found out about a declaration.
/// </summary>
/// <remarks>
/// A fact is stated only when it is true. The code the pass analyzed is every module of the project, so a declaration that another module can refer to is
/// counted over all of them.
/// <para>
/// Some declarations are referred to by code the pass did not analyze, or by code that does not name them:
/// </para>
/// <list type="bullet">
/// <item>a declaration that code outside the project can refer to: a <c>Public</c> member of a standard module without <c>Option Private Module</c>, or of a
/// class that another project can use. In a library project it counts as used (<see cref="IsUsedByReferencingProjects"/>). A program, whose run starts at
/// its entry point (<see cref="DeclarationRole.EntryPoint"/>), is referred to by no other project: the references to its <c>Public</c> members are
/// counted over its own modules;</item>
/// <item>a declaration called through what it is bound to (<see cref="Binding"/>) or through the object it is a member of: an event handler, a member that
/// implements an interface;</item>
/// <item>a declaration named in a string, or reached by a member access that is bound only when the code runs.</item>
/// </list>
/// <para>
/// Their references are not counted: <see cref="References"/> is then <see langword="null"/>, which says the references are not known and not that there are
/// none.
/// </para>
/// </remarks>
/// <param name="Symbol">The identity of the declared symbol.</param>
/// <param name="Name">The name it is declared with.</param>
/// <param name="Kind">What it declares.</param>
/// <param name="Access">Who can refer to it besides the module's own code.</param>
/// <param name="IsImplicit">Whether it was never declared: it came into being because something referred to it, which is legal and worth reporting.</param>
/// <param name="Location">Where it is declared.</param>
/// <param name="References">
/// Every reference to it, when every reference to it is in the code that was analyzed and was analyzed completely; <see langword="null"/> otherwise.
/// </param>
public sealed record class DeclarationFact(
    SemanticId Symbol,
    string Name,
    DeclarationKind Kind,
    AccessModifier Access,
    bool IsImplicit,
    SourceLocation Location,
    DeclarationReferences? References)
{
    /// <summary>
    /// What the declaration is there for, when it implements a member of an interface, handles an event, or is the project's entry point. A role is stated
    /// only for a member of a class module whose interfaces and event sources the pass resolved, and for the entry point of a project that has one;
    /// <see cref="DeclarationRole.None"/> is not a statement that it has none otherwise.
    /// </summary>
    public DeclarationRole Role { get; init; }

    /// <summary>
    /// Whether the declaration counts as used by the projects that reference this one, whether or not one does: a member of a library project
    /// (<see cref="Workspace.ProjectOutputType.Library"/>) that a referencing project can name, or reach through an object of its class
    /// (<strong>MS-VBAL §5.3.1.1</strong>). <see cref="References"/> is then <see langword="null"/>: what those projects refer to is not known.
    /// </summary>
    public bool IsUsedByReferencingProjects { get; init; }

    /// <summary>
    /// The event and its source, or the interface member and its interface, that the declaration is bound to by its <see cref="Role"/>
    /// (<strong>MS-VBAL §5.3.1.8</strong>, <strong>§5.3.1.9</strong>, <strong>§5.3.1.10</strong>). Stated with <see cref="DeclarationRole.EventHandler"/>,
    /// <see cref="DeclarationRole.UnmatchedEventHandler"/> and <see cref="DeclarationRole.InterfaceImplementation"/>; <see langword="null"/> for any other
    /// role.
    /// </summary>
    public DeclarationBinding? Binding { get; init; }

    /// <summary>
    /// How a local variable came to be declared: a <c>Dim</c> or a <c>Static</c> statement, a <c>ReDim</c> statement whose name matched nothing
    /// (<strong>MS-VBAL §5.4.3.3</strong>), or a reference to a name that matched nothing (§5.6.10). <see langword="null"/> for any other declaration.
    /// </summary>
    public LocalDeclarationKind? DeclaredBy { get; init; }

    /// <summary>
    /// What the declaration hides: what its name would mean where the declaration is in scope, had it not been declared (<strong>MS-VBAL §5.6.10</strong>
    /// matches a name in the procedure first, then in the module, then in the project and the libraries it references). A local or a parameter hides what
    /// the name means in the rest of the module; a declaration of a module can hide one of another module or of a library (<c>Public Function Replace</c>
    /// hides <c>VBA.Strings.Replace</c>). <see langword="null"/> when the name means nothing else, or when the host does not look for what the declaration hides.
    /// </summary>
    public HiddenDeclaration? Hides { get; init; }

    /// <summary>
    /// Each reference to the declaration, in any module of the project: one for each that <see cref="References"/> counts.
    /// <see langword="null"/> exactly when <see cref="References"/> is.
    /// </summary>
    public ImmutableArray<DeclarationReference>? ReferencedFrom { get; init; }

    /// <summary>
    /// The class whose object the variable is given where code first uses it, for a variable declared <c>As New</c>, or an array whose elements are
    /// (<strong>MS-VBAL §5.2.3.1.4</strong>): <see cref="ModuleSemanticModel.Classes"/> says what is known of creating its objects. <see langword="null"/>
    /// for any other declaration.
    /// </summary>
    public SemanticId? AutoInstantiates { get; init; }

    /// <summary>
    /// The kind of type it is declared as, or for an array the kind of its elements' type (<see cref="IsArray"/>): the type its <c>As</c> clause names, the
    /// type a type-declaration character or a <c>Def</c><em>Type</em> directive gives it, or <c>Variant</c> (<strong>MS-VBAL §5.2.3.1.4</strong>,
    /// <strong>§5.2.3.1.5</strong>). For a function or a property, the type it returns. <see langword="null"/> for a declaration that has no type (a
    /// <c>Sub</c>, an <c>Event</c>), and for one whose type did not resolve.
    /// </summary>
    public DeclaredTypeKind? TypeKind { get; init; }

    /// <summary>
    /// Whether it is declared as an array: with bounds, with an empty list of them (<c>Dim a() As Long</c>), or by a <c>ReDim</c> statement
    /// (<strong>MS-VBAL §5.4.3.3</strong>). <see cref="TypeKind"/> is then the kind of its elements' type. A <c>Variant</c> that holds an array is not one.
    /// </summary>
    public bool IsArray { get; init; }

    /// <summary>
    /// For a procedure declared by a <c>Declare</c> statement (<strong>MS-VBAL §5.2.3.5</strong>), the platforms it is declared on: the union of those of
    /// each of its statements (<see cref="StatementsCompiledOn"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A procedure written once in each branch of a <c>#If</c> is one procedure, which each platform declares with the statement of its own live branch. A
    /// statement compiled on every platform calls its one library whatever the platform; a procedure declared on every platform by several statements, each
    /// on some of them, does not: each platform calls the library of its own statement.
    /// </para>
    /// <para>
    /// The host finds it by evaluating the module's <c>#If</c> and <c>#ElseIf</c> conditions once for each platform of
    /// <see cref="CompiledPlatformProfiles.Each"/>, with the predefined constants of that platform
    /// (<see cref="PredefinedConditionalConstants.Of(RDCore.SDK.Runtime.Abstract.Execution.IRuntimeEnvironmentProfile)"/>) and every other constant as
    /// the session has it: not only under the constants of the session that analyzed the code, which are those of the one platform it runs on.
    /// <see langword="null"/> for any other declaration, and where a condition one of its statements depends on cannot be evaluated.
    /// </para>
    /// </remarks>
    public CompiledPlatforms? CompiledOn { get; init; }

    /// <summary>
    /// For a procedure declared by <c>Declare</c> statements (<strong>MS-VBAL §5.2.3.5</strong>), the platforms each of them is compiled on, in the order they
    /// are written: those whose predefined conditional-compilation constants make live the <c>#If</c> branch the statement is written in
    /// (<strong>MS-VBAL §3.4.2</strong>). <see cref="CompiledPlatforms.All"/> for a statement outside any <c>#If</c> block.
    /// </summary>
    /// <remarks>
    /// One element for a procedure that one statement declares; one for each statement of a procedure written once in each branch of a <c>#If</c>, whose
    /// <see cref="CompiledOn"/> is their union. The host finds them as it finds <see cref="CompiledOn"/>. Default for any other declaration, and where
    /// <see cref="CompiledOn"/> is <see langword="null"/>.
    /// </remarks>
    public ImmutableArray<CompiledPlatforms> StatementsCompiledOn { get; init; }
}

/// <summary>
/// A declaration that another declaration hides (<see cref="DeclarationFact.Hides"/>).
/// </summary>
/// <param name="Symbol">The identity of the declaration hidden.</param>
/// <param name="QualifiedName">Its name, after the module or the library that declares it: <c>Module1.Count</c>, <c>VBA.Strings.Left</c>.</param>
public sealed record class HiddenDeclaration(Uri Symbol, string QualifiedName);
