namespace RDCore.SDK.Model.AST.Declarations;

/// <summary>
/// Describes the kind of <c>Const</c> declaration for a statically-valued AST node.
/// </summary>
public enum ConstKind
{
    /// <summary>
    /// Node is a local <c>Const</c> declaration.
    /// </summary>
    Local,
    /// <summary>
    /// Node is a module-scoped <c>Const</c> declaration.
    /// </summary>
    ModuleMember,
    /// <summary>
    /// Node is a member of an <c>Enum</c> declaration.
    /// </summary>
    EnumMember,
}
