using RDCore.SDK.Runtime.Stdole;

namespace RDCore.Tests.Runtime.Stdole;

/// <summary>
/// A <c>stdole.StdFont</c>'s object (<see cref="StdFontObject"/>): its defaults, size snapping, limits and change notification, as VB6 and
/// MS-VBA (Excel 64-bit) read a StdFont that no control holds.
/// </summary>
[TestClass]
public sealed class StdFontObjectTests
{
    private static StdFontObject Font(string? name = null, decimal? size = null)
    {
        var font = new StdFontObject();
        if (name is not null)
        {
            Assert.AreEqual(StdFontFailure.None, font.TrySetName(name));
        }

        if (size is { } points)
        {
            Assert.AreEqual(StdFontFailure.None, font.TrySetSize(points));
        }

        return font;
    }

    private static List<string> Watched(StdFontObject font)
    {
        var names = new List<string>();
        font.Changed += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    [TestMethod]
    public void ANewFont_IsMSSansSerif825_Weight400_Charset0_WithNoStyle()
    {
        var font = new StdFontObject();

        Assert.AreEqual("MS Sans Serif", font.Name);
        Assert.AreEqual(8.25m, font.Size);
        Assert.AreEqual((short)400, font.Weight);
        Assert.IsFalse(font.Bold);
        Assert.AreEqual((short)0, font.Charset);
        Assert.IsFalse(font.Italic || font.Underline || font.Strikethrough);
    }

    [TestMethod]
    [DataRow("Arial", 14.0, 14.25)]
    [DataRow("Arial", 0.0001, 1.5)]
    [DataRow("Arial", 1.5, 1.5)]
    [DataRow("Arial", 2.0, 2.25)]
    [DataRow("Arial", 4.0, 3.75)]
    [DataRow("Arial", 7.0, 6.75)]
    [DataRow("Arial", 2161.0, 2160.75)]
    [DataRow("Arial", 3000.0, 3000.0)]
    [DataRow("Times New Roman", 5.0, 5.25)]
    [DataRow("MS Sans Serif", 14.0, 13.5)]
    [DataRow("MS Sans Serif", 0.0001, 8.25)]
    [DataRow("MS Sans Serif", 8.0, 8.25)]
    [DataRow("Courier", 1.0, 9.75)]
    [DataRow("Small Fonts", 0.5, 2.25)]
    [DataRow("Small Fonts", 4.0, 3.75)]
    public void ASize_ReadsBackAsDrawn(string name, double size, double drawn)
    {
        var font = Font(name, (decimal)size);

        Assert.AreEqual((decimal)drawn, font.Size);
    }

    [TestMethod]
    public void ABitmapFaceName_IsMatchedIgnoringCase()
    {
        Assert.AreEqual(13.5m, StdFontObject.DrawnSize("ms sans serif", 14));
    }

    [TestMethod]
    public void ASizeNotReadYet_SnapsToALaterName()
    {
        var font = Font("Courier", 14);
        Assert.AreEqual(StdFontFailure.None, font.TrySetName("Arial"));

        Assert.AreEqual(14.25m, font.Size);
    }

    [TestMethod]
    public void ASizeRead_IsTheOneALaterNameSnapsFrom()
    {
        var font = Font("Courier", 14);
        Assert.AreEqual(12m, font.Size);
        Assert.AreEqual(StdFontFailure.None, font.TrySetName("Arial"));

        Assert.AreEqual(12m, font.Size);
    }

    [TestMethod]
    public void TheSizeAsDrawn_IsTheSizeRead_AndALaterName_SnapsFromTheSizeSet()
    {
        var font = Font("Courier", 14);
        var changes = Watched(font);
        Assert.AreEqual(12m, font.SizeAsDrawn);
        Assert.AreEqual(StdFontFailure.None, font.TrySetName("Arial"));

        Assert.AreEqual(14.25m, font.SizeAsDrawn);
        Assert.AreEqual(14.25m, font.Size);
        Assert.AreEqual(14.25m, font.SizeAsDrawn);
        CollectionAssert.AreEqual(new[] { "Name" }, changes);
    }

    [TestMethod]
    public void ASizeRead_SnapsAgain_ToALaterBitmapFace()
    {
        var font = Font(size: 14);
        Assert.AreEqual(14.25m, font.Size);
        Assert.AreEqual(StdFontFailure.None, font.TrySetName("MS Sans Serif"));

        Assert.AreEqual(13.5m, font.Size);
    }

    [TestMethod]
    [DataRow(0.0001, 1.5)]
    [DataRow(1.0, 1.5)]
    [DataRow(1.25, 2.25)]
    [DataRow(1.5, 2.25)]
    [DataRow(2.5, 2.25)]
    [DataRow(3.0, 3.0)]
    [DataRow(8.0, 8.25)]
    [DataRow(10.0, 9.75)]
    [DataRow(14.0, 14.25)]
    [DataRow(24.0, 24.0)]
    public void ASize_OfAFontNeverNamed_SnapsAsAScalableFace_AndTwoPixelsAsThree(double size, double drawn)
    {
        var font = Font(size: (decimal)size);

        Assert.AreEqual((decimal)drawn, font.Size);
        Assert.AreEqual("MS Sans Serif", font.Name);
    }

    // The sizes of no name are those a font never named reads back.
    [TestMethod]
    [DataRow(0.0001, 1.5)]
    [DataRow(1.5, 2.25)]
    [DataRow(8.0, 8.25)]
    [DataRow(14.0, 14.25)]
    [DataRow(30000.0, 24575.25)]
    public void TheDrawnSize_OfNoName_IsAScalableFacesWithTwoPixelsAsThree(double size, double drawn)
    {
        Assert.AreEqual((decimal)drawn, StdFontObject.DrawnSize(null, (decimal)size));
    }

    [TestMethod]
    public void TheDrawnSize_OfNoSize_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => StdFontObject.DrawnSize(null, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => StdFontObject.DrawnSize("Arial", -1));
    }

    [TestMethod]
    public void AFont_IsNamed_OnceANameIsSet_ItsOwnIncluded_AndAClone_IsAsNamed()
    {
        var font = new StdFontObject();
        font.Bold = true;
        font.TrySetCharset(2);
        font.TrySetSize(14);
        var unnamed = font.Clone();
        Assert.IsFalse(font.IsNamed);

        Assert.AreEqual(StdFontFailure.None, font.TrySetName(font.Name));

        Assert.IsTrue(font.IsNamed);
        Assert.IsTrue(font.Clone().IsNamed);
        Assert.IsFalse(unnamed.IsNamed);
    }

    [TestMethod]
    public void NamingAFont_ItsOwnName_GivesItTheBitmapSizes()
    {
        var font = new StdFontObject();
        var names = Watched(font);
        Assert.AreEqual(StdFontFailure.None, font.TrySetName(font.Name));
        font.TrySetSize(14);

        Assert.AreEqual(13.5m, font.Size);
        CollectionAssert.AreEqual(new[] { "Size" }, names);
    }

    [TestMethod]
    public void BoldAndACharset_LeaveAFontUnnamed()
    {
        var font = Font(size: 14);
        font.Bold = true;
        font.TrySetCharset(0);

        Assert.AreEqual(14.25m, font.Size);
    }

    [TestMethod]
    public void ASizeNotRead_ThroughBoldAndAName_SnapsToTheLastName()
    {
        var font = Font("Arial", 13);
        font.Bold = true;
        Assert.AreEqual(StdFontFailure.None, font.TrySetName("MS Sans Serif"));

        Assert.AreEqual(12m, font.Size);
    }

    [TestMethod]
    public void ASize_RaisesSizeWhenSet_AndAReadRaisesNothing()
    {
        var font = Font("Arial");
        var names = Watched(font);

        font.TrySetSize(12);
        font.TrySetSize(12.1m);
        _ = font.Size;
        var afterRead = names.Count;
        font.TrySetSize(12.1m);

        CollectionAssert.AreEqual(new[] { "Size", "Size", "Size" }, names);
        Assert.AreEqual(2, afterRead);
    }

    [TestMethod]
    public void TheSameSize_NotReadYet_RaisesNothing()
    {
        var font = Font("Arial", 12.1m);
        var names = Watched(font);

        font.TrySetSize(12.1m);

        Assert.IsEmpty(names);
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    public void ASizeOfZeroOrLess_IsAnInvalidPropertyValue_AndChangesNothing(double size)
    {
        var font = Font("Arial", 10);
        var names = Watched(font);

        Assert.AreEqual(StdFontFailure.InvalidPropertyValue, font.TrySetSize((decimal)size));
        Assert.AreEqual(9.75m, font.Size);
        Assert.IsEmpty(names);
    }

    [TestMethod]
    [DataRow(24576.0)]
    [DataRow(32767.0)]
    [DataRow(100000.0)]
    public void ASizeOf24576OrMore_IsAnOverflow_AndChangesNothing(double size)
    {
        var font = Font("Arial", 10);

        Assert.AreEqual(StdFontFailure.Overflow, font.TrySetSize((decimal)size));
        Assert.AreEqual(9.75m, font.Size);
    }

    [TestMethod]
    [DataRow(null, 24575.0, 24575.25)]
    [DataRow("Arial", 24575.5, 24575.25)]
    [DataRow("MS Sans Serif", 24575.75, 24575.25)]
    [DataRow("Arial", 16384.0, 16383.75)]
    public void TheLargestSizes_AreDrawnAtMost32767PixelsHigh(string? name, double size, double drawn)
    {
        Assert.AreEqual((decimal)drawn, Font(name, (decimal)size).Size);
    }

    [TestMethod]
    [DataRow((short)0, (short)400)]
    [DataRow((short)550, (short)400)]
    [DataRow((short)551, (short)700)]
    [DataRow((short)1000, (short)700)]
    public void AWeight_IsStoredAsNormalOrBold(short weight, short stored)
    {
        var font = new StdFontObject();

        Assert.AreEqual(StdFontFailure.None, font.TrySetWeight(weight));
        Assert.AreEqual(stored, font.Weight);
        Assert.AreEqual(stored == 700, font.Bold);
    }

    [TestMethod]
    [DataRow((short)-1)]
    [DataRow((short)1001)]
    public void AWeightOutOf0To1000_IsAnInvalidPropertyValue_AndChangesNothing(short weight)
    {
        var font = new StdFontObject { Bold = true };

        Assert.AreEqual(StdFontFailure.InvalidPropertyValue, font.TrySetWeight(weight));
        Assert.AreEqual((short)700, font.Weight);
    }

    [TestMethod]
    public void Bold_SetsTheWeight_AndRaisesBold_OnlyWhenItChanges()
    {
        var font = new StdFontObject();
        var names = Watched(font);

        font.Bold = true;
        font.Bold = true;
        Assert.AreEqual((short)700, font.Weight);
        font.Bold = false;

        Assert.AreEqual((short)400, font.Weight);
        CollectionAssert.AreEqual(new[] { "Bold", "Bold" }, names);
    }

    [TestMethod]
    public void AWeightThatStoresTheSameValue_RaisesNothing()
    {
        var font = new StdFontObject { Bold = true };
        var names = Watched(font);

        font.TrySetWeight(700);
        font.TrySetWeight(600);

        Assert.IsEmpty(names);
    }

    [TestMethod]
    public void ANameAndACharset_ReadBackAsSet()
    {
        var font = new StdFontObject();

        Assert.AreEqual(StdFontFailure.None, font.TrySetName("no such face"));
        Assert.AreEqual(StdFontFailure.None, font.TrySetCharset(1));

        Assert.AreEqual("no such face", font.Name);
        Assert.AreEqual((short)1, font.Charset);
    }

    [TestMethod]
    public void ANameOf31Characters_IsSet()
    {
        var font = new StdFontObject();

        Assert.AreEqual(StdFontFailure.None, font.TrySetName(new string('a', 31)));
        Assert.AreEqual(31, font.Name.Length);
    }

    [TestMethod]
    [DataRow(32)]
    [DataRow(40)]
    public void ANameOfMoreThan31Characters_IsAnInvalidPropertyValue_AndChangesNothing(int length)
    {
        var font = Font(size: 14);
        var names = Watched(font);

        Assert.AreEqual(StdFontFailure.InvalidPropertyValue, font.TrySetName(new string('a', length)));
        Assert.AreEqual("MS Sans Serif", font.Name);
        Assert.AreEqual(14.25m, font.Size);
        Assert.IsEmpty(names);
    }

    [TestMethod]
    [DataRow((short)-1)]
    [DataRow((short)256)]
    public void ACharsetOutOf0To255_IsAnInvalidPropertyValue(short charset)
    {
        var font = new StdFontObject();

        Assert.AreEqual(StdFontFailure.InvalidPropertyValue, font.TrySetCharset(charset));
        Assert.AreEqual((short)0, font.Charset);
    }

    [TestMethod]
    public void EachMember_RaisesItsName_WhenItsValueChanges_AndNothingWhenItDoesNot()
    {
        var font = new StdFontObject();
        var names = Watched(font);

        font.TrySetName("Arial");
        font.TrySetName("Arial");
        font.TrySetSize(10);
        font.TrySetWeight(700);
        font.Italic = true;
        font.Italic = true;
        font.Underline = true;
        font.Strikethrough = true;
        font.TrySetCharset(2);
        font.TrySetCharset(2);

        CollectionAssert.AreEqual(new[] { "Name", "Size", "Weight", "Italic", "Underline", "Strikethrough", "Charset" }, names);
    }

    [TestMethod]
    public void EveryHandler_IsRaised()
    {
        var font = new StdFontObject();
        var first = Watched(font);
        var second = Watched(font);

        font.Italic = true;

        CollectionAssert.AreEqual(new[] { "Italic" }, first);
        CollectionAssert.AreEqual(new[] { "Italic" }, second);
    }

    [TestMethod]
    public void AClone_HasTheValues_AndNeitherChangesTheOther()
    {
        var font = Font("Arial", 14);
        font.Italic = true;
        var names = Watched(font);

        var clone = font.Clone();
        clone.Bold = true;
        font.TrySetName("Courier");

        Assert.AreEqual("Arial", clone.Name);
        Assert.AreEqual(14.25m, clone.Size);
        Assert.IsTrue(clone.Italic);
        Assert.IsFalse(font.Bold);
        CollectionAssert.AreEqual(new[] { "Name" }, names);
    }
}
