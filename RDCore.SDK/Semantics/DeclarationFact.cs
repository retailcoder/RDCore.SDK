using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
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
    /// It handles an event of a <c>WithEvents</c> variable of the class module, by being named <c>VariableName_EventName</c> (<strong>MS-VBAL §5.3.1.8</strong>).
    /// </summary>
    EventHandler,

    /// <summary>
    /// It is named as the handler of an event of one of a form's controls (<c>TextBox1_Change</c>), and does not match the event: it is not a subroutine, or
    /// its parameter list is not compatible with the event's. MS-VBA compiles it, and the event never calls it: it is dead code, unless the module's own code
    /// calls it as the procedure it also is.
    /// </summary>
    UnmatchedEventHandler,
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
/// <item>a declaration that the projects referencing this one can refer to: a <c>Public</c> member of a standard module without
/// <c>Option Private Module</c>, or of a class that another project can use;</item>
/// <item>a declaration called by convention or through the object it is a member of: an event handler, a member that implements an interface;</item>
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
    /// What the declaration is there for, when it implements a member of an interface or handles an event. A role is stated only for a member of a class
    /// module whose interfaces and event sources the pass resolved; <see cref="DeclarationRole.None"/> is not a statement that it has none otherwise.
    /// </summary>
    public DeclarationRole Role { get; init; }

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
    /// Whether it is a variable declared <c>As New</c> of a class whose objects are never created (<c>Dim e As New ErrObject</c>): <strong>MS-VBA</strong>
    /// compiles the declaration, and every reference to the variable that creates its object raises run-time error -2147221164 <em>Class not
    /// registered</em> (recorded in Excel: <c>oracle\s62</c> X06, X07, X09). Assigning it <c>Nothing</c> creates nothing.
    /// </summary>
    public bool AutoInstantiationAlwaysFails { get; init; }
}

/// <summary>
/// A declaration that another declaration hides (<see cref="DeclarationFact.Hides"/>).
/// </summary>
/// <param name="Symbol">The identity of the declaration hidden.</param>
/// <param name="QualifiedName">Its name, after the module or the library that declares it: <c>Module1.Count</c>, <c>VBA.Strings.Left</c>.</param>
public sealed record class HiddenDeclaration(Uri Symbol, string QualifiedName);
