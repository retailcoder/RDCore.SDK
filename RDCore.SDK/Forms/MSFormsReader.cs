using System.Buffers.Binary;

namespace RDCore.SDK.Forms;

/// <summary>
/// Reads the designer data of a Microsoft Forms form: the storage a VBA UserForm persists itself to, which the VBE exports
/// to the form's <c>.frx</c> file ([MS-OFORMS] 2.1).
/// </summary>
/// <remarks>
/// The reader runs on any platform: no OLE32 and no Microsoft Forms. Its input is untrusted, so every size it reads is
/// bounded by the data it has; damaged data is an <see cref="MSFormsFormatException"/>, never a hang or an out-of-range
/// read. It reads, for each parent control (the form, a <c>Frame</c>, a <c>MultiPage</c> and its pages), the form stream
/// (<c>f</c>) with the parent's own properties and its controls' sites; the object stream (<c>o</c>), one piece per control
/// whose site says it is there; and a <c>MultiPage</c>'s page properties (<c>x</c>). The data of an ActiveX control that is
/// not one of Microsoft Forms' own is skipped.
/// </remarks>
public static class MSFormsReader
{
    private static ReadOnlySpan<byte> Signature => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private const int BlobHeaderSize = 24;

    // FormEmbeddedActiveXControlCached ([MS-OFORMS] 2.4.5); 7 (a form) and 15 (a generic MorphData) depend on their context
    private static readonly IReadOnlyDictionary<ushort, MSFormsControlKind> CachedKinds = new Dictionary<ushort, MSFormsControlKind>
    {
        [12] = MSFormsControlKind.Image,
        [14] = MSFormsControlKind.Frame,
        [16] = MSFormsControlKind.SpinButton,
        [17] = MSFormsControlKind.CommandButton,
        [18] = MSFormsControlKind.TabStrip,
        [21] = MSFormsControlKind.Label,
        [23] = MSFormsControlKind.TextBox,
        [24] = MSFormsControlKind.ListBox,
        [25] = MSFormsControlKind.ComboBox,
        [26] = MSFormsControlKind.CheckBox,
        [27] = MSFormsControlKind.OptionButton,
        [28] = MSFormsControlKind.ToggleButton,
        [47] = MSFormsControlKind.ScrollBar,
        [57] = MSFormsControlKind.MultiPage,
    };

    private const ushort FormCacheIndex = 7;
    private const ushort MorphDataCacheIndex = 15;
    private const ushort InvalidCacheIndex = 0x7FFF;
    private const ushort ClassTableIndex = 0x8000;

    // the properties of a MultiPage that its own FormControl holds; the others are its tab row's and its x stream's
    private static readonly string[] MultiPageFormProperties = ["BackColor", "ForeColor", "Width", "Height"];
    private static readonly string[] MultiPageTabRowProperties = ["Value", "Style", "TabOrientation", "MultiRow", "TabFixedWidth", "TabFixedHeight", "Font"];

    /// <summary>
    /// Reads the designer data in <paramref name="frx"/>.
    /// </summary>
    /// <param name="frx">The designer data as a <c>.frx</c> file holds it: the VBE's 24-byte blob header, whose bytes 4 to
    /// 7 are the length of the compound file ([MS-CFB]) that follows it; or that compound file alone, as
    /// <c>IPersistStorage</c> writes it.</param>
    /// <returns>The form, its properties and its controls.</returns>
    /// <exception cref="MSFormsFormatException">The data is not a form's designer data, or a damaged one
    /// (<see cref="MSFormsFormatException.Problem"/>).</exception>
    public static MSFormsDesign Read(ReadOnlySpan<byte> frx)
    {
        var (start, length) = CompoundFileRange(frx);
        var file = CompoundFile.Open(frx.Slice(start, length).ToArray());
        var (properties, children) = ReadParent(file, file.Root, MSFormsControlKind.Form, visited: []);
        return new MSFormsDesign(new MSFormsControl(string.Empty, 0, MSFormsControlKind.Form, null, null, properties, children));
    }

    // where the compound file is: after the blob header the VBE writes before it (undocumented: its bytes 4 to 7 are the
    // length of what follows it), or all of the data
    internal static (int Start, int Length) CompoundFileRange(ReadOnlySpan<byte> frx)
    {
        if (frx.StartsWith(Signature))
        {
            return (0, frx.Length);
        }

        if (frx.Length < BlobHeaderSize + Signature.Length || !frx[BlobHeaderSize..].StartsWith(Signature))
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.NotACompoundFile);
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(frx[4..]);
        return length > frx.Length - BlobHeaderSize
            ? throw new MSFormsFormatException(MSFormsFormatProblem.Truncated)
            : (BlobHeaderSize, (int)length);
    }

    /// <summary>
    /// A control being read: its site, its kind and class, its own properties, and its controls.
    /// </summary>
    /// <param name="site">The control's site.</param>
    /// <param name="kind">The control's kind, as its site's class gives it.</param>
    /// <param name="classId">The control's CLSID, when its site's class names one.</param>
    /// <param name="properties">The control's properties, until its data is read.</param>
    private sealed class Node(RawSite site, MSFormsControlKind kind, Guid? classId, MSFormsProperties properties)
    {
        public RawSite Site { get; } = site;

        public MSFormsControlKind Kind { get; set; } = kind;

        public Guid? ClassId { get; } = classId;

        public MSFormsProperties Properties { get; set; } = properties;

        public TabRow? TabRow { get; set; }

        // the controls of a parent control's own storage, then those its parent's form stream nests under it by depth
        public List<MSFormsControl> Children { get; set; } = [];

        public List<Node> Nested { get; } = [];

        public MSFormsControl ToControl()
            => new(Site.Name, Site.Id, Kind, ClassId, Site.Site, Properties, [.. Children, .. Nested.Select(node => node.ToControl())]);
    }

    // a parent control's storage ([MS-OFORMS] 2.1.2): its form stream's properties and sites, its object stream's pieces,
    // and the storages of its own parent controls
    private static (MSFormsProperties Properties, List<MSFormsControl> Children) ReadParent(CompoundFile file, CompoundFileEntry storage, MSFormsControlKind kind, HashSet<int> visited)
    {
        if (!visited.Add(storage.Id))
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.CorruptCompoundFile);
        }

        var form = file.Find(storage, "f") is { Type: CompoundFileEntryType.Stream } f
            ? FormStream.Read(file.Read(f), kind)
            : throw new MSFormsFormatException(MSFormsFormatProblem.MissingFormStream);
        var objects = file.Find(storage, "o") is { Type: CompoundFileEntryType.Stream } o ? file.Read(o) : [];

        // each site whose control is streamed takes the next ObjectStreamSize bytes of o, in site order (2.1.2.2.1), and
        // nothing of o is left over: the sizes are checked before any piece is read, so a wrong size is not misread as a
        // wrong piece
        var sizes = form.Sites.Where(site => site.Site.Streamed).Select(site => (long)site.Site.ObjectStreamSize).ToList();
        if (sizes.Any(size => size < 0) || sizes.Sum() != objects.Length)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.InvalidObjectStream);
        }

        // a site's depth places it under the closest preceding site one level up (2.2.10.7)
        var roots = new List<Node>();
        var open = new List<Node>();
        var offset = 0;
        foreach (var site in form.Sites)
        {
            var (siteKind, classId, isGenericMorphData) = KindOf(site, form.ClassTable, kind);
            var node = new Node(site, siteKind, classId, Empty(siteKind));
            if (site.Site.Streamed)
            {
                var size = site.Site.ObjectStreamSize;
                var piece = objects.AsSpan(offset, size);
                offset += size;
                if (!IsParent(siteKind) && siteKind != MSFormsControlKind.Other)
                {
                    var data = ControlStructures.Read(piece, siteKind, isGenericMorphData);
                    (node.Kind, node.Properties, node.TabRow) = (data.Kind, data.Properties, data.TabRow);
                }
            }
            else if (IsParent(siteKind) && file.Find(storage, $"i{site.Id:00}") is { Type: CompoundFileEntryType.Storage } own)
            {
                (node.Properties, node.Children) = ReadParent(file, own, siteKind, visited);
            }

            if (site.Depth > open.Count)
            {
                throw new MSFormsFormatException(MSFormsFormatProblem.InvalidControl);
            }

            open.RemoveRange(site.Depth, open.Count - site.Depth);
            (site.Depth == 0 ? roots : open[^1].Nested).Add(node);
            open.Add(node);
        }

        return kind == MSFormsControlKind.MultiPage
            ? ReadMultiPage(file, storage, form, roots)
            : (form.PropertiesOf(kind), [.. roots.Select(node => node.ToControl())]);
    }

    internal static bool IsParent(MSFormsControlKind kind)
        => kind is MSFormsControlKind.Form or MSFormsControlKind.Frame or MSFormsControlKind.MultiPage or MSFormsControlKind.Page;

    private static MSFormsProperties Empty(MSFormsControlKind kind) => new(kind, new Dictionary<string, object>(), new Dictionary<string, object>());

    // a site's kind by its ClsidCacheIndex ([MS-OFORMS] 2.5.13): a Microsoft Forms control's below 0x7FFF (2.4.5), a class
    // table entry's above 0x8000
    internal static (MSFormsControlKind Kind, Guid? ClassId, bool IsGenericMorphData) KindOf(RawSite site, IReadOnlyList<Guid?> classTable, MSFormsControlKind parentKind)
    {
        var index = site.ClsidCacheIndex;
        if (index >= ClassTableIndex)
        {
            var entry = index - ClassTableIndex;
            return entry < classTable.Count && classTable[entry] is { } classId
                ? (MSFormsControlKind.Other, classId, false)
                : throw new MSFormsFormatException(MSFormsFormatProblem.InvalidControl);
        }

        return index switch
        {
            // a MultiPage's pages are persisted as forms (2.1.2.3.1)
            FormCacheIndex => (parentKind == MSFormsControlKind.MultiPage ? MSFormsControlKind.Page : MSFormsControlKind.Form, null, false),

            // a generic MorphData control is a TextBox until its DisplayStyle says otherwise
            MorphDataCacheIndex => (MSFormsControlKind.TextBox, null, true),
            InvalidCacheIndex => throw new MSFormsFormatException(MSFormsFormatProblem.InvalidControl),
            _ => CachedKinds.TryGetValue(index, out var kind)
                ? (kind, null, false)
                : throw new MSFormsFormatException(MSFormsFormatProblem.InvalidControl),
        };
    }

    // PageProperties ([MS-OFORMS] 2.2.6.4.1) and MultiPageProperties (2.2.6.1)
    private static readonly Slot[] PageSlots =
    [
        new(1, SlotType.UInt32, "TransitionEffect", 0),
        new(2, SlotType.UInt32, "TransitionPeriod", 0),
    ];

    private static readonly Slot[] MultiPageSlots =
    [
        new(1, SlotType.Int32, "_PageCount", 0),
        new(2, SlotType.Int32, "_ID", 0),
        new(3, SlotType.Flag, "Enabled", true),
    ];

    // a MultiPage's storage (2.1.2.3): its form stream's sites are its tab row, a TabStrip in its o, and its pages; its x
    // stream holds a PageProperties per page after a first one, then a MultiPageProperties whose PageIDs give the pages'
    // order, which is also their tabs' order
    private static (MSFormsProperties Properties, List<MSFormsControl> Children) ReadMultiPage(CompoundFile file, CompoundFileEntry storage, FormStream form, List<Node> roots)
    {
        var tabRow = roots.FirstOrDefault(node => node.Kind == MSFormsControlKind.TabStrip);
        var pages = roots.Where(node => node.Kind == MSFormsControlKind.Page).ToList();
        var others = roots.Where(node => node != tabRow && node.Kind != MSFormsControlKind.Page);

        var pageProperties = new List<Block>();
        var multiPage = new Block(0);
        var pageIds = new List<int>();
        if (file.Find(storage, "x") is { Type: CompoundFileEntryType.Stream } x)
        {
            var reader = new ByteReader(file.Read(x));
            for (var index = 0; index <= pages.Count; index++)
            {
                pageProperties.Add(PropertyBlock.Read(ref reader, PageSlots, maskSize: 4, majorVersion: 2));
            }

            pageProperties.RemoveAt(0);
            multiPage = PropertyBlock.Read(ref reader, MultiPageSlots, maskSize: 4, majorVersion: 2);
            var pageCount = multiPage.Get("_PageCount", pages.Count);
            if (pageCount < 0 || pageCount > reader.Length / 4)
            {
                throw new MSFormsFormatException(MSFormsFormatProblem.Truncated);
            }

            for (var index = 0; index < pageCount; index++)
            {
                pageIds.Add(reader.ReadInt32());
            }
        }

        // the pages in PageIDs order; a page the list does not name keeps its site's place after them
        var ordered = pages
            .Select((page, index) => (Page: page, Order: pageIds.IndexOf(page.Site.Id) is >= 0 and var at ? at : pageIds.Count + index))
            .OrderBy(entry => entry.Order)
            .Select(entry => entry.Page)
            .ToList();
        // a page's tab is its place in that order; its PageProperties, its site's place among the pages
        for (var index = 0; index < ordered.Count; index++)
        {
            var page = ordered[index];
            var siteIndex = pages.IndexOf(page);
            page.Properties = PageProperties(page.Properties, tabRow?.TabRow, index, siteIndex < pageProperties.Count ? pageProperties[siteIndex] : null);
        }

        var (stored, defaults) = (new Dictionary<string, object>(), new Dictionary<string, object>());
        var own = form.PropertiesOf(MSFormsControlKind.MultiPage);
        Copy(own, MultiPageFormProperties, stored, defaults);
        var row = tabRow?.Properties
            ?? new MSFormsProperties(MSFormsControlKind.TabStrip, new Dictionary<string, object>(), PropertyBlock.Defaults(ControlStructures.TabStrip, MSFormsControlKind.TabStrip));
        Copy(row, MultiPageTabRowProperties, stored, defaults);

        var (multiStored, multiDefaults) = ControlStructures.PropertiesOf(multiPage, MultiPageSlots, MSFormsControlKind.MultiPage);
        defaults["Enabled"] = multiDefaults["Enabled"];
        if (multiStored.TryGetValue("Enabled", out var enabled))
        {
            stored["Enabled"] = enabled;
        }

        return (new MSFormsProperties(MSFormsControlKind.MultiPage, stored, defaults), [.. ordered.Concat(others).Select(node => node.ToControl())]);
    }

    // a page's properties: its FormControl's, then its tab's in the tab row (2.2.9), then its PageProperties
    private static MSFormsProperties PageProperties(MSFormsProperties own, TabRow? row, int tab, Block? page)
    {
        var stored = own.Stored.ToDictionary(StringComparer.OrdinalIgnoreCase);
        var defaults = own.Names.Where(name => !own.IsStored(name)).ToDictionary(name => name, name => own.Get<object>(name), StringComparer.OrdinalIgnoreCase);
        defaults["Caption"] = string.Empty;
        defaults["ControlTipText"] = string.Empty;
        defaults["Tag"] = string.Empty;
        defaults["Accelerator"] = '\0';
        defaults["Visible"] = true;
        defaults["Enabled"] = true;
        if (row is not null && tab < row.Tabs.Count)
        {
            var entry = row.Tabs[tab];
            Store(row.CaptionsStored, "Caption", entry.Caption);
            Store(row.TipsStored, "ControlTipText", entry.ControlTipText);
            Store(row.TagsStored, "Tag", entry.Tag);
            Store(row.AcceleratorsStored, "Accelerator", entry.Accelerator);
            Store(tab < row.FlagCount, "Visible", entry.Visible);
            Store(tab < row.FlagCount, "Enabled", entry.Enabled);
        }

        foreach (var (name, value) in PropertyBlock.Defaults(PageSlots, MSFormsControlKind.Page))
        {
            defaults[name] = value;
        }

        if (page is not null)
        {
            foreach (var (name, value) in page.Values)
            {
                stored[name] = value;
            }
        }

        return new MSFormsProperties(MSFormsControlKind.Page, stored, defaults);

        void Store(bool isStored, string name, object value)
        {
            if (isStored)
            {
                stored[name] = value;
            }
        }
    }

    private static void Copy(MSFormsProperties from, string[] names, Dictionary<string, object> stored, Dictionary<string, object> defaults)
    {
        foreach (var name in names)
        {
            if (from.Stored.TryGetValue(name, out var value))
            {
                stored[name] = value;
            }
            else if (from.TryGet(name, out var fallback))
            {
                defaults[name] = fallback;
            }
        }
    }
}
