namespace RDCore.SDK.Forms;

/// <summary>
/// The kinds of controls a Microsoft Forms form persists, by the structure that persists them ([MS-OFORMS] 2.2).
/// </summary>
public enum MSFormsControlKind
{
    /// <summary>
    /// The form itself (a VBA <c>UserForm</c>), persisted as a <c>FormControl</c> ([MS-OFORMS] 2.2.10).
    /// </summary>
    Form,

    /// <summary>
    /// A <c>CommandButton</c> ([MS-OFORMS] 2.2.1).
    /// </summary>
    CommandButton,

    /// <summary>
    /// A <c>Label</c> ([MS-OFORMS] 2.2.4).
    /// </summary>
    Label,

    /// <summary>
    /// A <c>TextBox</c>: a <c>MorphDataControl</c> whose <c>DisplayStyle</c> is text ([MS-OFORMS] 2.2.5).
    /// </summary>
    TextBox,

    /// <summary>
    /// A <c>ListBox</c>: a <c>MorphDataControl</c> whose <c>DisplayStyle</c> is a list ([MS-OFORMS] 2.2.5).
    /// </summary>
    ListBox,

    /// <summary>
    /// A <c>ComboBox</c>: a <c>MorphDataControl</c> whose <c>DisplayStyle</c> is a combo or a drop-down list
    /// ([MS-OFORMS] 2.2.5).
    /// </summary>
    ComboBox,

    /// <summary>
    /// A <c>CheckBox</c>: a <c>MorphDataControl</c> whose <c>DisplayStyle</c> is a check box ([MS-OFORMS] 2.2.5).
    /// </summary>
    CheckBox,

    /// <summary>
    /// An <c>OptionButton</c>: a <c>MorphDataControl</c> whose <c>DisplayStyle</c> is an option button
    /// ([MS-OFORMS] 2.2.5).
    /// </summary>
    OptionButton,

    /// <summary>
    /// A <c>ToggleButton</c>: a <c>MorphDataControl</c> whose <c>DisplayStyle</c> is a toggle button
    /// ([MS-OFORMS] 2.2.5).
    /// </summary>
    ToggleButton,

    /// <summary>
    /// A <c>Frame</c>: a parent control, persisted as a <c>FormControl</c> in a storage of its own ([MS-OFORMS] 2.2.2).
    /// </summary>
    Frame,

    /// <summary>
    /// An <c>Image</c> ([MS-OFORMS] 2.2.3).
    /// </summary>
    Image,

    /// <summary>
    /// A <c>ScrollBar</c> ([MS-OFORMS] 2.2.7).
    /// </summary>
    ScrollBar,

    /// <summary>
    /// A <c>SpinButton</c> ([MS-OFORMS] 2.2.8).
    /// </summary>
    SpinButton,

    /// <summary>
    /// A <c>MultiPage</c>: a parent control whose children are its pages ([MS-OFORMS] 2.2.6).
    /// </summary>
    MultiPage,

    /// <summary>
    /// A page of a <c>MultiPage</c>: a parent control, persisted as a <c>FormControl</c> in a storage of its own
    /// ([MS-OFORMS] 2.1.2.3.1).
    /// </summary>
    Page,

    /// <summary>
    /// A <c>TabStrip</c> ([MS-OFORMS] 2.2.9).
    /// </summary>
    TabStrip,

    /// <summary>
    /// An ActiveX control that is not one of Microsoft Forms' own, named by its class identifier
    /// (<see cref="MSFormsControl.ClassId"/>). Its persisted data is the control's own and is not read.
    /// </summary>
    Other,
}

/// <summary>
/// A control of a Microsoft Forms form, or the form itself: its site in its parent, the properties its own structure
/// stores, and the controls it contains.
/// </summary>
/// <param name="Name">The control's name; empty for the form itself, which its module names.</param>
/// <param name="Id">The control's ID in its parent ([MS-OFORMS] 2.2.10.12.3), which also names the storage of a parent
/// control (<c>i</c> and the ID in at least two digits); <c>0</c> for the form itself.</param>
/// <param name="Kind">The kind of control.</param>
/// <param name="ClassId">The class identifier of an <see cref="MSFormsControlKind.Other"/> control, from the form's class
/// table ([MS-OFORMS] 2.2.10.10); <see langword="null"/> for a Microsoft Forms control.</param>
/// <param name="Site">The control's site in its parent ([MS-OFORMS] 2.2.10.12); <see langword="null"/> for the form itself.</param>
/// <param name="Properties">The properties the control's own structure stores, with the file format's defaults for those
/// it does not.</param>
/// <param name="Children">The controls a <see cref="MSFormsControlKind.Frame"/> or a <see cref="MSFormsControlKind.Page"/>
/// (or the form) contains, in the order their sites are persisted; the pages of a
/// <see cref="MSFormsControlKind.MultiPage"/>, in their tabs' order. Empty for any other control.</param>
public sealed record class MSFormsControl(
    string Name,
    int Id,
    MSFormsControlKind Kind,
    Guid? ClassId,
    MSFormsSite? Site,
    MSFormsProperties Properties,
    IReadOnlyList<MSFormsControl> Children)
{
    /// <summary>
    /// The control and every control it contains, at any depth, in the order of <see cref="Children"/> (the control
    /// first).
    /// </summary>
    /// <returns>The control, then each of its children and their descendants, depth first.</returns>
    public IEnumerable<MSFormsControl> DescendantsAndSelf()
    {
        yield return this;
        foreach (var descendant in Children.SelectMany(child => child.DescendantsAndSelf()))
        {
            yield return descendant;
        }
    }
}

/// <summary>
/// The designer data of a Microsoft Forms form, as <see cref="MSFormsReader.Read"/> reads it from the storage the form
/// persists itself to ([MS-OFORMS] 2.1).
/// </summary>
/// <remarks>
/// A VBA UserForm keeps some of its own properties in its module's designer block instead (<c>Caption</c>,
/// <c>ClientWidth</c>, <c>StartUpPosition</c>): those are not part of the designer data.
/// </remarks>
/// <param name="Form">The form, a control of kind <see cref="MSFormsControlKind.Form"/>, whose properties are its
/// <c>FormControl</c>'s ([MS-OFORMS] 2.2.10) and whose children are its controls.</param>
public sealed record class MSFormsDesign(MSFormsControl Form);
