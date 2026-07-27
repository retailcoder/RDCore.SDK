namespace RDCore.SDK.Model;

/// <summary>
/// Describes the kind of parameter.
/// </summary>
public enum ParameterKind
{
    /// <summary>
    /// The parameter is implicitly declared as being passed by reference (implicit default).
    /// </summary>
    ImplicitByRef,
    /// <summary>
    /// The parameter is explicitly declared as being passed by reference (<c>ByRef</c>).
    /// </summary>
    /// <remarks>
    /// If the member is a <c>Property Let</c> and <c>Property Set</c> declaration, semantics work <c>ByVal</c> regardless.
    /// </remarks>
    ExplicitByRef,
    /// <summary>
    /// The parameter is explicitly declared as being passed by value (<c>ByVal</c>).
    /// </summary>
    ExplicitByVal,
    /// <summary>
    /// The parameter is implicitly declared as being passed by value (<c>ByVal</c>).
    /// </summary>
    /// <remarks>
    /// This is only applicable for the value paraemter of <c>Property Let</c> and <c>Property Set</c> declarations.
    /// </remarks>
    ImplicitByVal,
}
