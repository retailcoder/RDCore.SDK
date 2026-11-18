namespace RDCore.SDK.Runtime.Abstract.StdLib;

/// <summary>
/// The buttons a <c>MsgBox</c> has, by its style (<strong>MS-VBAL §6.1.1.12</strong> <c>VbMsgBoxStyle</c>, <strong>§6.1.1.11</strong>
/// <c>VbMsgBoxResult</c>): what whoever shows the dialog may answer it with, which of them has the focus, and how the dialog is laid out, as Windows
/// shows it.
/// </summary>
public static class VBMsgBoxButtons
{
    private const int ButtonsMask = 0x7;
    private const int IconMask = 0x70;
    private const int DefaultButtonMask = 0x300;
    private const int DefaultButtonShift = 8;

    /// <summary>
    /// The icon of a style: <see cref="VBMsgBoxStyle.VBCritical"/>, <see cref="VBMsgBoxStyle.VBQuestion"/>, <see cref="VBMsgBoxStyle.VBExclamation"/>,
    /// <see cref="VBMsgBoxStyle.VBInformation"/>, or <c>0</c> for none. A value that names none of them is <c>0</c>.
    /// </summary>
    /// <param name="style">The style a program asked for: the buttons, the icon, the default button and the modality, summed.</param>
    public static VBMsgBoxStyle IconOf(VBMsgBoxStyle style)
        => (VBMsgBoxStyle)((int)style & IconMask) is var icon
            && icon is VBMsgBoxStyle.VBCritical or VBMsgBoxStyle.VBQuestion or VBMsgBoxStyle.VBExclamation or VBMsgBoxStyle.VBInformation
            ? icon
            : 0;

    /// <summary>
    /// The index in <see cref="ChoicesOf"/> of the button that has the focus when the dialog opens: the one <see cref="VBMsgBoxStyle.VBDefaultButton1"/>
    /// through <see cref="VBMsgBoxStyle.VBDefaultButton4"/> names, or the first when the dialog has no such button.
    /// </summary>
    /// <param name="style">The style a program asked for.</param>
    public static int DefaultChoiceOf(VBMsgBoxStyle style)
        => (((int)style & DefaultButtonMask) >> DefaultButtonShift) is var index && index < ChoicesOf(style).Count ? index : 0;

    /// <summary>
    /// The answer of a dialog of the style closed without choosing a button, by <kbd>Esc</kbd> or its close button: <see cref="VBMsgBoxResult.VBCancel"/>
    /// when it has a <em>Cancel</em> button, <see cref="VBMsgBoxResult.VBOK"/> when <em>OK</em> is its only one, and <see langword="null"/> for a dialog
    /// that cannot be closed that way (<see cref="VBMsgBoxStyle.VBYesNo"/>, <see cref="VBMsgBoxStyle.VBAbortRetryIgnore"/>).
    /// </summary>
    /// <param name="style">The style a program asked for.</param>
    public static VBMsgBoxResult? EscapeResultOf(VBMsgBoxStyle style) => ChoicesOf(style) switch
    {
        [VBMsgBoxResult.VBOK] => VBMsgBoxResult.VBOK,
        var choices when choices.Contains(VBMsgBoxResult.VBCancel) => VBMsgBoxResult.VBCancel,
        _ => null,
    };

    /// <summary>
    /// Whether a dialog of the style has a <em>Help</em> button after its others (<see cref="VBMsgBoxStyle.VBMsgBoxHelpButton"/>). The button answers
    /// nothing: it opens the help topic the <c>MsgBox</c> names, and the dialog stays open.
    /// </summary>
    /// <param name="style">The style a program asked for.</param>
    public static bool HasHelpButton(VBMsgBoxStyle style) => style.HasFlag(VBMsgBoxStyle.VBMsgBoxHelpButton);

    /// <summary>
    /// Whether the message of a dialog of the style is aligned on the right (<see cref="VBMsgBoxStyle.VBMsgBoxRight"/>).
    /// </summary>
    /// <param name="style">The style a program asked for.</param>
    public static bool IsRightAligned(VBMsgBoxStyle style) => style.HasFlag(VBMsgBoxStyle.VBMsgBoxRight);

    /// <summary>
    /// Whether a dialog of the style reads from right to left, as in Hebrew and Arabic (<see cref="VBMsgBoxStyle.VBMsgBoxRtlReading"/>): its icon and its
    /// buttons are mirrored, and its message reads from the right.
    /// </summary>
    /// <param name="style">The style a program asked for.</param>
    public static bool IsRightToLeft(VBMsgBoxStyle style) => style.HasFlag(VBMsgBoxStyle.VBMsgBoxRtlReading);

    /// <summary>
    /// The set of buttons of a style: <see cref="VBMsgBoxStyle.VBOKOnly"/> through <see cref="VBMsgBoxStyle.VBRetryCancel"/>. A value that names none of them
    /// is <see cref="VBMsgBoxStyle.VBOKOnly"/>.
    /// </summary>
    /// <param name="style">The style a program asked for: the buttons, the icon, the default button and the modality, summed.</param>
    public static VBMsgBoxStyle Of(VBMsgBoxStyle style) => ((int)style & ButtonsMask) is var buttons and <= (int)VBMsgBoxStyle.VBRetryCancel
        ? (VBMsgBoxStyle)buttons
        : VBMsgBoxStyle.VBOKOnly;

    /// <summary>
    /// The buttons of a dialog of the style, in the order they are shown, as the answer each of them gives.
    /// </summary>
    /// <param name="style">The style a program asked for.</param>
    public static IReadOnlyList<VBMsgBoxResult> ChoicesOf(VBMsgBoxStyle style) => Of(style) switch
    {
        VBMsgBoxStyle.VBOKCancel => [VBMsgBoxResult.VBOK, VBMsgBoxResult.VBCancel],
        VBMsgBoxStyle.VBAbortRetryIgnore => [VBMsgBoxResult.VBAbort, VBMsgBoxResult.VBRetry, VBMsgBoxResult.VBIgnore],
        VBMsgBoxStyle.VBYesNoCancel => [VBMsgBoxResult.VBYes, VBMsgBoxResult.VBNo, VBMsgBoxResult.VBCancel],
        VBMsgBoxStyle.VBYesNo => [VBMsgBoxResult.VBYes, VBMsgBoxResult.VBNo],
        VBMsgBoxStyle.VBRetryCancel => [VBMsgBoxResult.VBRetry, VBMsgBoxResult.VBCancel],
        _ => [VBMsgBoxResult.VBOK],
    };

    /// <summary>
    /// The name a program writes the set of buttons of the style with: <c>vbOKOnly</c>, <c>vbYesNo</c>, ...
    /// </summary>
    /// <param name="style">The style a program asked for.</param>
    public static string NameOf(VBMsgBoxStyle style) => Of(style) switch
    {
        VBMsgBoxStyle.VBOKCancel => "vbOKCancel",
        VBMsgBoxStyle.VBAbortRetryIgnore => "vbAbortRetryIgnore",
        VBMsgBoxStyle.VBYesNoCancel => "vbYesNoCancel",
        VBMsgBoxStyle.VBYesNo => "vbYesNo",
        VBMsgBoxStyle.VBRetryCancel => "vbRetryCancel",
        _ => "vbOKOnly",
    };
}
