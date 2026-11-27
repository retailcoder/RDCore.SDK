namespace RDCore.SDK.Workspace.Forms;

/// <summary>
/// The designers a form module can be written by.
/// </summary>
public enum FormDesignerKind
{
    /// <summary>
    /// A VBA UserForm: the Microsoft Forms 2.0 designer (<c>Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F}</c>). Its controls
    /// are in the companion <c>.frx</c> file, persisted as [MS-OFORMS] describes.
    /// </summary>
    MSForms,

    /// <summary>
    /// A Visual Basic 6.0 form (<c>Begin VB.Form</c>, <c>VB.MDIForm</c>, <c>VB.UserControl</c>, …). Its controls are in the
    /// module's text, as nested <c>Begin</c> … <c>End</c> blocks.
    /// </summary>
    VisualBasic6,

    /// <summary>
    /// Another ActiveX designer, named by its class identifier only (<c>Begin {…}</c>), whose controls this reader does not
    /// know how to find.
    /// </summary>
    Other,

    /// <summary>
    /// An RDForms form: the code of the form is a <c>.rdform</c> module (<see cref="ProjectModuleKinds.RDFormsModuleExtension"/>), and its controls are in the
    /// RDForms document of the same base name beside it (<see cref="FormDesigner.RDFormsDocumentExtension"/>), which an <see cref="IFormDocumentReader"/>
    /// reads. Its lengths are in device-independent pixels, 96 to an inch.
    /// </summary>
    RDForms,
}

/// <summary>
/// A control of a form, as its designer persisted it: the facts a form's declarations need.
/// </summary>
/// <remarks>
/// A form's controls are declarations of its class module: each one is a field of the form's class, of the control's
/// class, that the module's code refers to by name (<c>Me.TextBox1</c>) and handles the events of
/// (<c>TextBox1_Change</c>). That is why a form's designer data is read statically, on any platform, before any form is
/// shown.
/// </remarks>
/// <param name="Name">The name of the control, which is the name of its field in the form's class.</param>
/// <param name="Class">The class of the control.</param>
/// <param name="TabIndex">The position of the control in its container's tab order, or <c>null</c> when the designer data
/// gives none (a control that cannot take the focus, such as an MSForms <c>Image</c>).</param>
/// <param name="Index">The index of the control in its control array (a Visual Basic 6.0 control with an <c>Index</c>
/// property), or <c>null</c> when it is not part of a control array.</param>
/// <param name="Children">The controls the control contains (the controls of a <c>Frame</c>, the pages of a
/// <c>MultiPage</c>), in the order the designer persisted them.</param>
public sealed record class FormControlDescriptor(
    string Name,
    FormControlClass Class,
    int? TabIndex,
    int? Index,
    IReadOnlyList<FormControlDescriptor> Children)
{
    /// <summary>
    /// The control and every control it contains, at any depth, in the order the designer persisted them (the control
    /// first).
    /// </summary>
    public IEnumerable<FormControlDescriptor> DescendantsAndSelf()
    {
        yield return this;
        foreach (var descendant in Children.SelectMany(child => child.DescendantsAndSelf()))
        {
            yield return descendant;
        }
    }
}

/// <summary>
/// What a form module's designer declares: the form's own class and designer properties, and its controls.
/// </summary>
/// <remarks>
/// <see cref="FormDescriptor"/> is the designer model shared by every platform component that needs to know what a form
/// declares, whichever designer wrote it: the language server, to compose the form's class and analyse its code-behind,
/// and a form designer (an IDE), to agree with it. Read one with <see cref="FormDesigner.Read"/>.
/// </remarks>
/// <param name="Name">The name of the form, as its designer block names it (<c>Begin {…} ExampleView</c>).</param>
/// <param name="Kind">The designer that wrote the form.</param>
/// <param name="Class">The class of the form itself (<c>MSForms.UserForm</c>, <c>VB.Form</c>).</param>
/// <param name="Properties">The form's own designer properties, by name (case-insensitive), as written in the module's
/// designer block: a string value without its quotes, any other value as written, without its trailing comment. An RDForms form's properties are as its
/// document writes them, its lengths in device-independent pixels.</param>
/// <param name="Controls">The form's controls, in the order the designer persisted them; a container's controls are its
/// <see cref="FormControlDescriptor.Children"/>.</param>
public sealed record class FormDescriptor(
    string Name,
    FormDesignerKind Kind,
    FormControlClass Class,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<FormControlDescriptor> Controls)
{
    /// <summary>
    /// The form's own designer properties, by name, case-insensitively (whatever comparer the given dictionary had: one that went over the wire has none).
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(Properties, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The form's <c>Caption</c> designer property, or <c>null</c> when the designer block does not set it.
    /// </summary>
    public string? Caption => Properties.GetValueOrDefault("Caption");

    /// <summary>
    /// The form's <c>StartUpPosition</c> designer property (<c>1</c> is <c>CenterOwner</c>), or <c>null</c> when the designer
    /// block does not set it or sets it to something that is not a number.
    /// </summary>
    public int? StartUpPosition
        => int.TryParse(Properties.GetValueOrDefault("StartUpPosition"), out var value) ? value : null;

    /// <summary>
    /// The form's <c>ShowModal</c> designer property, or <c>null</c> when the designer block does not set it. A designer
    /// writes it only when it is not its default, <c>True</c>.
    /// </summary>
    public bool? ShowModal => Properties.GetValueOrDefault("ShowModal") switch
    {
        null => null,
        "0" or "False" => false,
        _ => true,
    };

    /// <summary>
    /// The form's <c>ClientLeft</c> designer property, in points: the distance of its client area from the left of the desktop (<see cref="PointsPerUnit"/>).
    /// <c>null</c> when the designer does not set it, or sets it to something that is not a number.
    /// </summary>
    public double? ClientLeft => PointsOf("ClientLeft");

    /// <summary>
    /// The form's <c>ClientTop</c> designer property, in points: the distance of its client area from the top of the desktop (<see cref="PointsPerUnit"/>).
    /// <c>null</c> when the designer does not set it, or sets it to something that is not a number.
    /// </summary>
    public double? ClientTop => PointsOf("ClientTop");

    /// <summary>
    /// The form's <c>ClientWidth</c> designer property, in points: the width of its window's client area (<see cref="PointsPerUnit"/>). <c>null</c> when the
    /// designer does not set it, or sets it to something that is not a number.
    /// </summary>
    public double? ClientWidth => PointsOf("ClientWidth");

    /// <summary>
    /// The form's <c>ClientHeight</c> designer property, in points: the height of its window's client area (<see cref="PointsPerUnit"/>). <c>null</c> when
    /// the designer does not set it, or sets it to something that is not a number.
    /// </summary>
    public double? ClientHeight => PointsOf("ClientHeight");

    /// <summary>
    /// How many points one unit of the form's length properties (<see cref="Properties"/>) is: a designer block writes lengths in twips, 20 to a point
    /// (0.05); an RDForms document in device-independent pixels, 96 to an inch, so a pixel is three quarters of a point (0.75).
    /// </summary>
    public double PointsPerUnit => Kind == FormDesignerKind.RDForms ? 0.75 : 0.05;

    /// <summary>
    /// Whether the form keeps Visual Basic 6.0's form model, as a Visual Basic 6.0 form (<see cref="FormDesignerKind.VisualBasic6"/>) and an RDForms form
    /// (<see cref="FormDesignerKind.RDForms"/>) do: its handlers are <c>Form_</c>'s, a member of its form loads it, <c>Show</c> with no style shows it
    /// modeless, its code can draw on it, and a form host that hosts Visual Basic 6.0 forms hosts it. <see langword="false"/> for a UserForm and for any
    /// other designer's form.
    /// </summary>
    public bool HasVisualBasic6FormModel => Kind is FormDesignerKind.VisualBasic6 or FormDesignerKind.RDForms;

    // a length property, as points: a designer writes numbers the same way in every locale.
    private double? PointsOf(string property)
        => double.TryParse(Properties.GetValueOrDefault(property), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var length)
            ? length * PointsPerUnit
            : null;

    /// <summary>
    /// The designer's state of the form, as the library that hosts it loads it (<see cref="FormDesigner.StateOf"/>), opaque here: for a VBA UserForm, the
    /// compound file its <c>.frx</c> keeps; for a Visual Basic 6.0 form, the whole <c>.frx</c>. Empty for a form whose designer keeps none, and for one read
    /// without its companion file.
    /// </summary>
    /// <remarks>
    /// What a form declares is read once (<see cref="Controls"/>); the state itself travels with the descriptor so that whatever runs the form (the
    /// environment host, which has no file of the workspace) can hand it to the form host as it is.
    /// </remarks>
    public ReadOnlyMemory<byte> State { get; init; }

    /// <summary>
    /// The text of a Visual Basic 6.0 form's designer: the module's source from its first line through the designer block's <c>End</c> line, with the
    /// <c>VERSION</c> header, the <c>Object</c> lines and the <c>Begin</c> … <c>End</c> block, and without the <c>Attribute</c> lines and the code that
    /// follow it. For an RDForms form, the text of its RDForms document. <c>null</c> for a VBA UserForm and for another designer.
    /// </summary>
    /// <remarks>
    /// It travels with the descriptor, with <see cref="State"/>, so that the library that hosts the form reads the form's properties from it where no file
    /// of the workspace is at hand.
    /// </remarks>
    public string? DesignerText { get; init; }

    /// <summary>
    /// The base class the form's class is built on, when it has one: the class whose members the form's code names unqualified, and which are not members
    /// of the form's own class. That is a Visual Basic 6.0 <c>VB.UserControl</c>, <c>VB.PropertyPage</c> or <c>VB.UserDocument</c>: in a UserControl's code,
    /// <c>BackColor</c> and <c>UserControl.BackColor</c> are the base class's, and <c>Me.BackColor</c> is a member the UserControl declares or none. A
    /// declaration of the module's own of the same name shadows the base class's member. <c>null</c> for any other form: the members of a
    /// <c>VB.Form</c>, a <c>VB.MDIForm</c> and an <c>MSForms.UserForm</c> are members of the form's own class.
    /// </summary>
    public FormControlClass? BaseClass
        => Kind == FormDesignerKind.VisualBasic6 && Class is { Library: { } library, Name: { } name }
           && string.Equals(library, VisualBasicLibrary, StringComparison.OrdinalIgnoreCase)
           && BaseClassNames.Contains(name, StringComparer.OrdinalIgnoreCase)
            ? Class
            : null;

    private const string VisualBasicLibrary = "VB";

    private static readonly string[] BaseClassNames = ["UserControl", "PropertyPage", "UserDocument"];

    /// <summary>
    /// Every control of the form, at any depth, in the order the designer persisted them.
    /// </summary>
    public IEnumerable<FormControlDescriptor> AllControls() => Controls.SelectMany(control => control.DescendantsAndSelf());

    /// <summary>
    /// The controls the form's class has a field for, one per name, in the order the designer persisted them.
    /// </summary>
    /// <remarks>
    /// Every control is one, at any depth: the controls of a <c>Frame</c> and of a <c>MultiPage</c>'s pages are fields of the form, like the form's own
    /// (<c>UserForm1.OptionButton1</c>). The pages of a <c>MultiPage</c> are not (MS-VBA: <c>UserForm1.Page1</c> is "Method or data member not found", and the
    /// form's <c>Controls</c> collection does not have them; Excel-measured), and are reached through their <c>MultiPage</c> (<c>MultiPage1.Pages("Page1")</c>).
    /// The controls of a Visual Basic 6.0 control array share their name, which is the one field.
    /// </remarks>
    public IEnumerable<FormControlDescriptor> DeclaredControls()
        => AllControls()
            .Where(control => !(Kind == FormDesignerKind.MSForms && control.Class is { Library: FormDesigner.MSFormsLibrary, Name: "Page" }))
            .DistinctBy(control => control.Name, StringComparer.OrdinalIgnoreCase);
}
