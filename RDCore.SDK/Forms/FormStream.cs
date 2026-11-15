namespace RDCore.SDK.Forms;

/// <summary>
/// A site of a parent control's form stream, read ([MS-OFORMS] 2.2.10.12): what the reader needs to find the control's
/// class and data, and what the control's <see cref="MSFormsSite"/> exposes.
/// </summary>
internal sealed record class RawSite(string Name, int Id, ushort ClsidCacheIndex, int Depth, MSFormsSite Site);

/// <summary>
/// A parent control's form stream (<c>f</c>), read: its <c>FormControl</c>'s properties, the class table, and the sites
/// of its embedded controls ([MS-OFORMS] 2.2.10).
/// </summary>
internal sealed record class FormStream(Block Block, IReadOnlyList<RawSite> Sites, IReadOnlyList<Guid?> ClassTable)
{
    // FormFlags ([MS-OFORMS] 2.5.6.1): FORM_FLAG_ENABLED, FORM_FLAG_DONTSAVECLASSTABLE
    private const uint FormEnabled = 1u << 2;
    private const uint DontSaveClassTable = 1u << 15;

    private static Slot[] FormControlSlots(string widthName, string heightName) =>
    [
        new(1, SlotType.Color, "BackColor", 0x8000000Fu),
        new(2, SlotType.Color, "ForeColor", 0x80000012u),
        new(3, SlotType.UInt32, "_NextAvailableID", 0),
        new(6, SlotType.Bits, "_BooleanProperties", 0x00000004u),
        new(7, SlotType.UInt8, "BorderStyle", 0),
        new(8, SlotType.UInt8, "MousePointer", 0),
        new(9, SlotType.UInt8, "_ScrollBars", 0x0C),
        new(10, SlotType.Size, widthName, (4000, 3000), heightName),
        new(11, SlotType.Size, "ScrollWidth", (4000, 3000), "ScrollHeight"),
        new(12, SlotType.Size, "ScrollLeft", (0, 0), "ScrollTop"),
        new(13, SlotType.Int32, "_GroupCnt", 0),
        new(15, SlotType.Picture, "MouseIcon"),
        new(16, SlotType.UInt8, "Cycle", 0),
        new(17, SlotType.UInt8, "SpecialEffect", 0),
        new(18, SlotType.Color, "BorderColor", 0x80000012u),
        new(19, SlotType.String, "Caption", ""),
        new(20, SlotType.Font, "Font"),
        new(21, SlotType.Picture, "Picture"),
        new(22, SlotType.UInt32, "Zoom", 100),
        new(23, SlotType.UInt8, "PictureAlignment", 2),
        new(24, SlotType.Flag, "PictureTiling", false),
        new(25, SlotType.UInt8, "PictureSizeMode", 0),
        new(26, SlotType.UInt32, "_ShapeCookie", 0),
        new(27, SlotType.UInt32, "DrawBuffer"),
    ];

    // a form's DisplayedSize is its inside size; a Frame's, a page's or a MultiPage's is its size (2.2.10.2)
    private static readonly Slot[] FormSlots = FormControlSlots("InsideWidth", "InsideHeight");
    private static readonly Slot[] ContainerSlots = FormControlSlots("Width", "Height");

    // OleSiteConcreteControl ([MS-OFORMS] 2.2.10.12.2), in mask order
    private static readonly Slot[] SiteSlots =
    [
        new(0, SlotType.String, "Name", ""),
        new(1, SlotType.String, "Tag", ""),
        new(2, SlotType.Int32, "ID", 0),
        new(3, SlotType.Int32, "HelpContextID", 0),
        new(4, SlotType.Bits, "BitFlags", 0x00000033u),
        new(5, SlotType.UInt32, "ObjectStreamSize", 0),
        new(6, SlotType.Int16, "TabIndex", -1),
        new(7, SlotType.UInt16, "ClsidCacheIndex", 0x7FFF),
        new(8, SlotType.Size, "Left", (0, 0), "Top"),
        new(9, SlotType.UInt16, "GroupID", 0),
        new(11, SlotType.String, "ControlTipText", ""),
        new(12, SlotType.String, "RuntimeLicKey", ""),
        new(13, SlotType.String, "ControlSource", ""),
        new(14, SlotType.String, "RowSource", ""),
    ];

    private static readonly Dictionary<string, object> SiteDefaults = PropertyBlock.Defaults(SiteSlots, MSFormsControlKind.Other);

    /// <summary>
    /// Reads the content of a form stream: a <c>FormControl</c> ([MS-OFORMS] 2.2.10.1), up to its sites.
    /// </summary>
    /// <exception cref="MSFormsFormatException">The stream is not a <c>FormControl</c> of a known version, or ends before
    /// its sites do.</exception>
    public static FormStream Read(ReadOnlySpan<byte> stream, MSFormsControlKind kind)
    {
        var reader = new ByteReader(stream);
        var block = PropertyBlock.Read(ref reader, kind == MSFormsControlKind.Form ? FormSlots : ContainerSlots, maskSize: 4, majorVersion: 4);

        // FormSiteData ([MS-OFORMS] 2.2.10.6)
        var classTable = new List<Guid?>();
        if ((block.Get("_BooleanProperties", 0x00000004u) & DontSaveClassTable) == 0)
        {
            var countOfSiteClassInfo = reader.ReadUInt16();
            for (var index = 0; index < countOfSiteClassInfo; index++)
            {
                classTable.Add(ReadSiteClassInfo(ref reader));
            }
        }

        var countOfSites = reader.ReadUInt32();
        reader.ReadUInt32(); // CountOfBytes
        if (countOfSites > stream.Length)
        {
            // every site takes at least a byte of SiteDepthsAndTypes: a count larger than the stream is damage
            throw new MSFormsFormatException(MSFormsFormatProblem.Truncated);
        }

        var depths = new List<int>((int)countOfSites);
        var depthsStart = reader.Position;
        while (depths.Count < countOfSites)
        {
            // FormObjectDepthTypeCount ([MS-OFORMS] 2.2.10.7): a depth, then a type or, with fCount, a count and a type
            var depth = reader.ReadByte();
            var typeOrCount = reader.ReadByte();
            var count = 1;
            if ((typeOrCount & 0x80) != 0)
            {
                count = typeOrCount & 0x7F;
                reader.ReadByte(); // OptionalType
            }

            for (var index = 0; index < count && depths.Count < countOfSites; index++)
            {
                depths.Add(depth);
            }
        }

        reader.AlignFrom(depthsStart, 4); // ArrayPadding

        var sites = new List<RawSite>((int)countOfSites);
        foreach (var depth in depths)
        {
            sites.Add(ReadSite(ref reader, depth));
        }

        // what follows the sites (FormDesignExData, 2.2.10.11) is the design surface's alone
        return new FormStream(block, sites, classTable);
    }

    /// <summary>
    /// The properties of the parent control of kind <paramref name="kind"/> whose <c>FormControl</c> this is, with its
    /// <c>BooleanProperties</c> and <c>ScrollBars</c> decoded ([MS-OFORMS] 2.5.6.1, 2.5.7).
    /// </summary>
    public MSFormsProperties PropertiesOf(MSFormsControlKind kind)
    {
        var slots = kind == MSFormsControlKind.Form ? FormSlots : ContainerSlots;
        var (stored, defaults) = ControlStructures.PropertiesOf(Block, slots, kind);
        AddFlags(defaults, 0x00000004u, 0x0C);
        if (Block.Values.TryGetValue("_BooleanProperties", out var flags))
        {
            stored["Enabled"] = ((uint)flags & FormEnabled) != 0;
        }

        if (Block.Values.TryGetValue("_ScrollBars", out var scrollBars))
        {
            // FormScrollBarFlags: bits 0 and 1 are the scroll bars shown, bits 2 and 3 those kept visible, as fmScrollBars
            stored["ScrollBars"] = (int)scrollBars & 3;
            stored["KeepScrollBarsVisible"] = ((int)scrollBars >> 2) & 3;
        }

        return new MSFormsProperties(kind, stored, defaults);
    }

    private static void AddFlags(Dictionary<string, object> into, uint booleanProperties, int scrollBars)
    {
        into["Enabled"] = (booleanProperties & FormEnabled) != 0;
        into["ScrollBars"] = scrollBars & 3;
        into["KeepScrollBarsVisible"] = (scrollBars >> 2) & 3;
    }

    // SiteClassInfo ([MS-OFORMS] 2.2.10.10.1): only its ClsID matters here
    private static Guid? ReadSiteClassInfo(ref ByteReader reader)
    {
        var start = reader.Position;
        reader.ReadUInt16(); // Version
        var cbClassTable = reader.ReadUInt16();
        var end = reader.Position + cbClassTable;
        var propMask = reader.ReadUInt32();

        // ClassInfoDataBlock (2.2.10.10.3): its values, in mask order, each aligned to its size from the structure's start
        Guid? classId = null;
        if ((propMask & 1u) != 0)
        {
            var sizes = new (uint Bit, int Size)[]
            {
                (1u << 4, 2), (1u << 4, 2), // fClassFlags: ClassTableFlags, VarFlags
                (1u << 5, 4), (1u << 6, 4), // CountOfMethods, DispidBind
                (1u << 7, 2), (1u << 8, 2), (1u << 9, 2), (1u << 10, 2), (1u << 11, 2), (1u << 12, 2), // GetBindIndex … ValueType
                (1u << 13, 4), (1u << 14, 2), // DispidRowset, SetRowset
            };
            foreach (var (bit, size) in sizes)
            {
                if ((propMask & bit) != 0)
                {
                    reader.AlignFrom(start, size);
                    reader.Skip(size);
                }
            }

            reader.AlignFrom(start, 4);
            classId = reader.ReadGuid(); // ClassInfoExtraDataBlock.ClsID, its first value
        }

        reader.Seek(end);
        return classId;
    }

    // OleSiteConcreteControl ([MS-OFORMS] 2.2.10.12.1); its SITE_FLAG bits (2.5.4.1)
    private static RawSite ReadSite(ref ByteReader reader, int depth)
    {
        var block = PropertyBlock.Read(ref reader, SiteSlots, maskSize: 4, majorVersion: null);
        T Value<T>(string name) => block.Values.TryGetValue(name, out var value) ? (T)value : (T)SiteDefaults[name];
        var flags = Value<uint>("BitFlags");
        bool Flag(int bit) => (flags & (1u << bit)) != 0;
        var site = new MSFormsSite(
            Value<double>("Left"),
            Value<double>("Top"),
            Value<int>("TabIndex"),
            Visible: Flag(1),
            TabStop: Flag(0),
            Default: Flag(2),
            Cancel: Flag(3),
            Streamed: Flag(4),
            AutoSize: Flag(5),
            PreserveHeight: Flag(8),
            FitToParent: Flag(9),
            SelectChild: Flag(13),
            PromoteControls: Flag(18),
            Tag: Value<string>("Tag"),
            ControlTipText: Value<string>("ControlTipText"),
            HelpContextId: Value<int>("HelpContextID"),
            ControlSource: Value<string>("ControlSource"),
            RowSource: Value<string>("RowSource"),
            RuntimeLicenseKey: Value<string>("RuntimeLicKey"),
            GroupId: Value<int>("GroupID"),
            ObjectStreamSize: Value<int>("ObjectStreamSize"));
        return new RawSite(Value<string>("Name"), Value<int>("ID"), (ushort)Value<int>("ClsidCacheIndex"), depth, site);
    }
}
