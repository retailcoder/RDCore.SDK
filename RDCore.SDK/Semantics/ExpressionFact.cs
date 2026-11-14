using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.Errors;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Semantics.Flags;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Semantics;

/// <summary>
/// What an expression is classified as (<strong>MS-VBAL §5.6.1</strong>): what it names, which decides what may be done with it.
/// </summary>
public enum ExpressionClassification
{
    /// <summary>
    /// What it is could not be told: a name that did not resolve, or a kind of expression the static pass does not classify yet.
    /// </summary>
    Unknown,

    /// <summary>
    /// A value, which is not something that can be assigned to: a literal, the result of an operator, of a call or of an index.
    /// </summary>
    Value,

    /// <summary>
    /// A variable: a local, a parameter, a field, which has storage and can be assigned to.
    /// </summary>
    Variable,

    /// <summary>
    /// A constant: its value is known where it is written, and is substituted there.
    /// </summary>
    Constant,

    /// <summary>
    /// A <c>Function</c>.
    /// </summary>
    Function,

    /// <summary>
    /// A <c>Property</c>.
    /// </summary>
    Property,

    /// <summary>
    /// A <c>Sub</c>.
    /// </summary>
    Subroutine,

    /// <summary>
    /// A type: a class, a <c>Type</c> or an <c>Enum</c>.
    /// </summary>
    Type,

    /// <summary>
    /// A project or a procedural module, which a member is looked up in (<strong>MS-VBAL §5.6.12</strong>).
    /// </summary>
    Namespace,

    /// <summary>
    /// A member of an object whose type does not say what it has: it is bound when the expression runs.
    /// </summary>
    UnboundMember,
}

/// <summary>
/// What the static pass found out about one expression.
/// </summary>
/// <remarks>
/// A fact is a description of the expression, not an opinion of it: whether a late-bound member, a name written in the wrong case or a constant condition
/// is worth a diagnostic is for an analyzer to say.
/// </remarks>
/// <param name="Node">The expression the fact describes.</param>
/// <param name="Location">Where the expression is written.</param>
/// <param name="DeclaredType">The declared type of the expression (<strong>RD-VBAL §5.0.1</strong>), or <see langword="null"/> when it is an error.</param>
/// <param name="Classification">What the expression names.</param>
/// <param name="Binding">The symbol the expression refers to, when it refers to one that resolved.</param>
/// <param name="Flags">What else is the case of it.</param>
/// <param name="Error">The compile error of the expression itself or of the first of its operands that has one, when it has.</param>
public sealed record class ExpressionFact(
    SyntaxNodeId Node,
    SourceLocation Location,
    VBType? DeclaredType,
    ExpressionClassification Classification,
    SemanticId? Binding,
    ValueExpressionSemanticFlags Flags,
    VBCompileErrorInfo? Error = null)
{
    /// <summary>
    /// The class of the object the expression creates, when it is a <c>CreateObject</c> call given a constant programmatic identifier of a class that is
    /// described: a fact for tooling only, never a declaration (<see cref="ObjectCreationFact"/>). <see langword="null"/> otherwise.
    /// </summary>
    public ObjectCreationFact? Creates { get; init; }

    /// <summary>
    /// The constant binding of the constant the expression names (<strong>MS-VBAL §5.2.3.2</strong>): the value of its constant expression, Let-coerced to
    /// its declared type. <c>DEV</c> is <c>1</c> with <c>Const DEV = 1</c>, and <c>True</c> with <c>Const DEV As Boolean = 1</c>. It is known where the
    /// constant expression is a literal, a negated literal, or the name of another constant whose binding is known. <see langword="null"/> for an expression
    /// that names no constant, and for a constant whose binding is not known.
    /// </summary>
    public VBTypedValue? ConstantValue { get; init; }

    /// <summary>
    /// The accessors of the property the expression names (<strong>MS-VBAL §5.3.1.7</strong>): those its module declares under the name, or, for a member
    /// of an object, those its class declares. <see cref="PropertyAccessors.None"/> for an expression that names no property.
    /// </summary>
    public PropertyAccessors Accessors { get; init; }

    /// <summary>
    /// The declared type of the value parameter of the <c>Property Let</c> of the property the expression names: its last parameter, which a <c>Let</c> of the
    /// property gives the value. <see langword="null"/> for an expression that names no property with a <c>Property Let</c>.
    /// </summary>
    public VBType? LetValueType { get; init; }

    /// <summary>
    /// The default member of the class the expression's declared type is (<see cref="DefaultMemberFact"/>); <see langword="null"/> for an expression of
    /// any other type, and of a class that has no default member.
    /// </summary>
    public DefaultMemberFact? DefaultMember { get; init; }

    /// <summary>
    /// Whether the expression's value is taken and Let-coerced to a declared type (<strong>MS-VBAL §5.5.1.1</strong>): it is an operand of an operator other
    /// than <c>Is</c>, the value of a <c>Let</c> assignment, or an argument given to a parameter declared with a value type. An object stands there for the
    /// value of its default member. An argument given to a parameter declared <c>Variant</c> is passed as it is, and a variable of a <c>ByRef</c> parameter's
    /// own type is passed by reference (<strong>MS-VBAL §5.3.1.11</strong>): neither is Let-coerced.
    /// </summary>
    public bool IsLetCoerced { get; init; }
}

/// <summary>
/// Takes the facts of the expressions the static pass evaluates.
/// </summary>
public interface IExpressionFactSink
{
    /// <summary>
    /// Records what is known of an expression. An expression that is evaluated again has its fact replaced.
    /// </summary>
    /// <param name="fact">The fact.</param>
    void Record(ExpressionFact fact);

    /// <summary>
    /// Gets the fact of an expression the pass has evaluated, which it does for an operand before the expression that has it.
    /// </summary>
    /// <param name="node">The expression.</param>
    /// <param name="fact">Its fact.</param>
    bool TryGet(SyntaxNodeId node, [NotNullWhen(true)] out ExpressionFact? fact);
}

/// <summary>
/// An <see cref="IExpressionFactSink"/> that keeps what it is given, by the expression.
/// </summary>
public sealed class ExpressionFactCollector : IExpressionFactSink
{
    private readonly Dictionary<SyntaxNodeId, ExpressionFact> _facts = [];

    /// <inheritdoc/>
    public void Record(ExpressionFact fact) => _facts[fact.Node] = fact;

    /// <inheritdoc/>
    public bool TryGet(SyntaxNodeId node, [NotNullWhen(true)] out ExpressionFact? fact) => _facts.TryGetValue(node, out fact);

    /// <summary>
    /// The facts recorded so far, by expression.
    /// </summary>
    public ImmutableDictionary<SyntaxNodeId, ExpressionFact> ToImmutable() => _facts.ToImmutableDictionary();
}
