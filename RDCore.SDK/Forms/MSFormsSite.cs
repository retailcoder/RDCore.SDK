namespace RDCore.SDK.Forms;

/// <summary>
/// What a parent control stores about one of its controls: the control's site, an <c>OleSiteConcreteControl</c>
/// ([MS-OFORMS] 2.2.10.12), with its <c>SITE_FLAG</c> bits decoded ([MS-OFORMS] 2.5.4.1).
/// </summary>
/// <remarks>
/// A property the site does not store has the file format's default: no tag or text, a position of (0, 0), no tab index,
/// and the flags <see cref="TabStop"/>, <see cref="Visible"/>, <see cref="Streamed"/> and <see cref="AutoSize"/> set.
/// </remarks>
/// <param name="Left">The distance from the left edge of the parent's scrollable area to the control, in points
/// (HIMETRIC × 72 / 2540).</param>
/// <param name="Top">The distance from the top edge of the parent's scrollable area to the control, in points.</param>
/// <param name="TabIndex">The control's position in its parent's tab order; <c>-1</c> when the site stores none.</param>
/// <param name="Visible">Whether the control is visible (<c>fVisible</c>).</param>
/// <param name="TabStop">Whether the control takes the focus when the user tabs to it (<c>fTabStop</c>).</param>
/// <param name="Default">Whether the control is its form's default button (<c>fDefault</c>).</param>
/// <param name="Cancel">Whether the control is its form's cancel button (<c>fCancel</c>).</param>
/// <param name="Streamed">Whether the control's data is in its parent's object stream (<c>o</c>), as opposed to a
/// storage of its own (<c>fStreamed</c>).</param>
/// <param name="AutoSize">The site's <c>fAutoSize</c> flag. A control's own <c>AutoSize</c> property is in its
/// <see cref="MSFormsControl.Properties"/>.</param>
/// <param name="PreserveHeight">Whether a <c>ListBox</c> keeps the height it was given (<c>fPreserveHeight</c>).</param>
/// <param name="FitToParent">Whether the control is sized to its parent (<c>fFitToParent</c>).</param>
/// <param name="SelectChild">Whether selecting the control in a designer selects a control it contains
/// (<c>fSelectChild</c>).</param>
/// <param name="PromoteControls">Whether the control is a parent whose controls a designer can select
/// (<c>fPromoteControls</c>), which a <c>Frame</c>, a <c>MultiPage</c> and a page have set.</param>
/// <param name="Tag">The control's <c>Tag</c>.</param>
/// <param name="ControlTipText">The control's <c>ControlTipText</c>.</param>
/// <param name="HelpContextId">The control's <c>HelpContextID</c>.</param>
/// <param name="ControlSource">The control's <c>ControlSource</c>, the cell a host binds its value to.</param>
/// <param name="RowSource">The control's <c>RowSource</c>, the range a host fills a list from.</param>
/// <param name="RuntimeLicenseKey">The license key of an ActiveX control that needs one at run time.</param>
/// <param name="GroupId">The design-time group the control belongs to; <c>0</c> when it belongs to none.</param>
/// <param name="ObjectStreamSize">How many bytes of its parent's object stream (<c>o</c>) the control's data takes;
/// <c>0</c> when it has none there.</param>
public sealed record class MSFormsSite(
    double Left,
    double Top,
    int TabIndex,
    bool Visible,
    bool TabStop,
    bool Default,
    bool Cancel,
    bool Streamed,
    bool AutoSize,
    bool PreserveHeight,
    bool FitToParent,
    bool SelectChild,
    bool PromoteControls,
    string Tag,
    string ControlTipText,
    int HelpContextId,
    string ControlSource,
    string RowSource,
    string RuntimeLicenseKey,
    int GroupId,
    int ObjectStreamSize);
