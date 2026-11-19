using RDCore.SDK.Forms;

namespace RDCore.Tests.Forms;

/// <remarks>
/// Golden tests on real designer data. <c>ExampleView.frx</c>, <c>ExampleDynamicView.frx</c>,
/// <c>ExploreTextboxEvents.frx</c> and <c>Resources.frx</c> are forms of rubberduck-vba/MVVM (MIT), exported by the VBE;
/// <c>FormSizeProbe.frx</c> is an empty form whose inside size was measured in Excel (333 × 285 points);
/// <c>AllControls.storage</c> is a form's storage written by Microsoft Forms 2.0 itself (<c>IPersistStorage.Save</c>),
/// with no blob header. Each expected value was decoded by hand from the file's bytes.
/// </remarks>
[TestClass]
public sealed class MSFormsReaderTests
{
    private const double Tolerance = 0.005;

    internal static byte[] Fixture(string fileName)
    {
        var assembly = typeof(MSFormsReaderTests).Assembly;
        var name = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith($".Fixtures.{fileName}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static MSFormsDesign Read(string fileName) => MSFormsReader.Read(Fixture(fileName));

    private static double Points(int himetric) => himetric * 72.0 / 2540.0;

    private static MSFormsControl Control(MSFormsDesign design, string name)
        => design.Form.DescendantsAndSelf().Single(control => control.Name == name);

    private static string Tree(IEnumerable<MSFormsControl> controls, int depth = 0)
        => string.Concat(controls.Select(control =>
            $"{new string(' ', depth * 2)}{control.Name} : {control.Kind} #{control.Id} [{control.Site?.TabIndex}]\n{Tree(control.Children, depth + 1)}"));

    [TestMethod]
    public void ExampleView_ReadsItsControlsKindsAndNesting()
    {
        var design = Read("ExampleView.frx");

        Assert.AreEqual(MSFormsControlKind.Form, design.Form.Kind);
        Assert.IsNull(design.Form.Site);
        Assert.AreEqual(
            "AmountBox : TextBox #1 [0]\n" +
            "CancelButton : CommandButton #2 [1]\n" +
            "OkButton : CommandButton #3 [2]\n" +
            "BrowseButton : CommandButton #4 [3]\n" +
            "ValidationMessage1 : Label #7 [4]\n" +
            "OptionsFrame : Frame #8 [5]\n" +
            "  OptionButton1 : OptionButton #5 [0]\n" +
            "  OptionButton2 : OptionButton #6 [1]\n" +
            "InstructionsLabel : Label #9 [6]\n" +
            "TextBox1 : TextBox #11 [7]\n" +
            "ValidationMessage2 : Label #12 [8]\n" +
            "ValidationMessage3 : Label #13 [9]\n" +
            "ComboBox1 : ComboBox #14 [10]\n",
            Tree(design.Form.Children));
    }

    [TestMethod]
    public void ExampleView_Form_ReadsItsFormControl()
    {
        var form = Read("ExampleView.frx").Form.Properties;

        Assert.AreEqual(0x00FFFFFFu, form.Get<uint>("BackColor"));
        Assert.AreEqual(0x80000012u, form.Get<uint>("ForeColor"), "the default, not stored");
        Assert.IsFalse(form.IsStored("ForeColor"));
        Assert.AreEqual(Points(11578), form.Get<double>("InsideWidth"), Tolerance);
        Assert.AreEqual(Points(8319), form.Get<double>("InsideHeight"), Tolerance);
        Assert.AreEqual(32000, form.Get<int>("DrawBuffer"));
        Assert.IsFalse(form.IsStored("Caption"), "a UserForm's caption is in its module");
        Assert.AreEqual(new MSFormsFont("Tahoma", 7.8, false, false, false, false, 400, 0), form.Get<MSFormsFont>("Font"));
    }

    [TestMethod]
    public void ExampleView_TextBox_ReadsItsSiteAndMorphData()
    {
        var box = Control(Read("ExampleView.frx"), "AmountBox");

        Assert.AreEqual(Points(423), box.Site!.Left, Tolerance);
        Assert.AreEqual(Points(1482), box.Site.Top, Tolerance);
        Assert.IsTrue(box.Site.Visible);
        Assert.IsTrue(box.Site.TabStop);
        Assert.IsTrue(box.Site.Streamed);
        Assert.AreEqual(60, box.Site.ObjectStreamSize);
        Assert.AreEqual(Points(4869), box.Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual(18.0, box.Properties.Get<double>("Height"), Tolerance);
        Assert.AreEqual(1, box.Properties.Get<int>("BorderStyle"));
        Assert.AreEqual(0, box.Properties.Get<int>("SpecialEffect"));
        Assert.IsTrue(box.Properties.IsStored("SpecialEffect"));
        Assert.IsTrue(box.Properties.Get<bool>("Enabled"));
        Assert.IsFalse(box.Properties.Get<bool>("MultiLine"));
        Assert.IsTrue(box.Properties.Get<bool>("IntegralHeight"));
        Assert.AreEqual(1, box.Properties.Get<int>("BackStyle"));
        Assert.AreEqual(0x80000005u, box.Properties.Get<uint>("BackColor"));
        Assert.AreEqual(new MSFormsFont("Tahoma", 7.8, false, false, false, false, 400, 0), box.Properties.Get<MSFormsFont>("Font"));
        Assert.IsFalse(box.Properties.TryGet<string>("Caption", out _), "a TextBox has no Caption");
    }

    [TestMethod]
    public void ExampleView_CommandButtons_ReadTheirCaptionsAndTheCancelFlag()
    {
        var design = Read("ExampleView.frx");
        var cancel = Control(design, "CancelButton");

        Assert.AreEqual("Cancel", cancel.Properties.Get<string>("Caption"));
        Assert.IsTrue(cancel.Site!.Cancel);
        Assert.IsFalse(cancel.Site.Default);
        Assert.AreEqual(72.0, cancel.Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual(18.0, cancel.Properties.Get<double>("Height"), Tolerance);
        Assert.AreEqual(2, cancel.Properties.Get<int>("TextAlign"), "ParagraphAlign 3 is fmTextAlignCenter");
        Assert.IsTrue(cancel.Properties.Get<bool>("TakeFocusOnClick"));
        Assert.AreEqual("Ok", Control(design, "OkButton").Properties.Get<string>("Caption"));
        Assert.IsFalse(Control(design, "OkButton").Site!.Cancel);
        Assert.AreEqual("...", Control(design, "BrowseButton").Properties.Get<string>("Caption"));
    }

    [TestMethod]
    public void ExampleView_Label_ReadsItsCaptionBackStyleAndFont()
    {
        var label = Control(Read("ExampleView.frx"), "ValidationMessage1");

        Assert.AreEqual("Label1", label.Properties.Get<string>("Caption"));
        Assert.AreEqual(0, label.Properties.Get<int>("BackStyle"), "transparent");
        Assert.IsTrue(label.Properties.Get<bool>("WordWrap"));
        Assert.IsFalse(label.Site!.TabStop);
        Assert.AreEqual(Points(5715), label.Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual(new MSFormsFont("Tahoma", 7.8, false, false, false, false, 400, 0), label.Properties.Get<MSFormsFont>("Font"));
    }

    [TestMethod]
    public void ExampleView_Label_ReadsABoldFontFromItsTextProps()
    {
        var font = Control(Read("ExampleView.frx"), "InstructionsLabel").Properties.Get<MSFormsFont>("Font");

        Assert.IsTrue(font.Bold);
        Assert.AreEqual(700, font.Weight);
    }

    [TestMethod]
    public void ExampleView_Frame_ReadsItsOwnFormControl()
    {
        var frame = Control(Read("ExampleView.frx"), "OptionsFrame");

        Assert.AreEqual(MSFormsControlKind.Frame, frame.Kind);
        Assert.AreEqual("Options", frame.Properties.Get<string>("Caption"));
        Assert.AreEqual(1, frame.Properties.Get<int>("BorderStyle"));
        Assert.AreEqual(162.0, frame.Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual(Points(1482), frame.Properties.Get<double>("Height"), Tolerance);
        Assert.IsTrue(frame.Properties.Get<bool>("Enabled"));
        Assert.AreEqual(new MSFormsFont("Tahoma", 6, false, false, false, false, 400, 0), frame.Properties.Get<MSFormsFont>("Font"));
        Assert.IsFalse(frame.Site!.Streamed);
        Assert.IsTrue(frame.Site.PromoteControls);
        Assert.AreEqual(162.0, frame.Site.Top, Tolerance);
    }

    [TestMethod]
    public void ExampleView_OptionButton_ReadsItsValueAndCaption()
    {
        var option = Control(Read("ExampleView.frx"), "OptionButton1");

        Assert.AreEqual("OptionButton1", option.Properties.Get<string>("Caption"));
        Assert.AreEqual("0", option.Properties.Get<string>("Value"));
        Assert.AreEqual(0, option.Properties.Get<int>("BackStyle"));
        Assert.AreEqual(0x8000000Fu, option.Properties.Get<uint>("BackColor"));
        Assert.AreEqual(1, option.Properties.Get<int>("Alignment"), "fmAlignmentRight, the default");
        Assert.AreEqual(Points(2371), option.Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual("", option.Properties.Get<string>("GroupName"));
    }

    [TestMethod]
    public void ExampleView_ComboBox_ReadsItsStyleAndDropButton()
    {
        var combo = Control(Read("ExampleView.frx"), "ComboBox1");

        Assert.AreEqual(0, combo.Properties.Get<int>("Style"), "fmStyleDropDownCombo");
        Assert.AreEqual(1, combo.Properties.Get<int>("MatchEntry"));
        Assert.AreEqual(2, combo.Properties.Get<int>("ShowDropButtonWhen"));
        Assert.AreEqual(8, combo.Properties.Get<int>("ListRows"));
        Assert.IsFalse(combo.Properties.IsStored("ListRows"));
    }

    [TestMethod]
    public void AllControls_ReadsEveryCachedClassAndNesting()
    {
        var design = Read("AllControls.storage");

        // a MultiPage's own tab strip is not a control; its pages are, with their own controls
        Assert.AreEqual(
            "Caption1 : Label #1 [1]\n" +
            "Entry : TextBox #2 [2]\n" +
            "Choices : ComboBox #3 [3]\n" +
            "Items : ListBox #4 [4]\n" +
            "Toggle1 : CheckBox #5 [5]\n" +
            "Option1 : OptionButton #6 [6]\n" +
            "Switch : ToggleButton #7 [7]\n" +
            "Go : CommandButton #8 [0]\n" +
            "Tabs : TabStrip #9 [8]\n" +
            "Scroller : ScrollBar #10 [9]\n" +
            "Spinner : SpinButton #11 [10]\n" +
            "Picture1 : Image #12 [13]\n" +
            "Outer : Frame #13 [11]\n" +
            "  OuterEntry : TextBox #14 [0]\n" +
            "  Inner : Frame #15 [1]\n" +
            "    InnerCheck : CheckBox #16 [0]\n" +
            "Pages : MultiPage #17 [12]\n" +
            "  Page1 : Page #19 [1]\n" +
            "    FirstPageEntry : TextBox #21 [0]\n" +
            "  Page2 : Page #20 [2]\n" +
            "    SecondPageGo : CommandButton #22 [0]\n",
            Tree(design.Form.Children));
    }

    [TestMethod]
    public void AllControls_Form_ReadsItsTextPropsFont_AndDefaultsItsSize()
    {
        var form = Read("AllControls.storage").Form.Properties;

        Assert.AreEqual(new MSFormsFont("MS Sans Serif", 8, false, false, false, false, 400, 0), form.Get<MSFormsFont>("Font"));
        Assert.IsFalse(form.IsStored("InsideWidth"));
        Assert.AreEqual(Points(4000), form.Get<double>("InsideWidth"), Tolerance);
        Assert.AreEqual(Points(3000), form.Get<double>("InsideHeight"), Tolerance);
        Assert.AreEqual(0, form.Get<int>("ScrollBars"));
        Assert.AreEqual(3, form.Get<int>("KeepScrollBarsVisible"));
    }

    [TestMethod]
    public void AllControls_ListAndButtons_ReadTheirMorphData()
    {
        var design = Read("AllControls.storage");

        var list = Control(design, "Items").Properties;
        Assert.AreEqual(3, list.Get<int>("ScrollBars"));
        Assert.AreEqual(0, list.Get<int>("MatchEntry"));
        Assert.AreEqual(72.0, list.Get<double>("Height"), Tolerance);
        Assert.IsFalse(list.TryGet<int>("Style", out _), "a ListBox has no Style");

        var check = Control(design, "Toggle1").Properties;
        Assert.AreEqual("0", check.Get<string>("Value"));
        Assert.AreEqual("", check.Get<string>("Caption"));
        Assert.AreEqual(108.0, check.Get<double>("Width"), Tolerance);

        var toggle = Control(design, "Switch").Properties;
        Assert.AreEqual(36.0, toggle.Get<double>("Width"), Tolerance);
        Assert.AreEqual(Points(1411), toggle.Get<double>("Height"), Tolerance);
        Assert.AreEqual(2, toggle.Get<int>("TextAlign"));
    }

    [TestMethod]
    public void AllControls_TabStrip_ReadsItsTabs()
    {
        var tabs = Control(Read("AllControls.storage"), "Tabs").Properties;

        Assert.AreEqual(0, tabs.Get<int>("Value"));
        Assert.AreEqual(144.0, tabs.Get<double>("Width"), Tolerance);
        Assert.AreEqual(108.0, tabs.Get<double>("Height"), Tolerance);
        CollectionAssert.AreEqual(
            new[] { new MSFormsTab("Tab1", "Tab1", "", "", '\0', true, true), new MSFormsTab("Tab2", "Tab2", "", "", '\0', true, true) },
            tabs.Get<IReadOnlyList<MSFormsTab>>("Tabs").ToArray());
    }

    [TestMethod]
    public void AllControls_ScrollBarSpinButtonAndImage_ReadTheirSizes()
    {
        var design = Read("AllControls.storage");

        var scroller = Control(design, "Scroller").Properties;
        Assert.AreEqual(-1, scroller.Get<int>("Orientation"));
        Assert.IsTrue(scroller.IsStored("Orientation"));
        Assert.AreEqual(32767, scroller.Get<int>("Max"));
        Assert.AreEqual(Points(2250), scroller.Get<double>("Height"), Tolerance);
        Assert.IsTrue(scroller.Get<bool>("ProportionalThumb"));

        var spinner = Control(design, "Spinner").Properties;
        Assert.AreEqual(100, spinner.Get<int>("Max"));
        Assert.AreEqual(Points(900), spinner.Get<double>("Height"), Tolerance);

        var image = Control(design, "Picture1").Properties;
        Assert.AreEqual(72.0, image.Get<double>("Width"), Tolerance);
        Assert.AreEqual(1, image.Get<int>("BorderStyle"), "an Image's default border is fmBorderStyleSingle");
        Assert.IsFalse(image.TryGet<MSFormsPicture>("Picture", out _));
    }

    [TestMethod]
    public void AllControls_Frames_ReadTheirSpecialEffectAndSize()
    {
        var design = Read("AllControls.storage");

        Assert.AreEqual(3, Control(design, "Outer").Properties.Get<int>("SpecialEffect"));
        Assert.AreEqual(216.0, Control(design, "Outer").Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual(144.0, Control(design, "Outer").Properties.Get<double>("Height"), Tolerance);
        Assert.AreEqual(3, Control(design, "Inner").Properties.Get<int>("SpecialEffect"));
    }

    [TestMethod]
    public void AllControls_MultiPage_GivesItsPagesTheirTabs_InPageIdOrder()
    {
        var design = Read("AllControls.storage");
        var pages = Control(design, "Pages");

        Assert.AreEqual(144.0, pages.Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual(108.0, pages.Properties.Get<double>("Height"), Tolerance);
        Assert.AreEqual(0, pages.Properties.Get<int>("Value"));
        Assert.IsTrue(pages.Properties.Get<bool>("Enabled"));
        Assert.AreEqual(0, pages.Properties.Get<int>("Style"));
        Assert.AreEqual("MS Sans Serif", pages.Properties.Get<MSFormsFont>("Font").Name);
        Assert.IsFalse(pages.Properties.TryGet<string>("Caption", out _), "a MultiPage has no Caption");

        var (first, second) = (pages.Children[0], pages.Children[1]);
        Assert.AreEqual("Page1", first.Properties.Get<string>("Caption"));
        Assert.AreEqual("Page2", second.Properties.Get<string>("Caption"));
        Assert.AreEqual(Points(4974), first.Properties.Get<double>("Width"), Tolerance);
        Assert.AreEqual(Points(3201), first.Properties.Get<double>("Height"), Tolerance);
        Assert.AreEqual(0, first.Properties.Get<int>("TransitionEffect"));

        // the second page's site has fVisible clear, its tab is visible: a page's Visible is its tab's
        Assert.IsTrue(second.Properties.Get<bool>("Visible"));
        Assert.IsFalse(second.Site!.Visible);
        Assert.IsTrue(first.Site!.Visible);
    }

    [TestMethod]
    public void Resources_Image_ReadsItsBitmapPicture()
    {
        var image = Control(Read("Resources.frx"), "ValidationErrorIcon");

        Assert.AreEqual(MSFormsControlKind.Image, image.Kind);
        Assert.IsFalse(image.Site!.Visible);
        Assert.AreEqual(874, image.Site.ObjectStreamSize);
        Assert.AreEqual(0x00000080u, image.Properties.Get<uint>("BorderColor"));
        Assert.AreEqual(0, image.Properties.Get<int>("BackStyle"));
        Assert.AreEqual(Points(424), image.Properties.Get<double>("Width"), Tolerance);

        // a whole bitmap file: its BITMAPFILEHEADER, a 16 × 16 24-bit BITMAPINFOHEADER and its pixels
        var picture = image.Properties.Get<MSFormsPicture>("Picture");
        Assert.AreEqual(MSFormsPictureFormat.Bitmap, picture.Format);
        Assert.AreEqual(822, picture.Data.Length);
        Assert.AreEqual(822, BitConverter.ToInt32(picture.Data.Span[2..6]));
    }

    [TestMethod]
    public void Resources_Labels_ReadTheirLongCaptions()
    {
        var design = Read("Resources.frx");

        var instructions = Control(design, "InstructionsLabel").Properties.Get<string>("Caption");
        Assert.AreEqual(248, instructions.Length);
        Assert.IsTrue(instructions.StartsWith("This isn't really a form.", StringComparison.Ordinal));
        Assert.IsTrue(Control(design, "ValidationErrorIconDescription").Properties.Get<string>("Caption").EndsWith("failed value validation.", StringComparison.Ordinal));
        Assert.AreEqual("ValidationErrorIcon", Control(design, "ValidationErrorIconName").Properties.Get<string>("Caption"));
        Assert.IsTrue(Control(design, "ValidationErrorIconName").Properties.Get<MSFormsFont>("Font").Bold);
        Assert.AreEqual(Points(6900), design.Form.Properties.Get<double>("InsideWidth"), Tolerance);
    }

    [TestMethod]
    public void FormSizeProbe_InsideSize_IsWhatExcelMeasured()
    {
        var form = Read("FormSizeProbe.frx").Form;

        Assert.AreEqual(333.0, form.Properties.Get<double>("InsideWidth"), 0.05);
        Assert.AreEqual(285.0, form.Properties.Get<double>("InsideHeight"), 0.05);
        Assert.AreEqual(0, form.Children.Count);
    }

    [TestMethod]
    public void ExampleDynamicView_HasNoControls()
    {
        var form = Read("ExampleDynamicView.frx").Form;

        Assert.AreEqual(0, form.Children.Count);
        Assert.AreEqual(Points(8954), form.Properties.Get<double>("InsideWidth"), Tolerance);
        Assert.IsFalse(form.Properties.TryGet<MSFormsFont>("Font", out _));
    }

    [TestMethod]
    public void ExploreTextboxEvents_ReadsItsTextBoxAndLabel()
    {
        var design = Read("ExploreTextboxEvents.frx");

        Assert.AreEqual("TextBox1 : TextBox #1 [0]\nLabel1 : Label #2 [1]\n", Tree(design.Form.Children));
        Assert.AreEqual(18.0, Control(design, "TextBox1").Site!.Left, Tolerance);
        Assert.AreEqual(8.1, Control(design, "TextBox1").Properties.Get<MSFormsFont>("Font").Size, Tolerance);
        Assert.AreEqual("Label1", Control(design, "Label1").Properties.Get<string>("Caption"));
        Assert.AreEqual(Points(2328), Control(design, "Label1").Site!.Top, Tolerance);
    }

    [TestMethod]
    public void Read_FrxWithItsBlobHeader_IsTheCompoundFileAfterIt()
    {
        var frx = Fixture("ExampleView.frx");

        Assert.AreEqual(Tree(MSFormsReader.Read(frx).Form.Children), Tree(MSFormsReader.Read(frx.AsSpan(24)).Form.Children));
    }
}
