namespace RDCore.SDK.Server.Configuration;

/// <summary>
/// The settings of a workspace: the lists that apply to that workspace only, for whoever opens it, such as the codes of the diagnostics it disables
/// (<c>Analysis.DisabledDiagnostics</c>).
/// </summary>
/// <remarks>
/// <para>
/// A setting of a workspace adds to the user's setting of the platform: a code either one disables is disabled (<see cref="SdkAnalysisOptions.DisabledCodesWith"/>).
/// What a workspace keeps is checked as the user's settings are: an item that is no value of the setting is neither kept nor read.
/// </para>
/// <para>
/// Where a workspace keeps its settings is the implementation's to say. A client changes them as a change of its own, as it changes the user's settings: the
/// language server asks it to (<c>rdcore/settings/listValue</c>).
/// </para>
/// </remarks>
public interface IWorkspaceSettings
{
    /// <summary>
    /// The items of the list a workspace has for a setting; none when it has none.
    /// </summary>
    /// <param name="workspaceRoot">The root folder of the workspace.</param>
    /// <param name="key">The key of the setting, without regard to case: <c>Analysis.DisabledDiagnostics</c>.</param>
    IReadOnlyList<string> Read(Uri workspaceRoot, string key);

    /// <summary>
    /// Adds items to the list a workspace has for a setting whose value is a list, as <see cref="UserSettingsStore.Add"/> adds them to the user's.
    /// </summary>
    /// <param name="workspaceRoot">The root folder of the workspace.</param>
    /// <param name="key">The key of the setting, without regard to case.</param>
    /// <param name="value">The item, or items, as they are written: <c>RDC01001</c>.</param>
    /// <returns>What became of the value.</returns>
    UserSettingChange Add(Uri workspaceRoot, string key, string value);

    /// <summary>
    /// Removes items from the list a workspace has for a setting whose value is a list, as <see cref="UserSettingsStore.Remove"/> removes them from the user's.
    /// </summary>
    /// <param name="workspaceRoot">The root folder of the workspace.</param>
    /// <param name="key">The key of the setting, without regard to case.</param>
    /// <param name="value">The item, or items, as they are written: <c>RDC01001</c>.</param>
    /// <returns>What became of the value.</returns>
    UserSettingChange Remove(Uri workspaceRoot, string key, string value);
}
