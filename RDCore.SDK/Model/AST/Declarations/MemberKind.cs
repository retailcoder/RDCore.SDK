namespace RDCore.SDK.Model.AST.Declarations;

/// <summary>
/// Describes the kind of member represented by a member AST node.
/// </summary>
public enum MemberKind
{
    /// <summary>
    /// Member is a <c>Sub</c> procedure declaration.
    /// </summary>
    Procedure,
    /// <summary>
    /// Member is a <c>Declare Sub</c> procedure declaration.
    /// </summary>
    ExternalProcedure,
    /// <summary>
    /// Member is a <c>Function</c> procedure declaration.
    /// </summary>
    Function,
    /// <summary>
    /// Member is a <c>Declare Function</c> procedure declaration.
    /// </summary>
    ExternalFunction,
    /// <summary>
    /// Member is a <c>Property Get</c> procedure declaration.
    /// </summary>
    PropertyGet,
    /// <summary>
    /// Member is a <c>Property Let</c> procedure declaration.
    /// </summary>
    PropertyLet,
    /// <summary>
    /// Member is a <c>Property Set</c> procedure declaration.
    /// </summary>
    PropertySet,
    /// <summary>
    /// Member is an <c>Enum</c> declaration.
    /// </summary>
    Enum,
    /// <summary>
    /// Member is an <c>Event</c> declaration.
    /// </summary>
    Event,
    /// <summary>
    /// Member is a module-scoped variable declaration.
    /// </summary>
    ModuleField,
    /// <summary>
    /// Member is a <c>Type</c> declaration.
    /// </summary>
    UserDefinedType,
    /// <summary>
    /// Member declares a member (field) of a user-defined <c>Type</c> declaration.
    /// </summary>
    UserDefinedTypeField,
}
