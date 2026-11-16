using RDCore.SDK.Forms;
using static RDCore.Tests.Forms.TestCompoundFile;

namespace RDCore.Tests.Forms;

/// <remarks>
/// Byte-level tests: each one writes the structures it gives the reader (<see cref="MSFormsBytes"/>), in a compound file
/// of its own (<see cref="TestCompoundFile"/>), and reads them through <see cref="MSFormsReader.Read"/>.
/// </remarks>
[TestClass]
public sealed class MSFormsStructureTests
{
    private const double Tolerance = 0.005;

    private static double Points(int himetric) => himetric * 72.0 / 2540.0;

    // a form with one streamed control, of class cache index `cacheIndex`, whose data in o is `piece`
    private static MSFormsControl Only(int cacheIndex, byte[] piece)
    {
        var f = MSFormsBytes.FormStream(MSFormsBytes.Site("Control1", 1, cacheIndex, piece.Length));
        return MSFormsReader.Read(Write(Stream("f", f), Stream("o", piece))).Form.Children.Single();
    }

    private static MSFormsFormatProblem ProblemOf(Action read) => Assert.ThrowsExactly<MSFormsFormatException>(read).Problem;

    // a Label that stores its Size alone: 72 × 18 points
    private static byte[] PlainLabel()
        => new MSFormsBytes()
            .Structure(2, 1u << 5, _ => { }, extra => extra.I32(2540).I32(635))
            .TextProps()
            .ToArray();

    [TestMethod]
    public void Label_MaskBits_GiveTheirValues_WithTwoByteBorderStyleAndSpecialEffect()
    {
        // ForeColor (0), Caption (3), Size (5), MousePointer (6), BorderStyle (8), SpecialEffect (9)
        var piece = new MSFormsBytes()
            .Structure(
                2,
                1u << 0 | 1u << 3 | 1u << 5 | 1u << 6 | 1u << 8 | 1u << 9,
                data => data.U32(0x000000FF).U32(MSFormsBytes.Count("Hello")).U8(99).Align(2).U16(1).U16(6),
                extra => extra.String("Hello").I32(2540).I32(635))
            .TextProps()
            .ToArray();

        var label = Only(21, piece);

        Assert.AreEqual(MSFormsControlKind.Label, label.Kind);
        Assert.AreEqual(0x000000FFu, label.Properties.Get<uint>("ForeColor"));
        Assert.AreEqual("Hello", label.Properties.Get<string>("Caption"));
        Assert.AreEqual(99, label.Properties.Get<int>("MousePointer"));
        Assert.AreEqual(1, label.Properties.Get<int>("BorderStyle"));
        Assert.AreEqual(6, label.Properties.Get<int>("SpecialEffect"));
        Assert.AreEqual(72.0, label.Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual(18.0, label.Properties.Get<double>("Height"), Tolerance);
    }

    [TestMethod]
    public void Label_PropertiesNotStored_HaveTheFileFormatsDefaults()
    {
        var label = Only(21, PlainLabel()).Properties;

        Assert.IsFalse(label.IsStored("BackColor"));
        Assert.AreEqual(0x8000000Fu, label.Get<uint>("BackColor"));
        Assert.AreEqual(0x80000006u, label.Get<uint>("BorderColor"));
        Assert.AreEqual(7, label.Get<int>("PicturePosition"), "fmPicturePositionAboveCenter");
        Assert.IsTrue(label.Get<bool>("WordWrap"), "a Label's VariousPropertyBits default has fWordWrap set");
        Assert.IsFalse(label.Get<bool>("AutoSize"));
        Assert.AreEqual(1, label.Get<int>("TextAlign"));
        Assert.AreEqual(new MSFormsFont("MS Sans Serif", 8, false, false, false, false, 400, 1), label.Get<MSFormsFont>("Font"));
        Assert.IsFalse(label.TryGet<MSFormsPicture>("Picture", out _), "a picture has no default");
        Assert.IsTrue(label.IsStored("Width"));
        Assert.ThrowsExactly<KeyNotFoundException>(() => label.Get<string>("Value"));
        Assert.ThrowsExactly<InvalidCastException>(() => label.Get<string>("BackColor"));
    }

    [TestMethod]
    public void CommandButton_OneByteValue_PadsTheNextTwoByteValue_AndAFlagBitMeansNotTheDefault()
    {
        // MousePointer (6, 1 byte), Picture (7, 2-byte marker), Accelerator (8, 2 bytes, aligned), TakeFocusOnClick (9)
        var picture = new byte[] { (byte)'B', (byte)'M', 1, 2, 3 };
        var piece = new MSFormsBytes()
            .Structure(
                2,
                1u << 5 | 1u << 6 | 1u << 7 | 1u << 8 | 1u << 9,
                data => data.U8(1).Align(2).U16(0xFFFF).U16('G'),
                extra => extra.I32(2540).I32(635))
            .Picture(picture)
            .TextProps()
            .ToArray();

        var button = Only(17, piece).Properties;

        Assert.AreEqual(1, button.Get<int>("MousePointer"));
        Assert.AreEqual('G', button.Get<char>("Accelerator"));
        Assert.IsFalse(button.Get<bool>("TakeFocusOnClick"));
        CollectionAssert.AreEqual(picture, button.Get<MSFormsPicture>("Picture").Data.ToArray());
        Assert.AreEqual(MSFormsPictureFormat.Bitmap, button.Get<MSFormsPicture>("Picture").Format);
    }

    [TestMethod]
    public void CommandButton_PictureAndMouseIcon_AreInMaskOrder()
    {
        // Picture (7) then MouseIcon (10) in the StreamData
        var piece = new MSFormsBytes()
            .Structure(2, 1u << 5 | 1u << 7 | 1u << 10, data => data.U16(0xFFFF).U16(0xFFFF), extra => extra.I32(1).I32(1))
            .Picture([0xFF, 0xD8, 0xFF, 0xE0])
            .Picture([0, 0, 2, 0])
            .TextProps()
            .ToArray();

        var button = Only(17, piece).Properties;

        Assert.AreEqual(MSFormsPictureFormat.Jpeg, button.Get<MSFormsPicture>("Picture").Format);
        Assert.AreEqual(MSFormsPictureFormat.Cursor, button.Get<MSFormsPicture>("MouseIcon").Format);
    }

    [TestMethod]
    [DataRow(0x00020000u, 0, DisplayName = "LeftTop")]
    [DataRow(0x00040004u, 12, DisplayName = "Center")]
    [DataRow(0x00010007u, 10, DisplayName = "BelowCenter")]
    [DataRow(0x00123456u, 0x00123456, DisplayName = "a value outside the table is kept")]
    public void CommandButton_PicturePosition_IsTheObjectModelsValue(uint stored, int expected)
    {
        var piece = new MSFormsBytes()
            .Structure(2, 1u << 4 | 1u << 5, data => data.U32(stored), extra => extra.I32(1).I32(1))
            .TextProps()
            .ToArray();

        Assert.AreEqual(expected, Only(17, piece).Properties.Get<int>("PicturePosition"));
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2, 3)]
    [DataRow(3, 2)]
    public void TextProps_ParagraphAlign_IsTheObjectModelsTextAlign(int paragraphAlign, int textAlign)
    {
        var piece = new MSFormsBytes()
            .Structure(2, 1u << 5, _ => { }, extra => extra.I32(1).I32(1))
            .Structure(2, 1u << 6, data => data.U8(paragraphAlign))
            .ToArray();

        Assert.AreEqual(textAlign, Only(17, piece).Properties.Get<int>("TextAlign"));
    }

    [TestMethod]
    public void TextProps_StoredFont_IsReadInTwips()
    {
        // FontName (0), FontEffects (1: italic, underline), FontHeight (2: 240 twips), FontCharSet (4), FontWeight (7)
        var piece = new MSFormsBytes()
            .Structure(2, 1u << 5, _ => { }, extra => extra.I32(1).I32(1))
            .Structure(2, 1u << 0 | 1u << 1 | 1u << 2 | 1u << 4 | 1u << 7, data => data.U32(MSFormsBytes.Count("Arial")).U32(6).U32(240).U8(0).Align(2).U16(300), extra => extra.String("Arial"))
            .ToArray();

        Assert.AreEqual(new MSFormsFont("Arial", 12, false, true, true, false, 300, 0), Only(21, piece).Properties.Get<MSFormsFont>("Font"));
    }

    // a MorphDataControl: its 8-byte mask, its DataBlock, its ExtraDataBlock (Size first), then a TextProps
    private static MSFormsBytes MorphData(ulong mask, Action<MSFormsBytes> data, Action<MSFormsBytes>? extra = null)
        => new MSFormsBytes().Structure(
            2,
            mask | 1UL << 8 | 1UL << 31,
            data,
            bytes =>
            {
                bytes.I32(2540).I32(635);
                extra?.Invoke(bytes);
            },
            maskSize: 8).TextProps();

    [TestMethod]
    public void MorphData_TextBox_DefaultsWhenNotStored()
    {
        var box = Only(23, MorphData(0, _ => { }).ToArray()).Properties;

        Assert.AreEqual(0x80000005u, box.Get<uint>("BackColor"));
        Assert.AreEqual(2, box.Get<int>("SpecialEffect"), "fmSpecialEffectSunken");
        Assert.IsTrue(box.Get<bool>("IntegralHeight"));
        Assert.IsTrue(box.Get<bool>("HideSelection"));
        Assert.IsFalse(box.Get<bool>("MultiLine"));
        Assert.AreEqual(0, box.Get<int>("MaxLength"));
        Assert.AreEqual("", box.Get<string>("Value"));
        Assert.IsFalse(box.TryGet<int>("MatchEntry", out _), "MatchEntry is a list's");
        Assert.IsFalse(box.TryGet<string>("GroupName", out _), "GroupName is a CheckBox's or an OptionButton's");
    }

    [TestMethod]
    public void MorphData_VariousPropertyBits_GiveTheirNamedProperties()
    {
        // fEnabled clear, fLocked, fMultiLine, fAutoSize, fWordWrap clear: Reserved1 and Reserved2 set
        var bits = 1u | 1u << 2 | 1u << 3 | 1u << 4 | 1u << 28 | 1u << 31;
        var box = Only(23, MorphData(1, data => data.U32(bits)).ToArray()).Properties;

        Assert.IsFalse(box.Get<bool>("Enabled"));
        Assert.IsTrue(box.Get<bool>("Locked"));
        Assert.IsTrue(box.Get<bool>("MultiLine"));
        Assert.IsTrue(box.Get<bool>("AutoSize"));
        Assert.IsFalse(box.Get<bool>("WordWrap"));
        Assert.IsFalse(box.Get<bool>("IntegralHeight"));
        Assert.IsTrue(box.IsStored("Enabled"));
    }

    [TestMethod]
    public void MorphData_GroupName_IsTheMasksHighHalf()
    {
        var option = Only(
                27,
                MorphData(1UL << 6 | 1UL << 32, data => data.U8(5).Align(4).U32(MSFormsBytes.Count("Group1")), extra => extra.String("Group1")).ToArray())
            .Properties;

        Assert.AreEqual("Group1", option.Get<string>("GroupName"));
        Assert.AreEqual(1, option.Get<int>("Alignment"));
    }

    [TestMethod]
    [DataRow(1, MSFormsControlKind.TextBox)]
    [DataRow(2, MSFormsControlKind.ListBox)]
    [DataRow(3, MSFormsControlKind.ComboBox)]
    [DataRow(4, MSFormsControlKind.CheckBox)]
    [DataRow(5, MSFormsControlKind.OptionButton)]
    [DataRow(6, MSFormsControlKind.ToggleButton)]
    [DataRow(7, MSFormsControlKind.ComboBox)]
    public void MorphData_GenericCacheIndex_IsTheKindItsDisplayStyleSays(int displayStyle, MSFormsControlKind kind)
        => Assert.AreEqual(kind, Only(15, MorphData(1UL << 6, data => data.U8(displayStyle)).ToArray()).Kind);

    [TestMethod]
    public void MorphData_GenericCacheIndex_WithAnUnknownDisplayStyle_Throws()
        => Assert.AreEqual(MSFormsFormatProblem.InvalidControl, ProblemOf(() => Only(15, MorphData(1UL << 6, data => data.U8(9)).ToArray())));

    [TestMethod]
    public void MorphData_ComboBox_DropDownList_IsItsStyle()
        => Assert.AreEqual(2, Only(25, MorphData(1UL << 6, data => data.U8(7)).ToArray()).Properties.Get<int>("Style"));

    [TestMethod]
    public void MorphData_ListBox_ReadsItsColumnWidths()
    {
        // cColumnInfo (15) = 2: a MorphDataColumnInfo per column after the TextProps, the second with no width set
        var piece = MorphData(1UL << 15, data => data.U16(2))
            .Structure(2, 1, data => data.I32(2540))
            .Structure(2, 0, _ => { })
            .ToArray();

        var list = Only(24, piece).Properties;

        CollectionAssert.AreEqual(new[] { 72.0, -1.0 }, list.Get<IReadOnlyList<double>>("ColumnWidths").ToArray());
        Assert.AreEqual(1, list.Get<int>("ColumnCount"));
        Assert.AreEqual(-1, list.Get<int>("TextColumn"));
    }

    [TestMethod]
    public void Image_FlagsAndPicture_AreRead()
    {
        // AutoSize (2, flag), PictureSizeMode (7), Size (9), Picture (10), PictureTiling (12, flag)
        var piece = new MSFormsBytes()
            .Structure(2, 1u << 2 | 1u << 7 | 1u << 9 | 1u << 10 | 1u << 12, data => data.U8(3).Align(2).U16(0xFFFF), extra => extra.I32(1270).I32(1270))
            .Picture([(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a'])
            .ToArray();

        var image = Only(12, piece).Properties;

        Assert.IsTrue(image.Get<bool>("AutoSize"));
        Assert.IsTrue(image.Get<bool>("PictureTiling"));
        Assert.AreEqual(3, image.Get<int>("PictureSizeMode"));
        Assert.AreEqual(2, image.Get<int>("PictureAlignment"));
        Assert.AreEqual(MSFormsPictureFormat.Gif, image.Get<MSFormsPicture>("Picture").Format);
        Assert.AreEqual(36.0, image.Get<double>("Width"), Tolerance);
    }

    [TestMethod]
    public void ScrollBar_IntegersAndTheTwoByteProportionalThumb_AreRead()
    {
        // Size (3), Min (5), Max (6), Position (7), ProportionalThumb (14, 2 bytes), Delay (15, aligned to 4)
        var piece = new MSFormsBytes()
            .Structure(2, 1u << 3 | 1u << 5 | 1u << 6 | 1u << 7 | 1u << 14 | 1u << 15, data => data.I32(-5).I32(5).I32(2).U16(0).Align(4).U32(100), extra => extra.I32(1).I32(1))
            .ToArray();

        var scroll = Only(47, piece).Properties;

        Assert.AreEqual(-5, scroll.Get<int>("Min"));
        Assert.AreEqual(5, scroll.Get<int>("Max"));
        Assert.AreEqual(2, scroll.Get<int>("Value"));
        Assert.IsFalse(scroll.Get<bool>("ProportionalThumb"));
        Assert.AreEqual(100, scroll.Get<int>("Delay"));
        Assert.AreEqual(1, scroll.Get<int>("LargeChange"));
    }

    [TestMethod]
    public void SpinButton_MousePointer_FollowsItsMouseIconMarker()
    {
        // Size (3), Max (6), MouseIcon (13, 2-byte marker), MousePointer (14, 1 byte)
        var piece = new MSFormsBytes()
            .Structure(2, 1u << 3 | 1u << 6 | 1u << 13 | 1u << 14, data => data.I32(10).U16(0xFFFF).U8(99), extra => extra.I32(1).I32(1))
            .Picture([0, 0, 1, 0])
            .ToArray();

        var spin = Only(16, piece).Properties;

        Assert.AreEqual(10, spin.Get<int>("Max"));
        Assert.AreEqual(99, spin.Get<int>("MousePointer"));
        Assert.AreEqual(MSFormsPictureFormat.Icon, spin.Get<MSFormsPicture>("MouseIcon").Format);
    }

    // a TabStrip with two tabs: their captions (compressed) and tooltips (the second uncompressed), and their flags
    private static byte[] TabStrip(uint firstFlags, uint secondFlags)
    {
        var tips = new MSFormsBytes().U32(0).U32(2).Raw("Hi"u8.ToArray().SelectMany(c => new[] { c, (byte)0 }).ToArray()).ToArray();
        var captions = new MSFormsBytes().U32(MSFormsBytes.Count("One")).Raw("One"u8.ToArray()).U8(0).U32(MSFormsBytes.Count("Two")).Raw("Two"u8.ToArray()).U8(0).ToArray();
        return new MSFormsBytes()
            .Structure(
                2,
                1u << 4 | 1u << 5 | 1u << 15 | 1u << 19 | 1u << 22,
                data => data.U32((uint)captions.Length).U32((uint)tips.Length).U32(2),
                extra => extra.I32(2540).I32(635).Raw(captions).Raw(tips))
            .TextProps()
            .U32(firstFlags)
            .U32(secondFlags)
            .ToArray();
    }

    [TestMethod]
    public void TabStrip_ReadsItsTabsArraysAndFlags()
    {
        var tabs = Only(18, TabStrip(3, 1)).Properties.Get<IReadOnlyList<MSFormsTab>>("Tabs");

        CollectionAssert.AreEqual(
            new[] { new MSFormsTab("One", "", "", "", '\0', true, true), new MSFormsTab("Two", "", "Hi", "", '\0', true, false) },
            tabs.ToArray());
    }

    [TestMethod]
    public void MultiPage_OrdersItsPagesByThePageIdsOfItsXStream()
    {
        // pages 3 and 2, in that site order; PageIDs say 2 then 3; the tab row captions them "B" then "C"
        var captions = new MSFormsBytes().U32(MSFormsBytes.Count("B")).Raw((byte)'B').Align(4).U32(MSFormsBytes.Count("C")).Raw((byte)'C').Align(4).ToArray();
        var tabRow = new MSFormsBytes()
            .Structure(2, 1u << 4 | 1u << 5 | 1u << 19, data => data.U32((uint)captions.Length), extra => extra.I32(1).I32(1).Raw(captions))
            .TextProps()
            .ToArray();
        var x = new MSFormsBytes();
        for (var page = 0; page < 3; page++)
        {
            x.Structure(2, page == 2 ? 1u << 1 : 0, data => data.U32(5)); // the second page in site order (ID 2) has TransitionEffect 5
        }

        x.Structure(2, 1u << 1 | 1u << 3, data => data.I32(2)).I32(2).I32(3); // PageCount 2, not enabled, then PageIDs
        var page2 = Storage("i02", Stream("f", MSFormsBytes.FormStream()));
        var page3 = Storage("i03", Stream("f", MSFormsBytes.FormStream()));
        var multiPage = Storage(
            "i01",
            Stream("f", MSFormsBytes.FormStream(MSFormsBytes.Site("", 4, 18, tabRow.Length), MSFormsBytes.Site("Page3", 3, 7, null), MSFormsBytes.Site("Page2", 2, 7, null))),
            Stream("o", tabRow),
            Stream("x", x.ToArray()),
            page3,
            page2);
        var root = Stream("f", MSFormsBytes.FormStream(MSFormsBytes.Site("Pages", 1, 57, null)));

        var pages = MSFormsReader.Read(Write(root, multiPage)).Form.Children.Single();

        Assert.AreEqual(MSFormsControlKind.MultiPage, pages.Kind);
        Assert.IsFalse(pages.Properties.Get<bool>("Enabled"));
        CollectionAssert.AreEqual(new[] { "Page2", "Page3" }, pages.Children.Select(page => page.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "B", "C" }, pages.Children.Select(page => page.Properties.Get<string>("Caption")).ToArray());
        Assert.AreEqual(5, pages.Children[0].Properties.Get<int>("TransitionEffect"));
        Assert.AreEqual(0, pages.Children[1].Properties.Get<int>("TransitionEffect"));
    }

    [TestMethod]
    public void Form_ScrollBarsAndBooleanProperties_AreDecoded()
    {
        // BooleanProperties (6): not enabled, class table not saved; ScrollBars (9): vertical, keep both; Caption (19)
        var f = MSFormsBytes.FormStream(
            1u << 6 | 1u << 9 | 1u << 19,
            data => data.U32(1u << 15).U8(0x0E).Align(4).U32(MSFormsBytes.Count("Title")),
            extra => extra.String("Title"));

        var form = MSFormsReader.Read(Write(Stream("f", f))).Form.Properties;

        Assert.IsFalse(form.Get<bool>("Enabled"));
        Assert.AreEqual(2, form.Get<int>("ScrollBars"));
        Assert.AreEqual(3, form.Get<int>("KeepScrollBarsVisible"));
        Assert.AreEqual("Title", form.Get<string>("Caption"));
        Assert.AreEqual(100, form.Get<int>("Zoom"));
    }

    [TestMethod]
    public void Form_StdFontAndPicture_AreReadFromItsStreamData()
    {
        // Font (20) then Picture (21): StdFont "Arial", bold and italic, 9.75 points
        var f = new MSFormsBytes()
            .Structure(4, 1u << 20 | 1u << 21, data => data.U16(0xFFFF).U16(0xFFFF))
            .Guid(new Guid("0BE35203-8F91-11CE-9DE3-00AA004BB851"))
            .U8(1).U16(0).U8(0x03).U16(700).U32(97500).U8(5).Raw("Arial"u8.ToArray())
            .Picture([1, 0, 9, 0])
            .U16(0).U32(0).U32(0)
            .ToArray();

        var form = MSFormsReader.Read(Write(Stream("f", f))).Form.Properties;

        Assert.AreEqual(new MSFormsFont("Arial", 9.75, true, true, false, false, 700, 0), form.Get<MSFormsFont>("Font"));
        Assert.AreEqual(MSFormsPictureFormat.Metafile, form.Get<MSFormsPicture>("Picture").Format);
    }

    [TestMethod]
    public void Site_StoredStringsAndFlags_AreRead()
    {
        // Name (0), Tag (1), BitFlags (4: Visible, Default, Streamed), ObjectStreamSize (5), ClsidCacheIndex (7),
        // Position (8), ControlTipText (11), RowSource (14)
        var piece = PlainLabel();
        var site = new MSFormsBytes().Structure(
            0,
            1u << 0 | 1u << 1 | 1u << 4 | 1u << 5 | 1u << 7 | 1u << 8 | 1u << 11 | 1u << 14,
            data => data.U32(MSFormsBytes.Count("L")).U32(MSFormsBytes.Count("tag")).U32(0x16).I32(piece.Length).U16(21).Align(4)
                .U32(MSFormsBytes.Count("tip")).U32(MSFormsBytes.Count("A1:A3")),
            extra => extra.String("L").String("tag").I32(2540).I32(1270).String("tip").String("A1:A3")).ToArray();

        var label = MSFormsReader.Read(Write(Stream("f", MSFormsBytes.FormStream(site)), Stream("o", piece))).Form.Children.Single();

        Assert.AreEqual("L", label.Name);
        Assert.AreEqual(new MSFormsSite(72, 36, -1, true, false, true, false, true, false, false, false, false, false, "tag", "tip", 0, "", "A1:A3", "", 0, piece.Length), label.Site);
    }

    [TestMethod]
    public void ActiveXControl_IsNamedByItsClassTableEntry_AndItsDataIsSkipped()
    {
        // a class table of one SiteClassInfo that stores its ClsID alone; the site's class is 0x8000, its data 3 bytes
        var classId = new Guid("8856F961-340A-11D0-A96B-00C04FD705A2");
        var classInfo = new MSFormsBytes().Structure(0, 1, _ => { }, extra => extra.Guid(classId)).ToArray();
        var bytes = new MSFormsBytes().Structure(4, 0, _ => { }).U16(1).Raw(classInfo).U32(1).U32(0);
        var depthsStart = bytes.Length;
        bytes.U8(0).U8(1).AlignFrom(depthsStart, 4).Raw(MSFormsBytes.Site("Browser", 1, 0x8000, 3));

        var control = MSFormsReader.Read(Write(Stream("f", bytes.ToArray()), Stream("o", [1, 2, 3]))).Form.Children.Single();

        Assert.AreEqual(MSFormsControlKind.Other, control.Kind);
        Assert.AreEqual(classId, control.ClassId);
        Assert.AreEqual("Browser", control.Name);
    }

    [TestMethod]
    public void ObjectStream_LongerThanItsSitesSay_Throws()
        => Assert.AreEqual(
            MSFormsFormatProblem.InvalidObjectStream,
            ProblemOf(() => MSFormsReader.Read(Write(Stream("f", MSFormsBytes.FormStream(MSFormsBytes.Site("L", 1, 21, PlainLabel().Length))), Stream("o", [.. PlainLabel(), 0, 0, 0, 0])))));

    [TestMethod]
    public void ObjectStream_ShorterThanItsSitesSay_Throws()
        => Assert.AreEqual(
            MSFormsFormatProblem.InvalidObjectStream,
            ProblemOf(() => MSFormsReader.Read(Write(Stream("f", MSFormsBytes.FormStream(MSFormsBytes.Site("L", 1, 21, 100))), Stream("o", PlainLabel())))));

    [TestMethod]
    public void ControlData_CutShortByItsSite_Throws()
    {
        // the site gives the Label fewer bytes than its own structure says it has
        var piece = PlainLabel()[..10];

        Assert.AreEqual(MSFormsFormatProblem.Truncated, ProblemOf(() => Only(21, piece)));
    }

    [TestMethod]
    public void ControlData_OfAnUnknownVersion_Throws()
    {
        var piece = PlainLabel();
        piece[1] = 3;

        Assert.AreEqual(MSFormsFormatProblem.UnsupportedVersion, ProblemOf(() => Only(21, piece)));
    }

    [TestMethod]
    public void FormStream_CutShort_Throws()
    {
        var f = MSFormsBytes.FormStream(MSFormsBytes.Site("L", 1, 21, 4));

        Assert.AreEqual(MSFormsFormatProblem.Truncated, ProblemOf(() => MSFormsReader.Read(Write(Stream("f", f[..^6])))));
    }

    [TestMethod]
    public void Storage_WithoutAFormStream_Throws()
        => Assert.AreEqual(MSFormsFormatProblem.MissingFormStream, ProblemOf(() => MSFormsReader.Read(Write(Stream("o", [])))));

    [TestMethod]
    [DataRow(0x7FFF)]
    [DataRow(99)]
    [DataRow(0x8000)]
    public void Site_WithAClassTheFormDoesNotDescribe_Throws(int cacheIndex)
        => Assert.AreEqual(MSFormsFormatProblem.InvalidControl, ProblemOf(() => Only(cacheIndex, PlainLabel())));

    // the VBE's blob header: "LB", version 8, the length of the compound file that follows, then four more integers
    private static byte[] WithBlobHeader(byte[] compoundFile, int length)
    {
        var frx = new byte[24 + compoundFile.Length];
        frx[0] = (byte)'L';
        frx[1] = (byte)'B';
        frx[2] = 8;
        BitConverter.GetBytes(length).CopyTo(frx, 4);
        compoundFile.CopyTo(frx, 24);
        return frx;
    }

    [TestMethod]
    public void Read_FrxWithABlobHeader_ReadsTheCompoundFileAfterIt()
    {
        var file = Write(Stream("f", MSFormsBytes.FormStream(1u << 22, data => data.U32(150), null)));

        Assert.AreEqual(150, MSFormsReader.Read(WithBlobHeader(file, file.Length)).Form.Properties.Get<int>("Zoom"));
    }

    [TestMethod]
    public void Read_BlobHeaderLengthPastTheData_Throws()
    {
        var file = Write(Stream("f", MSFormsBytes.FormStream()));

        Assert.AreEqual(MSFormsFormatProblem.Truncated, ProblemOf(() => MSFormsReader.Read(WithBlobHeader(file, file.Length + 1))));
    }

    [TestMethod]
    public void Read_DataThatIsNoCompoundFile_Throws()
        => Assert.AreEqual(MSFormsFormatProblem.NotACompoundFile, ProblemOf(() => MSFormsReader.Read(new byte[1024])));

    [TestMethod]
    public void Exception_Message_IsLocalized()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            var english = new MSFormsFormatException(MSFormsFormatProblem.Truncated).Message;
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("fr-CA");
            var french = new MSFormsFormatException(MSFormsFormatProblem.Truncated).Message;

            Assert.IsFalse(string.IsNullOrWhiteSpace(english));
            Assert.IsFalse(string.IsNullOrWhiteSpace(french));
            Assert.AreNotEqual(english, french);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = culture;
        }
    }
}
