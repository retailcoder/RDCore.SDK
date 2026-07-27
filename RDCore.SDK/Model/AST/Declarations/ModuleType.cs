namespace RDCore.SDK.Model.AST.Declarations;

/// <summary>
/// Describes the type of module an AST is for.
/// </summary>
public enum ModuleType
{
    /// <summary>
    /// Root node is for a standard (procedural) module.
    /// </summary>
    StdModule,
    /// <summary>
    /// Root node is for a class module.
    /// </summary>
    ClassModule,
}
