namespace RDCore.SDK.Semantics.Flags;

/// <summary>
/// The semantic flags of an expression that yields a value: the facts of how it is bound and what it is written as.
/// </summary>
[Flags]
public enum ValueExpressionSemanticFlags
{
    /// <summary>
    /// The expression is a literal: its value is what is written.
    /// </summary>
    Literal = 1 << 0,

    /// <summary>
    /// The member it names is not one its owner's declared type says it has, and is bound when the expression runs: the owner is a <c>Variant</c> or an <c>Object</c>.
    /// </summary>
    LateBound = 1 << 1,

    /// <summary>
    /// It indexes an object, which is a call of the default member of its class: <c>c(1)</c> is <c>c.Item(1)</c>.
    /// </summary>
    DefaultMember = 1 << 2,

    /// <summary>
    /// It is written relative to the object of an enclosing <c>With</c> block: <c>.Member</c> or <c>!Member</c>.
    /// </summary>
    WithBlockRelative = 1 << 3,

    /// <summary>
    /// It is a dictionary access, <c>owner!Member</c>: a call of the default member of the owner with the name of the member as its argument.
    /// </summary>
    DictionaryAccess = 1 << 4,

    /// <summary>
    /// It invokes a procedure, however it is written: a name with no argument list, or an index expression on a procedure.
    /// </summary>
    ProcedureCall = 1 << 5,

    /// <summary>
    /// The name is written in a different case than the declaration it refers to.
    /// </summary>
    CaseMismatch = 1 << 6,

    /// <summary>
    /// It is the callee of a <c>Call</c> statement written with the <c>Call</c> keyword (<see cref="RDCore.SDK.Model.AST.Statements.CallStatementNode.IsExplicitCall"/>),
    /// the obsolete form of the statement.
    /// </summary>
    ExplicitCallKeyword = 1 << 7,

    /// <summary>
    /// What it names is written to: it is the target of an assignment, the counter or control variable of a loop, the string a <c>Mid</c> statement replaces a
    /// part of, or the array a <c>ReDim</c> gives its dimensions. An element of an array is written through the array, which is what is flagged.
    /// </summary>
    AssignmentTarget = 1 << 8,

    /// <summary>
    /// It is an argument of a call that may take it by reference: what the procedure does with it, reads it or writes to it, is not what the expression says.
    /// An argument that is passed by value, and an index of an array, is not.
    /// </summary>
    PassedAsArgument = 1 << 9,

    /// <summary>
    /// It is the callee of a call statement (<strong>MS-VBAL §5.4.2.1</strong>) that calls a <c>Function</c>: the value the function
    /// returns is discarded.
    /// </summary>
    ResultDiscarded = 1 << 10,

    /// <summary>
    /// What it is bound to (<see cref="ExpressionFact.Binding"/>) is declared by a library that the project references (<strong>MS-VBAL §4.1</strong>): the
    /// standard library, a type library or another project, and not a module of the project. The project's own code is all the static pass analyzes; a library's
    /// members have no <see cref="DeclarationFact"/>.
    /// </summary>
    BoundToLibrary = 1 << 11,

    /// <summary>
    /// What it is bound to (<see cref="ExpressionFact.Binding"/>) is a control of a form: a member of the form's class that the form's designer declares, and
    /// the form's code does not (<see cref="Model.Symbols.Abstract.SymbolProperties.DesignerControl"/>). The form's own code names it directly
    /// (<c>TextBox1</c>); code in another module reaches it through the form (<c>UserForm1.TextBox1</c>). A control has no <see cref="DeclarationFact"/>.
    /// </summary>
    BoundToDesignerControl = 1 << 12,
}
