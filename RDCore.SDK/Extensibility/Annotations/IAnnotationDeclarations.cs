namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// The annotations an extension provides: the type an extension's <see cref="ExtensionAnnotationsAttribute"/> names, created when the extension is
/// described.
/// </summary>
public interface IAnnotationDeclarations
{
    /// <summary>
    /// The annotations the extension provides, each declared once.
    /// </summary>
    IReadOnlyList<AnnotationDeclaration> Declarations { get; }
}
