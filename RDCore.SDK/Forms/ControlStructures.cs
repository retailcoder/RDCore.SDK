using static RDCore.SDK.Forms.MSFormsControlKind;

namespace RDCore.SDK.Forms;

/// <summary>
/// What the tab row of a <c>TabStrip</c> (or of a <c>MultiPage</c>) stores about each tab, and which of its per-tab
/// arrays it stores ([MS-OFORMS] 2.2.9).
/// </summary>
/// <param name="Tabs">The tabs, in the order the row stores them.</param>
/// <param name="CaptionsStored">Whether the row stores the tabs' captions.</param>
/// <param name="NamesStored">Whether the row stores the tabs' names.</param>
/// <param name="TipsStored">Whether the row stores the tabs' tip texts.</param>
/// <param name="TagsStored">Whether the row stores the tabs' tags.</param>
/// <param name="AcceleratorsStored">Whether the row stores the tabs' accelerators.</param>
/// <param name="FlagCount">How many tab flags the row stores.</param>
internal sealed record class TabRow(
    IReadOnlyList<MSFormsTab> Tabs,
    bool CaptionsStored,
    bool NamesStored,
    bool TipsStored,
    bool TagsStored,
    bool AcceleratorsStored,
    int FlagCount);

/// <summary>
/// A control's data in its parent's object stream (<c>o</c>), read: its kind (a generic <c>MorphDataControl</c>'s comes
/// from its <c>DisplayStyle</c>), its properties, and a <c>TabStrip</c>'s tabs.
/// </summary>
/// <param name="Kind">The control's kind.</param>
/// <param name="Properties">The control's properties.</param>
/// <param name="TabRow">A <c>TabStrip</c>'s tab row; <c>null</c> for another control.</param>
internal sealed record class ControlData(MSFormsControlKind Kind, MSFormsProperties Properties, TabRow? TabRow = null);

/// <summary>
/// The structures of the controls [MS-OFORMS] 2.2 persists in a parent's object stream, and the bit fields they share.
/// </summary>
internal static class ControlStructures
{
    private static IReadOnlySet<MSFormsControlKind> Kinds(params MSFormsControlKind[] kinds) => new HashSet<MSFormsControlKind>(kinds);

    private static readonly IReadOnlySet<MSFormsControlKind> MorphDataKinds = Kinds(TextBox, ListBox, ComboBox, CheckBox, OptionButton, ToggleButton);

    // CommandButtonControl ([MS-OFORMS] 2.2.1.2)
    public static readonly Slot[] CommandButton =
    [
        new(0, SlotType.Color, "ForeColor", 0x80000012u),
        new(1, SlotType.Color, "BackColor", 0x8000000Fu),
        new(2, SlotType.Bits, "_VariousPropertyBits", 0x0000001Bu),
        new(3, SlotType.String, "Caption", ""),
        new(4, SlotType.PicturePosition, "PicturePosition", 0x00070001u),
        new(5, SlotType.Size, "Width", Name2: "Height"),
        new(6, SlotType.UInt8, "MousePointer", 0),
        new(7, SlotType.Picture, "Picture"),
        new(8, SlotType.Char, "Accelerator", '\0'),
        new(9, SlotType.Flag, "TakeFocusOnClick", true),
        new(10, SlotType.Picture, "MouseIcon"),
    ];

    // LabelControl ([MS-OFORMS] 2.2.4.2)
    public static readonly Slot[] Label =
    [
        new(0, SlotType.Color, "ForeColor", 0x80000012u),
        new(1, SlotType.Color, "BackColor", 0x8000000Fu),
        new(2, SlotType.Bits, "_VariousPropertyBits", 0x0080001Bu),
        new(3, SlotType.String, "Caption", ""),
        new(4, SlotType.PicturePosition, "PicturePosition", 0x00070001u),
        new(5, SlotType.Size, "Width", Name2: "Height"),
        new(6, SlotType.UInt8, "MousePointer", 0),
        new(7, SlotType.Color, "BorderColor", 0x80000006u),
        new(8, SlotType.UInt16, "BorderStyle", 0),
        new(9, SlotType.UInt16, "SpecialEffect", 0),
        new(10, SlotType.Picture, "Picture"),
        new(11, SlotType.Char, "Accelerator", '\0'),
        new(12, SlotType.Picture, "MouseIcon"),
    ];

    // MorphDataControl ([MS-OFORMS] 2.2.5.2): a property that does not apply to a kind of control has no default for it
    public static readonly Slot[] MorphData = MorphDataSlots();

    private static Slot[] MorphDataSlots()
    {
        var text = Kinds(TextBox);
        var list = Kinds(ListBox);
        var combo = Kinds(ComboBox);
        var lists = Kinds(ListBox, ComboBox);
        var texts = Kinds(TextBox, ComboBox);
        var bordered = Kinds(TextBox, ListBox, ComboBox);
        var buttons = Kinds(CheckBox, OptionButton, ToggleButton);
        return
        [
            new(0, SlotType.Bits, "_VariousPropertyBits", 0x2C80081Bu),
            new(1, SlotType.Color, "BackColor", 0x80000005u),
            new(2, SlotType.Color, "ForeColor", 0x80000008u),
            new(3, SlotType.UInt32, "MaxLength", 0, AppliesTo: texts),
            new(4, SlotType.UInt8, "BorderStyle", 0, AppliesTo: bordered),
            new(5, SlotType.UInt8, "ScrollBars", 0, AppliesTo: Kinds(TextBox, ListBox)),
            new(6, SlotType.UInt8, "_DisplayStyle", 1),
            new(7, SlotType.UInt8, "MousePointer", 0),
            new(8, SlotType.Size, "Width", Name2: "Height"),
            new(9, SlotType.Char, "PasswordChar", '\0', AppliesTo: text),
            new(10, SlotType.Length, "ListWidth", 0, AppliesTo: combo),
            new(11, SlotType.UInt16, "BoundColumn", 1, AppliesTo: lists),
            new(12, SlotType.Int16, "TextColumn", -1, AppliesTo: lists),
            new(13, SlotType.Int16, "ColumnCount", 1, AppliesTo: lists),
            new(14, SlotType.UInt16, "ListRows", 8, AppliesTo: combo),
            new(15, SlotType.UInt16, "_cColumnInfo", 0),
            new(16, SlotType.UInt8, "MatchEntry", 2, AppliesTo: lists),
            new(17, SlotType.UInt8, "ListStyle", 0, AppliesTo: lists),
            new(18, SlotType.UInt8, "ShowDropButtonWhen", 0, AppliesTo: texts),
            new(20, SlotType.UInt8, "DropButtonStyle", 1, AppliesTo: texts),
            new(21, SlotType.UInt8, "MultiSelect", 0, AppliesTo: list),
            new(22, SlotType.String, "Value", ""),
            new(23, SlotType.String, "Caption", "", AppliesTo: buttons),
            new(24, SlotType.PicturePosition, "PicturePosition", 0x00070001u, AppliesTo: buttons),
            new(25, SlotType.Color, "BorderColor", 0x80000006u, AppliesTo: bordered),
            new(26, SlotType.UInt32, "SpecialEffect", 2),
            new(27, SlotType.Picture, "MouseIcon"),
            new(28, SlotType.Picture, "Picture"),
            new(29, SlotType.Char, "Accelerator", '\0', AppliesTo: buttons),
            new(32, SlotType.String, "GroupName", "", AppliesTo: Kinds(CheckBox, OptionButton)),
        ];
    }

    // ImageControl ([MS-OFORMS] 2.2.3.2)
    public static readonly Slot[] Image =
    [
        new(2, SlotType.Flag, "AutoSize", false),
        new(3, SlotType.Color, "BorderColor", 0x80000006u),
        new(4, SlotType.Color, "BackColor", 0x8000000Fu),
        new(5, SlotType.UInt8, "BorderStyle", 1),
        new(6, SlotType.UInt8, "MousePointer", 0),
        new(7, SlotType.UInt8, "PictureSizeMode", 0),
        new(8, SlotType.UInt8, "SpecialEffect", 0),
        new(9, SlotType.Size, "Width", Name2: "Height"),
        new(10, SlotType.Picture, "Picture"),
        new(11, SlotType.UInt8, "PictureAlignment", 2),
        new(12, SlotType.Flag, "PictureTiling", false),
        new(13, SlotType.Bits, "_VariousPropertyBits", 0x0000001Bu),
        new(14, SlotType.Picture, "MouseIcon"),
    ];

    // ScrollBarControl ([MS-OFORMS] 2.2.7.2)
    public static readonly Slot[] ScrollBar =
    [
        new(0, SlotType.Color, "ForeColor", 0x80000012u),
        new(1, SlotType.Color, "BackColor", 0x8000000Fu),
        new(2, SlotType.Bits, "_VariousPropertyBits", 0x0000001Bu),
        new(3, SlotType.Size, "Width", Name2: "Height"),
        new(4, SlotType.UInt8, "MousePointer", 0),
        new(5, SlotType.Int32, "Min", 0),
        new(6, SlotType.Int32, "Max", 32767),
        new(7, SlotType.Int32, "Value", 0),
        new(9, SlotType.Int32, "_PrevEnabled", 1),
        new(10, SlotType.Int32, "_NextEnabled", 1),
        new(11, SlotType.Int32, "SmallChange", 1),
        new(12, SlotType.Int32, "LargeChange", 1),
        new(13, SlotType.Int32, "Orientation", -1),
        new(14, SlotType.Boolean16, "ProportionalThumb", true),
        new(15, SlotType.UInt32, "Delay", 50),
        new(16, SlotType.Picture, "MouseIcon"),
    ];

    // SpinButtonControl ([MS-OFORMS] 2.2.8.2)
    public static readonly Slot[] SpinButton =
    [
        new(0, SlotType.Color, "ForeColor", 0x80000012u),
        new(1, SlotType.Color, "BackColor", 0x8000000Fu),
        new(2, SlotType.Bits, "_VariousPropertyBits", 0x0000001Bu),
        new(3, SlotType.Size, "Width", Name2: "Height"),
        new(5, SlotType.Int32, "Min", 0),
        new(6, SlotType.Int32, "Max", 100),
        new(7, SlotType.Int32, "Value", 0),
        new(8, SlotType.Int32, "_PrevEnabled", 1),
        new(9, SlotType.Int32, "_NextEnabled", 1),
        new(10, SlotType.Int32, "SmallChange", 1),
        new(11, SlotType.Int32, "Orientation", -1),
        new(12, SlotType.UInt32, "Delay", 50),
        new(13, SlotType.Picture, "MouseIcon"),
        new(14, SlotType.UInt8, "MousePointer", 0),
    ];

    // TabStripControl ([MS-OFORMS] 2.2.9.2)
    public static readonly Slot[] TabStrip =
    [
        new(0, SlotType.Int32, "Value", -1),
        new(1, SlotType.Color, "BackColor", 0x8000000Fu),
        new(2, SlotType.Color, "ForeColor", 0x80000012u),
        new(4, SlotType.Size, "Width", Name2: "Height"),
        new(5, SlotType.Strings, "_Items"),
        new(6, SlotType.UInt8, "MousePointer", 0),
        new(8, SlotType.UInt32, "TabOrientation", 0),
        new(9, SlotType.UInt32, "Style", 0),
        new(10, SlotType.Flag, "MultiRow", false),
        new(11, SlotType.Length, "TabFixedWidth", 0),
        new(12, SlotType.Length, "TabFixedHeight", 0),
        new(13, SlotType.Flag, "_Tooltips", true),
        new(15, SlotType.Strings, "_TipStrings"),
        new(17, SlotType.Strings, "_Names"),
        new(18, SlotType.Bits, "_VariousPropertyBits", 0x0000001Bu),
        new(20, SlotType.UInt32, "_TabsAllocated", 0),
        new(21, SlotType.Strings, "_Tags"),
        new(22, SlotType.UInt32, "_TabData", 0),
        new(23, SlotType.Strings, "_Accelerators"),
        new(24, SlotType.Picture, "MouseIcon"),
    ];

    // MorphDataColumnInfo ([MS-OFORMS] 2.2.5.6): its width, in HIMETRIC; -1 when it is not set
    private static readonly Slot[] ColumnInfo = [new(0, SlotType.Int32, "_ColumnWidth", -1)];

    // VariousPropertyBits ([MS-OFORMS] 2.5.96.1): the fields that are object model properties, each with the kinds of
    // control it applies to (null: every kind whose structure has the field)
    private static readonly (int Bit, int Width, string Name, bool IsInteger, IReadOnlySet<MSFormsControlKind>? AppliesTo)[] VariousBits =
    [
        (1, 1, "Enabled", false, null),
        (2, 1, "Locked", false, Kinds(CheckBox, ComboBox, MSFormsControlKind.CommandButton, ListBox, OptionButton, TextBox, ToggleButton)),
        (3, 1, "BackStyle", true, Kinds(MSFormsControlKind.CommandButton, MSFormsControlKind.Label, MSFormsControlKind.Image, TextBox, ComboBox, CheckBox, OptionButton, ToggleButton)),
        (10, 1, "ColumnHeads", false, Kinds(ComboBox, ListBox)),
        (11, 1, "IntegralHeight", false, Kinds(ListBox, TextBox)),
        (12, 1, "MatchRequired", false, Kinds(ComboBox)),
        (13, 1, "Alignment", true, Kinds(CheckBox, OptionButton)),
        (15, 4, "IMEMode", true, MorphDataKinds),
        (19, 1, "DragBehavior", true, Kinds(ComboBox, TextBox)),
        (20, 1, "EnterKeyBehavior", false, Kinds(TextBox)),
        (21, 1, "EnterFieldBehavior", true, Kinds(ComboBox, TextBox)),
        (22, 1, "TabKeyBehavior", false, Kinds(TextBox)),
        (23, 1, "WordWrap", false, Kinds(MSFormsControlKind.CommandButton, MSFormsControlKind.Label, TextBox, CheckBox, OptionButton, ToggleButton)),
        (26, 1, "SelectionMargin", false, Kinds(TextBox, ComboBox)),
        (27, 1, "AutoWordSelect", false, Kinds(TextBox, ComboBox)),
        (28, 1, "AutoSize", false, Kinds(MSFormsControlKind.CommandButton, MSFormsControlKind.Label, TextBox, ComboBox, CheckBox, OptionButton, ToggleButton)),
        (29, 1, "HideSelection", false, Kinds(TextBox, ComboBox)),
        (30, 1, "AutoTab", false, Kinds(TextBox, ComboBox)),
        (31, 1, "MultiLine", false, Kinds(TextBox)),
    ];

    /// <summary>
    /// The object model properties a <c>VariousPropertyBits</c> ([MS-OFORMS] 2.5.96.1) gives a control of kind
    /// <paramref name="kind"/>.
    /// </summary>
    /// <param name="bits">The <c>VariousPropertyBits</c> value.</param>
    /// <param name="kind">The control's kind, which decides which of the bits are its properties.</param>
    /// <param name="into">The properties to add the decoded ones to, by name.</param>
    public static void DecodeVariousPropertyBits(uint bits, MSFormsControlKind kind, IDictionary<string, object> into)
    {
        foreach (var (bit, width, name, isInteger, appliesTo) in VariousBits)
        {
            if (appliesTo is not null && !appliesTo.Contains(kind))
            {
                continue;
            }

            var value = (int)((bits >> bit) & ((1u << width) - 1));
            into[name] = name switch
            {
                // a set fAlignment puts the caption on the left of the button: fmAlignmentLeft (0)
                "Alignment" => value != 0 ? 0 : 1,
                _ when isInteger => value,
                _ => value != 0,
            };
        }
    }

    /// <summary>
    /// The properties of a control of kind <paramref name="kind"/> from its structure's stored values and its slots'
    /// defaults, with its <c>VariousPropertyBits</c> decoded.
    /// </summary>
    /// <param name="block">The structure's stored values.</param>
    /// <param name="slots">The structure's slots.</param>
    /// <param name="kind">The control's kind.</param>
    public static (Dictionary<string, object> Stored, Dictionary<string, object> Defaults) PropertiesOf(Block block, IReadOnlyList<Slot> slots, MSFormsControlKind kind)
    {
        var stored = block.Values.Where(value => !value.Key.StartsWith('_')).ToDictionary(StringComparer.OrdinalIgnoreCase);
        var defaults = PropertyBlock.Defaults(slots, kind).Where(value => !value.Key.StartsWith('_')).ToDictionary(StringComparer.OrdinalIgnoreCase);
        if (slots.FirstOrDefault(slot => slot.Name == "_VariousPropertyBits") is { Default: uint defaultBits })
        {
            DecodeVariousPropertyBits(defaultBits, kind, defaults);
            if (block.Values.TryGetValue("_VariousPropertyBits", out var bits))
            {
                DecodeVariousPropertyBits((uint)bits, kind, stored);
            }
        }

        return (stored, defaults);
    }

    /// <summary>
    /// Reads a control's data in its parent's object stream ([MS-OFORMS] 2.1.2.2.1): <paramref name="piece"/>, the bytes
    /// its site gives it.
    /// </summary>
    /// <param name="piece">The control's bytes of the object stream.</param>
    /// <param name="kind">The control's kind by its site's class; for a generic <c>MorphDataControl</c> (class cache index
    /// 15, [MS-OFORMS] 2.4.5), <paramref name="isGenericMorphData"/> says its <c>DisplayStyle</c> decides.</param>
    /// <param name="isGenericMorphData">Whether the control is a generic <c>MorphDataControl</c>, whose kind its
    /// <c>DisplayStyle</c> decides.</param>
    public static ControlData Read(ReadOnlySpan<byte> piece, MSFormsControlKind kind, bool isGenericMorphData)
    {
        var reader = new ByteReader(piece);
        switch (kind)
        {
            case MSFormsControlKind.CommandButton or MSFormsControlKind.Label:
            {
                var slots = kind == MSFormsControlKind.CommandButton ? CommandButton : Label;
                var block = PropertyBlock.Read(ref reader, slots, maskSize: 4, majorVersion: 2);
                var (stored, defaults) = PropertiesOf(block, slots, kind);
                AddTextProps(ref reader, stored, defaults);
                return new(kind, new MSFormsProperties(kind, stored, defaults));
            }

            case MSFormsControlKind.Image or MSFormsControlKind.ScrollBar or MSFormsControlKind.SpinButton:
            {
                var slots = kind switch { MSFormsControlKind.Image => Image, MSFormsControlKind.ScrollBar => ScrollBar, _ => SpinButton };
                var block = PropertyBlock.Read(ref reader, slots, maskSize: 4, majorVersion: 2);
                var (stored, defaults) = PropertiesOf(block, slots, kind);
                return new(kind, new MSFormsProperties(kind, stored, defaults));
            }

            case MSFormsControlKind.TabStrip:
                return ReadTabStrip(ref reader);

            default:
                return ReadMorphData(ref reader, kind, isGenericMorphData);
        }
    }

    private static void AddTextProps(ref ByteReader reader, Dictionary<string, object> stored, Dictionary<string, object> defaults)
    {
        var (font, textAlign) = PropertyBlock.ReadTextProps(ref reader);
        stored["Font"] = font;
        defaults["TextAlign"] = 1;
        if (textAlign is { } align)
        {
            stored["TextAlign"] = align;
        }
    }

    // MorphDataControl ([MS-OFORMS] 2.2.5): its blocks, its TextProps, then a ListBox's or ComboBox's column information
    private static ControlData ReadMorphData(ref ByteReader reader, MSFormsControlKind kind, bool isGenericMorphData)
    {
        var block = PropertyBlock.Read(ref reader, MorphData, maskSize: 8, majorVersion: 2);
        var displayStyle = block.Get("_DisplayStyle", 1);
        if (isGenericMorphData)
        {
            kind = displayStyle switch
            {
                1 => TextBox,
                2 => ListBox,
                3 or 7 => ComboBox,
                4 => CheckBox,
                5 => OptionButton,
                6 => ToggleButton,
                _ => throw new MSFormsFormatException(MSFormsFormatProblem.InvalidControl),
            };
        }

        var (stored, defaults) = PropertiesOf(block, MorphData, kind);
        AddTextProps(ref reader, stored, defaults);
        if (kind == ComboBox)
        {
            // fmDisplayStyleList (7) is a drop-down list, fmStyleDropDownList (2); any other is a combo, fmStyleDropDownCombo (0)
            defaults["Style"] = 0;
            if (block.Has(6))
            {
                stored["Style"] = displayStyle == 7 ? 2 : 0;
            }
        }

        var columns = block.Get("_cColumnInfo", 0);
        if (columns > 0)
        {
            var widths = new double[columns];
            for (var index = 0; index < columns; index++)
            {
                var column = PropertyBlock.Read(ref reader, ColumnInfo, maskSize: 4, majorVersion: 2);
                var width = column.Get("_ColumnWidth", -1);
                widths[index] = width < 0 ? width : PropertyBlock.Points(width);
            }

            stored["ColumnWidths"] = Array.AsReadOnly(widths);
        }

        return new(kind, new MSFormsProperties(kind, stored, defaults));
    }

    // TabStripControl ([MS-OFORMS] 2.2.9): its blocks, its TextProps (when fNewVersion says it has one), then one
    // TabStripTabFlag (2.2.9.7) per tab its TabData counts
    private static ControlData ReadTabStrip(ref ByteReader reader)
    {
        var block = PropertyBlock.Read(ref reader, TabStrip, maskSize: 4, majorVersion: 2);
        var (stored, defaults) = PropertiesOf(block, TabStrip, MSFormsControlKind.TabStrip);
        if (block.Has(19))
        {
            AddTextProps(ref reader, stored, defaults);
        }

        var flagCount = block.Get("_TabData", 0);
        if (flagCount < 0 || flagCount > reader.Length / 4)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.Truncated);
        }

        var flags = new uint[flagCount];
        for (var index = 0; index < flagCount; index++)
        {
            flags[index] = reader.ReadUInt32();
        }

        string[] StringsOf(string name) => block.Get(name, System.Array.Empty<string>());
        var captions = StringsOf("_Items");
        var names = StringsOf("_Names");
        var tips = StringsOf("_TipStrings");
        var tags = StringsOf("_Tags");
        var accelerators = StringsOf("_Accelerators");
        var count = new[] { captions.Length, names.Length, tips.Length, tags.Length, accelerators.Length, flagCount }.Max();
        string At(string[] values, int index) => index < values.Length ? values[index] : string.Empty;

        // a tab its TabData does not cover has the default flags: visible and enabled
        var tabs = Enumerable.Range(0, count).Select(index => new MSFormsTab(
                At(captions, index),
                At(names, index),
                At(tips, index),
                At(tags, index),
                At(accelerators, index) is [var key, ..] ? key : '\0',
                index >= flagCount || (flags[index] & 1) != 0,
                index >= flagCount || (flags[index] & 2) != 0))
            .ToList()
            .AsReadOnly();
        defaults["Tabs"] = new List<MSFormsTab>().AsReadOnly();
        if (count > 0)
        {
            stored["Tabs"] = tabs;
        }

        var row = new TabRow(tabs, block.Has(5), block.Has(17), block.Has(15), block.Has(21), block.Has(23), flagCount);
        return new(MSFormsControlKind.TabStrip, new MSFormsProperties(MSFormsControlKind.TabStrip, stored, defaults), row);
    }
}
