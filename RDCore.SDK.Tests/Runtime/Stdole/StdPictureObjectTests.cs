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

    // an icon's DIB image of 32 bits, black and its mask clear.
    private static byte[] Dib(int side) => IconDib(side, side, 32, [], new byte[side * side * 4], new byte[(side + 31) / 32 * 4 * side]);

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
        Assert.AreEqual(3, Loaded(Icon(1, Dib(32))).Type);
        Assert.AreEqual(3, Loaded(Icon(2, Dib(32))).Type);
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
        var icon = Icon(1, Dib(16), Dib(48), Dib(32));

        Assert.AreEqual((16, 16), (Loaded(icon, 16, 16).Image!.Value.Width, Loaded(icon, 16, 16).Image!.Value.Height));
        Assert.AreEqual(32, Loaded(icon).Image!.Value.Width);
        Assert.AreEqual(16, Loaded(icon, 20, 20).Image!.Value.Width);
        Assert.AreEqual(PictureInfo.PixelsToHimetric(48), Loaded(icon, 48, 48).Width);
    }

    [TestMethod]
    public void AnIconWithTwoImagesOfTheSize_IsTheOneOfMoreBits()
    {
        var dib = Bitmap2x2[14..];
        var icon = Icon(1, dib, IconDib(2, 1, 32, [], new byte[8], new byte[4]));

        Assert.AreEqual(32, Loaded(icon, 2, 1).Image!.Value.BitCount);
    }

    [TestMethod]
    public void NoBytes_AnUnknownFormat_AndAMalformedPng_AreInvalidPictures_481()
    {
        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused([]));
        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused("not a picture"u8.ToArray()));
        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused(PngHeader(4, 4)[..20]));
        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused(PngHeader(0, 4)));
        Assert.AreEqual(481, (int)StdPictureLoadFailure.InvalidPicture);
    }

    // an RD-VBA addition: MS-VBA raises 481 for a PNG.
    [TestMethod]
    public void APng_IsABitmap_Type1_ItsSizeInHimetric_AndItsBytesAsRead()
    {
        var bytes = PngHeader(6, 4);

        var picture = Loaded(bytes, handle: 9);

        Assert.AreEqual((PictureFormat.Png, (short)1), (picture.Format, picture.Type));
        Assert.AreEqual((PictureInfo.PixelsToHimetric(6), PictureInfo.PixelsToHimetric(4)), (picture.Width, picture.Height));
        Assert.AreEqual(9, picture.Handle);
        Assert.IsNull(picture.Image);
        Assert.IsTrue(picture.Bytes.Span.SequenceEqual(bytes));
    }

    // Visual Basic 6.0's LoadPicture Size: vbLPSmall 0, vbLPLarge 1, vbLPSmallShell 2, vbLPLargeShell 3, vbLPCustom 4 (X by Y); X and Y read for 4 only.
    [TestMethod]
    [DataRow(null, 0, 0, DisplayName = "no size")]
    [DataRow(0, 16, 16, DisplayName = "vbLPSmall")]
    [DataRow(1, 32, 32, DisplayName = "vbLPLarge")]
    [DataRow(2, 16, 16, DisplayName = "vbLPSmallShell")]
    [DataRow(3, 32, 32, DisplayName = "vbLPLargeShell")]
    [DataRow(4, 48, 24, DisplayName = "vbLPCustom")]
    [DataRow(7, 32, 32, DisplayName = "another value")]
    public void TheIconSizeOfVisualBasicsSize_IsTheSystemsOrXByY(int? size, int width, int height)
    {
        Assert.AreEqual((width, height), StdPictureObject.IconSizeOf(size, 48, 24));
    }

    // the image vbLPSmall chooses of an icon of 32 by 32 and 16 by 16 images: the 16 by 16 one.
    [TestMethod]
    public void VisualBasicsSmallSize_LoadsAnIconsSmallImage()
    {
        var (width, height) = StdPictureObject.IconSizeOf(0, 0, 0);
        var image = Loaded(Icon(1, Dib(32), Dib(16)), width, height).Image!.Value;

        Assert.AreEqual((16, 16), (image.Width, image.Height));
    }

    // an RD-VBA addition: MS-VBA raises 481 for an icon or a cursor whose image to load is a PNG.
    [TestMethod]
    [DataRow((short)1, PictureFormat.Icon, DisplayName = "an icon")]
    [DataRow((short)2, PictureFormat.Cursor, DisplayName = "a cursor")]
    public void AnIconOrACursor_WhoseImageIsAPng_IsType3_ThatImage(short type, PictureFormat format)
    {
        var png = PngHeader(24, 16);
        var bytes = Icon(type, Dib(16), png);

        var picture = Loaded(bytes, 24, 16);

        Assert.AreEqual((format, (short)3), (picture.Format, picture.Type));
        Assert.AreEqual((24, 16, 32), (picture.Image!.Value.Width, picture.Image.Value.Height, picture.Image.Value.BitCount));
        Assert.AreEqual((PictureInfo.PixelsToHimetric(24), PictureInfo.PixelsToHimetric(16)), (picture.Width, picture.Height));
        Assert.IsTrue(picture.Bytes.Span.Slice(picture.Image.Value.Offset, picture.Image.Value.Length).SequenceEqual(png));
    }

    [TestMethod]
    public void ABitmapsHeaderCutShort_IsEUnexpected()
    {
        Assert.AreEqual(StdPictureLoadFailure.CutShort, Refused(Bitmap2x2[..20]));
        Assert.AreEqual(-2147418113, (int)StdPictureLoadFailure.CutShort);
    }

    // an 8-bit bitmap of 4 by 4 cut after its first row, and the 24-bit one short of its last bytes.
    [TestMethod]
    public void ABitmapWhoseRowsAreCutShort_IsAnInvalidPicture_481()
    {
        var bitmap = Bitmap(4, 4, 8, 0, [0x000000, 0xFF0000, 0x00FF00, 0x0000FF], [1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2, 3, 1, 2, 3, 1], used: 4);

        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused(bitmap[..(14 + 40 + 16 + 4)]));
        Assert.AreEqual(StdPictureLoadFailure.InvalidPicture, Refused(Bitmap2x2[..^4]));
        Assert.AreEqual(StdPictureLoadFailure.None, StdPictureObject.TryLoad(bitmap, 0, 0, () => 1, out _));
    }

    // its segments cut in a table, its bytes ending after its frame, and its scan's header cut.
    [TestMethod]
    public void AJpegWhoseBytesEndBeforeItsScan_IsEUnexpected()
    {
        var jpeg = JpegHeader(4, 3);
        var frame = jpeg[..^16];

        Assert.AreEqual(StdPictureLoadFailure.CutShort, Refused([.. frame, .. Hex("FFC4 00B5 0001 0203")]));
        Assert.AreEqual(StdPictureLoadFailure.CutShort, Refused(frame));
        Assert.AreEqual(StdPictureLoadFailure.CutShort, Refused(jpeg[..^10]));
        Assert.AreEqual(StdPictureLoadFailure.None, StdPictureObject.TryLoad(jpeg, 0, 0, () => 1, out _));
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
        foreach (var bytes in new[] { BitmapHeader(2, 2), Gif1x1, JpegHeader(2, 2), PngHeader(2, 2) })
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
        foreach (var picture in new[] { Loaded(Icon(1, Dib(32))), Loaded(Hex("D7CDC69A 0000 0000 0000 A005 D002 A005 00000000 0000")), StdPictureObject.CreateNew() })
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
