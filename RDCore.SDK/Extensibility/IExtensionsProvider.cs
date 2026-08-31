namespace RDCore.SDK.Extensibility;

/// <summary>
/// A service that can discover and manage platform extensions.
/// </summary>
public interface IExtensionsProvider
{
    /// <summary>
    /// Creates a serializable <see cref="ExtensionInfo"/> model for the specified extension executable <c>name</c>, with the specified <c>description</c>.
    /// </summary>
    /// <param name="name">The name of the executable extension.</param>
    /// <param name="description">A short description of the extension.</param>
    /// <remarks>
    /// 🧩 The implementation must validate that it is executing this method inside the target extension folder.
    /// </remarks>
    /// <returns><c>null</c> if the specified extension cannot be described.</returns>
    ExtensionInfo? Describe(string name, string description);

    /// <summary>
    /// Scans the <em>extensions folder</em> for subfolders containing an <em>extension manifest</em>.
    /// </summary>
    IEnumerable<ExtensionInfo> Discover();
    /// <summary>
    /// Enables the specified <see cref="ExtensionInfo"/> if the manifest and associated executable pass validation.
    /// </summary>
    /// <param name="extension">The deserialized <em>manifest</em> of the extension to enable.</param>
    /// <returns><c>true</c> if the specified extension <strong>passed validation</strong> and was enabled, <c>false</c> otherwise.</returns>
    bool Allow(ExtensionInfo extension);
    /// <summary>
    /// Blocks loading the specified <see cref="ExtensionInfo"/>.
    /// </summary>
    /// <param name="extension">The deserialized <em>manifest</em> of the extension to block.</param>
    /// <returns><c>true</c> if the specified extension could be blocked, <c>false</c> otherwise.</returns>
    bool Block(ExtensionInfo extension);
}
