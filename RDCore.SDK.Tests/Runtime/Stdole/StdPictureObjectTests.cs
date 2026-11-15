using RDCore.SDK.Runtime.Stdole;
using RDCore.SDK.Workspace.Pictures;
using static RDCore.Tests.Workspace.Pictures.TestPictures;

namespace RDCore.Tests.Runtime.Stdole;

/// <summary>
/// A <c>stdole.StdPicture</c>'s object (<see cref="StdPictureObject"/>): what <c>LoadPicture</c> makes of a file, and what <c>New StdPicture</c> and
/// <c>LoadPicture()</c> make, as MS-VBA reads them in Excel 64-bit.
/// </summary>
[TestClass]
public sealed class StdPictureObjectTests
{
    private static StdPictureObject Loaded(byte[] bytes, int iconWidth = 0, int iconHeight = 0, int handle = 7)
    {
        Assert.AreEqual(StdPictureLoadFailure.None, StdPictureObject.TryLoad(bytes, iconWidth, iconHeight, () => handle, out var picture));
        return picture!;
    }

    private static StdPictureLoadFailure Refused(byte[] bytes)
    {
        var calls = 0;
        var failure = StdPictureObject.TryLoad(bytes, 0, 0, () => ++calls, out var picture);
        Assert.IsNull(picture);
        Assert.AreEqual(0, calls);
        return failure;
    }

    [TestMethod]
    public void ABitmap_IsType1_ItsSizeInHimetric_WithTheHandleGiven_AndItsBytesAsRead()
    {
        var bytes = BitmapHeader(10, 20);

        var picture = Loaded(bytes, handle: 42);

        Assert.AreEqual((PictureFormat.Bitmap, (short)1), (picture.Format, picture.Type));
        Assert.AreEqual((PictureInfo.PixelsToHimetric(10), PictureInfo.PixelsToHimetric(20)), (picture.Width, picture.Height));
        Assert.AreEqual(42, picture.Handle);
        Assert.IsFalse(picture.IsNew);
        Assert.IsNull(picture.Image);
        Assert.IsTrue(picture.Bytes.Span.SequenceEqual(bytes));
    }

    [TestMethod]
    public void AGifAndAJpeg_AreBitmaps_Type1()
    {
        Assert.AreEqual((PictureFormat.Gif, (short)1), (Loaded(Gif1x1).Format, Loaded(Gif1x1).Type));
        Assert.AreEqual((PictureFormat.Jpeg, (short)1), (Loaded(JpegHeader(4, 3)).Format, Loaded(JpegHeader(4, 3)).Type));
    }

    [TestMethod]
    public void AMetafileIsType2_AnIconAndACursorType3_AnEnhancedMetafileType4()
    {
        var enhanced = new byte[88];
        enhanced[0] = 1;
        " EMF"u8.CopyTo(enhanced.AsSpan(40));

        Assert.AreEqual(2, Loaded(Hex("D7CDC69A 0000 0000 0000 A005 D002 A005 00000000 0000")).Type);
        Assert.AreEqual(3, Loaded(Icon(1, PngHeader(32, 32))).Type);
        Assert.AreEqual(3, Loaded(Icon(2, PngHeader(32, 32))).Type);
        Assert.AreEqual(4, Loaded(enhanced).Type);
    }

    [TestMethod]
    public void APlaceableMetafile_IsItsBoundingBox()
    {
        // 1440 units per inch, 1440 by 720 units: 2540 by 1270 HIMETRIC, as Excel reads its placeable WMF.
        var picture = Loaded(Hex("D7CDC69A 0000 0000 0000 A005 D002 A005 00000000 0000"));

        Assert.AreEqual((2540, 1270), (picture.Width, picture.Height));
    }

    [TestMethod]
    public void AnIcon_IsItsImageOfTheSizeAskedFor_ElseItsFirst_And32By32WhenNoneIsAsked()
    {
        var icon = Icon(1, PngHeader(16, 16), PngHeader(48, 48), PngHeader(32, 32));

        Assert.AreEqual((16, 16), (Loaded(icon, 16, 16).Image!.Value.Width, Loaded(icon, 16, 16).Image!.Value.Height));
        Assert.AreEqual(32, Loaded(icon).Image!.Value.Width);
        Assert.AreEqual(16, Loaded(icon, 20, 20).Image!.Value.Width);
        Assert.AreEqual(PictureInfo.PixelsToHimetric(48), Loaded(icon, 48, 48).Width);
    }

    [TestMethod]
    public void AnIconWithTwoImagesOfTheSize_IsTheOneOfMoreBits()
    {
        var dib = Bitmap2x2[14..];
        var icon = Icon(1, dib, PngHeader(2, 1));

        Assert.AreEqual(32, Loaded(icon, 2, 1).Image!.Value.BitCount);
    }

    [TestMethod]
    public void NoBytes_AnUnknownFormat_AndAPng_AreInvalidPictures_481()
    {
        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused([]));
        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused("not a picture"u8.ToArray()));
        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused(PngHeader(4, 4)));
        Assert.AreEqual(481, (int)StdPictureLoadFailure.InvalidPicture);
    }

    [TestMethod]
    public void ABitmapCutShort_IsEUnexpected()
    {
        Assert.AreEqual(StdPictureLoadFailure.BitmapCutShort, Refused(Bitmap2x2[..20]));
        Assert.AreEqual(-2147418113, (int)StdPictureLoadFailure.BitmapCutShort);
    }

    [TestMethod]
    public void AMetafileWithNoPlaceableHeader_IsEFail()
    {
        Assert.AreEqual(StdPictureLoadFailure.MetafileStatesNoSize, Refused(Hex("0100 0900 0003 00000000 0000 00000000 0000")));
        Assert.AreEqual(-2147467259, (int)StdPictureLoadFailure.MetafileStatesNoSize);
    }

    [TestMethod]
    public void NewAndEmpty_HoldNoPicture_AndOnlyNewIsNew()
    {
        var created = StdPictureObject.CreateNew();
        var empty = StdPictureObject.CreateEmpty();

        foreach (var picture in new[] { created, empty })
        {
            Assert.IsNull(picture.Format);
            Assert.AreEqual((0, 0, 0, (short)0), (picture.Handle, picture.Width, picture.Height, picture.Type));
            Assert.IsTrue(picture.Bytes.IsEmpty);
            Assert.IsFalse(picture.HasPalette);
        }

        Assert.IsTrue(created.IsNew);
        Assert.IsFalse(empty.IsNew);
        Assert.AreNotSame(StdPictureObject.CreateNew(), StdPictureObject.CreateNew());
        Assert.AreNotSame(StdPictureObject.CreateEmpty(), StdPictureObject.CreateEmpty());
    }

    [TestMethod]
    public void ABitmapsPalette_Is0_AndKeepsWhatIsAssigned()
    {
        foreach (var bytes in new[] { BitmapHeader(2, 2), Gif1x1, JpegHeader(2, 2) })
        {
            var picture = Loaded(bytes);
            Assert.IsTrue(picture.HasPalette);
            Assert.AreEqual(0, picture.Palette);
            picture.Palette = 5;
            Assert.AreEqual(5, picture.Palette);
        }
    }

    [TestMethod]
    public void AnIconAMetafileAndNoPicture_HaveNoPalette_ToReadOrAssign()
    {
        foreach (var picture in new[] { Loaded(Icon(1, PngHeader(32, 32))), Loaded(Hex("D7CDC69A 0000 0000 0000 A005 D002 A005 00000000 0000")), StdPictureObject.CreateNew() })
        {
            Assert.IsFalse(picture.HasPalette);
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = picture.Palette);
            Assert.ThrowsExactly<InvalidOperationException>(() => picture.Palette = 5);
        }
    }

    [TestMethod]
    public void TheConstructor_RefusesHandle0_ANegativeSize_AndAnImageForAPictureNotAnIcon()
    {
        var image = new IconImage(32, 32, 32, 0, 0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new StdPictureObject(PictureFormat.Bitmap, Bitmap2x2, 10, 10, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new StdPictureObject(PictureFormat.Bitmap, Bitmap2x2, -1, 10, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new StdPictureObject(PictureFormat.Bitmap, Bitmap2x2, 10, -1, 1));
        Assert.ThrowsExactly<ArgumentException>(() => new StdPictureObject(PictureFormat.Bitmap, Bitmap2x2, 10, 10, 1, image));
        Assert.AreEqual(image, new StdPictureObject(PictureFormat.Icon, Bitmap2x2, 10, 10, 1, image).Image);
    }

    [TestMethod]
    public void TryLoad_RefusesNoHandleGiver_AndAHandleOf0()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => StdPictureObject.TryLoad(Bitmap2x2, 0, 0, null!, out _));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => StdPictureObject.TryLoad(Bitmap2x2, 0, 0, () => 0, out _));
    }
}
