using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Forms;

/// <summary>
/// The properties of a Microsoft Forms control: the values its structure stores, by the Microsoft Forms property's name
/// (<c>Caption</c>, <c>BackColor</c>, <c>Width</c>), and the file format's default for each property it does not store
/// ([MS-OFORMS] 2.2).
/// </summary>
/// <remarks>
/// Names compare case-insensitively. Each property has one type:
/// <list type="table">
/// <listheader><term>Type</term><description>Properties</description></listheader>
/// <item><term><see cref="int"/></term><description>Enumerations and integers, with the Microsoft Forms object
/// model's values (<c>SpecialEffect</c>, <c>MousePointer</c>, <c>PicturePosition</c>, <c>TextAlign</c>, <c>Style</c>,
/// <c>Max</c>, <c>Zoom</c>), and <c>BackStyle</c>.</description></item>
/// <item><term><see cref="uint"/></term><description>Colors, as <c>OLE_COLOR</c> values ([MS-OFORMS] 2.4.9):
/// <c>BackColor</c>, <c>ForeColor</c>, <c>BorderColor</c>.</description></item>
/// <item><term><see cref="bool"/></term><description>Flags, among them the <c>VariousPropertyBits</c> of a control
/// ([MS-OFORMS] 2.5.96.1): <c>Enabled</c>, <c>Locked</c>, <c>WordWrap</c>, <c>AutoSize</c>, <c>MultiLine</c>.</description></item>
/// <item><term><see cref="string"/></term><description><c>Caption</c>, <c>Value</c>, <c>GroupName</c>.</description></item>
/// <item><term><see cref="double"/></term><description>Sizes and lengths, in points (HIMETRIC × 72 / 2540):
/// <c>Width</c>, <c>Height</c>, <c>ListWidth</c>; a form's <c>InsideWidth</c> and <c>InsideHeight</c>.</description></item>
/// <item><term><see cref="char"/></term><description><c>Accelerator</c>, <c>PasswordChar</c>; <c>'\0'</c> for
/// none.</description></item>
/// <item><term><see cref="MSFormsFont"/></term><description><c>Font</c>.</description></item>
/// <item><term><see cref="MSFormsPicture"/></term><description><c>Picture</c>, <c>MouseIcon</c>.</description></item>
/// <item><term><see cref="IReadOnlyList{T}"/> of <see cref="double"/></term><description><c>ColumnWidths</c>, in
/// points; <c>-1</c> for a column whose width is not set.</description></item>
/// <item><term><see cref="IReadOnlyList{T}"/> of <see cref="MSFormsTab"/></term><description>A <c>TabStrip</c>'s
/// <c>Tabs</c>.</description></item>
/// </list>
/// A property with no default in the file format (a control's <c>Width</c>, a <c>Picture</c>) has a value only when it is
/// stored.
/// </remarks>
public sealed class MSFormsProperties
{
    private readonly IReadOnlyDictionary<string, object> _stored;
    private readonly IReadOnlyDictionary<string, object> _defaults;

    internal MSFormsProperties(MSFormsControlKind kind, IReadOnlyDictionary<string, object> stored, IReadOnlyDictionary<string, object> defaults)
    {
        Kind = kind;
        _stored = new Dictionary<string, object>(stored, StringComparer.OrdinalIgnoreCase);
        _defaults = new Dictionary<string, object>(defaults, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the kind of control whose properties these are, which decides their defaults.
    /// </summary>
    public MSFormsControlKind Kind { get; }

    /// <summary>
    /// Gets the properties the control's structure stores, by name, with their values.
    /// </summary>
    public IReadOnlyDictionary<string, object> Stored => _stored;

    /// <summary>
    /// Gets the name of every property that has a value: stored, or the file format's default.
    /// </summary>
    public IEnumerable<string> Names => _stored.Keys.Union(_defaults.Keys, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the control's structure stores the property named <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The property's name.</param>
    public bool IsStored(string name) => _stored.ContainsKey(name);

    /// <summary>
    /// Gets the value of the property named <paramref name="name"/>: the stored one, or the file format's default.
    /// </summary>
    /// <param name="name">The property's name.</param>
    /// <param name="value">The property's value; <see langword="null"/> when it has none.</param>
    /// <returns>
    /// <see langword="true"/> when the property has a value; <see langword="false"/> when it is neither stored nor has a
    /// default for this kind of control.
    /// </returns>
    public bool TryGet(string name, [NotNullWhen(true)] out object? value)
        => _stored.TryGetValue(name, out value) || _defaults.TryGetValue(name, out value);

    /// <summary>
    /// Gets the value of the property named <paramref name="name"/> as a <typeparamref name="T"/>: the stored one, or the
    /// file format's default.
    /// </summary>
    /// <typeparam name="T">The property's type (see <see cref="MSFormsProperties"/>).</typeparam>
    /// <param name="name">The property's name.</param>
    /// <param name="value">The property's value; the type's default when it has none.</param>
    /// <returns>
    /// <see langword="true"/> when the property has a value of type <typeparamref name="T"/>; <see langword="false"/> when
    /// it has none, or one of another type.
    /// </returns>
    public bool TryGet<T>(string name, [MaybeNullWhen(false)] out T value)
    {
        if (TryGet(name, out var found) && found is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets the value of the property named <paramref name="name"/> as a <typeparamref name="T"/>: the stored one, or the
    /// file format's default.
    /// </summary>
    /// <typeparam name="T">The property's type (see <see cref="MSFormsProperties"/>).</typeparam>
    /// <param name="name">The property's name.</param>
    /// <returns>The property's value.</returns>
    /// <exception cref="KeyNotFoundException">The property is neither stored nor has a default for this kind of
    /// control.</exception>
    /// <exception cref="InvalidCastException">The property's value is not a <typeparamref name="T"/>.</exception>
    public T Get<T>(string name)
        => TryGet(name, out var value) ? (T)value : throw new KeyNotFoundException();
}
