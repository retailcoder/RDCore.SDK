using RDCore.SDK.Runtime.Stdole;

namespace RDCore.SDK.Runtime.Vbrun;

/// <summary>
/// What a <c>VBRUN.AmbientProperties</c> object reads, the <c>Ambient</c> of a UserControl's code: the ambient properties of the container
/// that holds a placed UserControl, as VB6 gives them on a form while the program runs.
/// </summary>
/// <remarks>
/// <para>
/// The members that depend on the container are read from it (<see cref="IAmbientContainer"/>) each time they are read. The others have the
/// values VB6 gives a UserControl on a running form:
/// </para>
/// <list type="table">
/// <listheader><term>Member</term><description>Value</description></listheader>
/// <item><term><see cref="MessageReflect"/>, <see cref="SupportsMnemonics"/>, <see cref="UserMode"/></term><description>True.</description></item>
/// <item><term><see cref="ShowGrabHandles"/>, <see cref="ShowHatching"/>, <see cref="UIDead"/></term><description>False.</description></item>
/// <item><term><see cref="TextAlign"/></term><description>0, General.</description></item>
/// </list>
/// <para>
/// A member <c>AmbientProperties</c> does not have, read late-bound, is run-time error 445, "Object doesn't support this action", in VB6; the
/// runtime raises it. <c>Palette</c> is not modelled.
/// </para>
/// </remarks>
public sealed class AmbientPropertiesObject
{
    private readonly IAmbientContainer _container;

    /// <summary>
    /// Creates the <c>Ambient</c> object of a placed UserControl.
    /// </summary>
    /// <param name="container">The container that holds the control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="container"/> is <see langword="null"/>.</exception>
    public AmbientPropertiesObject(IAmbientContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        _container = container;
    }

    /// <summary>
    /// The container's background color, an <c>OLE_COLOR</c>: unsigned, so that a system color such as <c>&amp;H8000000F</c> reads 2147483663.
    /// </summary>
    public uint BackColor => _container.BackColor;

    /// <summary>
    /// The container's foreground color, an <c>OLE_COLOR</c>.
    /// </summary>
    public uint ForeColor => _container.ForeColor;

    /// <summary>
    /// A copy of the container's font: a font of its own, which a change to the container's font does not change.
    /// </summary>
    public StdFontObject Font => _container.Font.Clone();

    /// <summary>
    /// Whether the container shows text from right to left.
    /// </summary>
    public bool RightToLeft => _container.RightToLeft;

    /// <summary>
    /// The name of the container's unit, such as <c>Twip</c> or <c>Pixel</c>.
    /// </summary>
    public string ScaleUnits => _container.ScaleUnits;

    /// <summary>
    /// The name the container gives the placed control: on a form, the control's name.
    /// </summary>
    public string DisplayName => _container.DisplayName;

    /// <summary>
    /// Whether the control is the container's default button: True for the Default control of a UserControl whose <c>DefaultCancel</c> is
    /// True, once its form is shown.
    /// </summary>
    public bool DisplayAsDefault => _container.DisplayAsDefault;

    /// <summary>
    /// The container's locale, such as 1033 for English (United States).
    /// </summary>
    public int LocaleID => _container.LocaleID;

    /// <summary>
    /// Whether the container reflects the control's window messages back to it: True.
    /// </summary>
    public bool MessageReflect => true;

    /// <summary>
    /// Whether the container shows grab handles: False while the program runs.
    /// </summary>
    public bool ShowGrabHandles => false;

    /// <summary>
    /// Whether the container shows the control hatched: False while the program runs.
    /// </summary>
    public bool ShowHatching => false;

    /// <summary>
    /// Whether the container handles access keys: True.
    /// </summary>
    public bool SupportsMnemonics => true;

    /// <summary>
    /// How the container aligns text: 0, General.
    /// </summary>
    public short TextAlign => 0;

    /// <summary>
    /// Whether the program runs, rather than its form being designed: True.
    /// </summary>
    public bool UserMode => true;

    /// <summary>
    /// Whether the container takes no input from the control: False.
    /// </summary>
    public bool UIDead => false;
}

/// <summary>
/// The container of a placed UserControl, as its <see cref="AmbientPropertiesObject"/> reads it: what a host gives the UserControl's code.
/// </summary>
public interface IAmbientContainer
{
    /// <summary>
    /// The container's background color, an <c>OLE_COLOR</c>: on a form, the form's <c>BackColor</c>.
    /// </summary>
    uint BackColor { get; }

    /// <summary>
    /// The container's foreground color, an <c>OLE_COLOR</c>: on a form, the form's <c>ForeColor</c>.
    /// </summary>
    uint ForeColor { get; }

    /// <summary>
    /// The container's font: on a form, the form's <c>Font</c>. <see cref="AmbientPropertiesObject.Font"/> reads a copy of it.
    /// </summary>
    StdFontObject Font { get; }

    /// <summary>
    /// Whether the container shows text from right to left: on a form, its <c>RightToLeft</c>.
    /// </summary>
    bool RightToLeft { get; }

    /// <summary>
    /// The name of the container's unit, from its <c>ScaleMode</c>: <c>Twip</c>, <c>Pixel</c>, or another unit's name.
    /// </summary>
    string ScaleUnits { get; }

    /// <summary>
    /// The name the container gives the placed control: on a form, the control's name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether the control is the container's default button.
    /// </summary>
    bool DisplayAsDefault { get; }

    /// <summary>
    /// The container's locale identifier.
    /// </summary>
    int LocaleID { get; }
}
