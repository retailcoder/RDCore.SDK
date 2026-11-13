using RDCore.SDK.Client;

namespace RDCore.SDK.Platform.Protocol;

/// <summary>
/// Where a setting's value is kept, which says what it applies to.
/// </summary>
public enum SettingScope
{
    /// <summary>
    /// The user's settings of the platform (<c>user.settings.json</c>): every workspace the user opens.
    /// </summary>
    Platform = 0,

    /// <summary>
    /// The settings of the workspace a document is in: that workspace only, for whoever opens it.
    /// </summary>
    Workspace = 1,
}

/// <summary>
/// Request <c>rdcore/settings/listValue</c> (<see cref="RDCorePlatformProtocol.ListSettingValue"/>): asks the client to add an item to the list of a setting,
/// or to remove one from it, in the user's settings of the platform or in the settings of a workspace. The client answers a
/// <see cref="ListSettingValueResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// A client owns its configuration, and the language server writes none of it: this asks the client to make the change as one of its own. The code action that
/// disables a diagnostic, and the one that allows a name a diagnostic is about, send it. The client announces the change as it does any other
/// (<c>workspace/didChangeConfiguration</c>), and the language server sees the setting in the section it pulls.
/// </para>
/// <para>
/// Sent only to a client that expects <see cref="ListSettingValue"/>. A client may decline, answering that it did not apply it.
/// </para>
/// </remarks>
public record class ListSettingValueParams
{
    /// <summary>
    /// The key of the setting, under <c>Configuration</c>: <c>Analysis.DisabledDiagnostics</c>.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// The item, as it is written: <c>RDC01001</c>.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Where the list is kept.
    /// </summary>
    public SettingScope Scope { get; init; }

    /// <summary>
    /// Whether the item is removed from the list; otherwise, it is added.
    /// </summary>
    public bool Remove { get; init; }

    /// <summary>
    /// The document the change was asked for from (the one a diagnostic is in), whose workspace a <see cref="SettingScope.Workspace"/> list is of;
    /// <see langword="null"/> for the client's workspace.
    /// </summary>
    public Uri? Document { get; init; }
}

/// <summary>
/// What a client did with an item it was asked to add to a list, or to remove from it.
/// </summary>
public record class ListSettingValueResult
{
    /// <summary>
    /// Whether the list now holds the item, or does not when it was to be removed: <see langword="false"/> when the client declined, or the item is no value of
    /// the setting.
    /// </summary>
    public bool Applied { get; init; }
}
