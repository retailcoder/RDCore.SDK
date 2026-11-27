using System.Globalization;
using System.Reflection;
using System.Resources;
using RDCore.SDK.Server.Configuration;

namespace RDCore.SDK.Extensibility.Annotations;

/// <summary>
/// Says which annotations an extension provides, and where they are described: the registration point of an extension's annotations.
/// </summary>
/// <remarks>
/// <para>
/// An annotation is described by the string of its name in <see cref="DescriptionsResource"/>, and each of its parameters by the annotation's name and the
/// parameter's joined by an underscore (<see cref="AnnotationSet.KeyOf(AnnotationDeclaration, AnnotationParameter)"/>), in each language of the platform
/// (<see cref="SettingsSchema.Languages"/>).
/// </para>
/// <para>
/// What this says is written into the extension's manifest when it is described (<see cref="IExtensionsProvider.Describe"/>,
/// <see cref="ExtensionInfo.Annotations"/>), so that a catalogue made from the manifests (<see cref="AnnotationCatalogue"/>) knows every extension's
/// annotations without starting any of them: an extension that is slow to start, or that has stopped, does not make its annotations unknown.
/// </para>
/// </remarks>
/// <param name="declarationsType">
/// A type of the extension's assembly, with a parameterless constructor, that implements <see cref="IAnnotationDeclarations"/>.
/// </param>
/// <param name="descriptionsResource">
/// The base name of the resources of the extension's assembly that describe them: <c>RDCore.Diagnostics.AnnotationDescriptions</c>.
/// </param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ExtensionAnnotationsAttribute(Type declarationsType, string descriptionsResource) : Attribute
{
    /// <summary>
    /// The type that declares the extension's annotations.
    /// </summary>
    public Type DeclarationsType { get; } = declarationsType;

    /// <summary>
    /// The base name of the resources of the extension's assembly that describe the annotations.
    /// </summary>
    public string DescriptionsResource { get; } = descriptionsResource;

    /// <summary>
    /// The annotations an extension's assembly says it provides, with their descriptions in each language of the platform; a description the extension has
    /// none of in a language is the one its resources fall back on.
    /// </summary>
    /// <param name="assembly">The extension's assembly.</param>
    /// <returns>The annotations, or <see langword="null"/> for an assembly that says nothing of any.</returns>
    /// <exception cref="InvalidOperationException">
    /// The attribute names a type that does not implement <see cref="IAnnotationDeclarations"/>, or that has no parameterless constructor.
    /// </exception>
    /// <exception cref="MissingManifestResourceException">The assembly has no resources of the name the attribute gives.</exception>
    public static AnnotationSet? Read(Assembly assembly)
    {
        if (assembly.GetCustomAttribute<ExtensionAnnotationsAttribute>() is not { } declared)
        {
            return null;
        }

        if (!typeof(IAnnotationDeclarations).IsAssignableFrom(declared.DeclarationsType) || declared.DeclarationsType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, Exceptions.ExtensionAnnotations_NoDeclarations,
                    declared.DeclarationsType.FullName, nameof(IAnnotationDeclarations)));
        }

        var declarations = ((IAnnotationDeclarations)Activator.CreateInstance(declared.DeclarationsType)!).Declarations.ToArray();
        var resources = new ResourceManager(declared.DescriptionsResource, assembly);
        var keys = declarations.SelectMany(annotation => annotation.Parameters.Select(parameter => AnnotationSet.KeyOf(annotation, parameter))
            .Prepend(AnnotationSet.KeyOf(annotation))).ToArray();
        return new AnnotationSet
        {
            Declarations = declarations,
            Descriptions = SettingsSchema.Languages.ToDictionary(
                language => language,
                language => keys
                    .Select(key => (Key: key, Text: resources.GetString(key, CultureInfo.GetCultureInfo(language))))
                    .Where(described => described.Text is not null)
                    .ToDictionary(described => described.Key, described => described.Text!)),
        };
    }
}
