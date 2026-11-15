using RDCore.SDK.Runtime.Stdole;
using RDCore.SDK.Workspace.Pictures;
using System.Buffers.Binary;
using static RDCore.Tests.Workspace.Pictures.TestPictures;

namespace RDCore.Tests.Runtime.Stdole;

/// <summary>
/// The file <c>SavePicture</c> writes for a <c>stdole.StdPicture</c> (<see cref="SavedPicture"/>), in the layouts MS-VBA writes (Excel 64-bit).
/// </summary>
[TestClass]
public sealed class SavedPictureTests
{
    private const string NotMeasured = "MS-VBA's bytes for this picture are not measured; the expected value is the provisional one";

    private static StdPictureObject Loaded(byte[] bytes)
    {
        Assert.AreEqual(StdPictureLoadFailure.None, StdPictureObject.TryLoad(bytes, 0, 0, () => 1, out var picture));
        return picture!;
    }

    private static byte[] Saved(byte[] bytes) => SavedPicture.BytesOf(Loaded(bytes)) ?? throw new AssertFailedException("nothing saved");

    private static string Head(byte[] file) => $"len {file.Length} {Convert.ToHexString(file[..16])}";

    private static DecodedPicture Decoded(byte[] bytes)
    {
        Assert.AreEqual(PictureDecodeFailure.None, PictureDecoder.TryDecode(bytes, out var picture));
        return picture!;
    }

    private static string Numbers(byte[] bytes) => string.Join(" ", Decoded(bytes).Indexes!.Value.ToArray());

    [TestMethod]
    public void ANewPicture_IsSavedAsNoBytes_AndAnEmptyOne_IsNotSaved()
    {
        Assert.AreEqual(0, SavedPicture.BytesOf(StdPictureObject.CreateNew())!.Length);
        Assert.IsNull(SavedPicture.BytesOf(StdPictureObject.CreateEmpty()));
        Assert.ThrowsExactly<ArgumentNullException>(() => SavedPicture.BytesOf(null!));
    }

    [TestMethod]
    public void AMetafile_IsSavedAsItWasRead()
    {
        var metafile = Hex("D7CDC69A 0000 0000 0000 A005 D002 A005 00000000 0000");

        CollectionAssert.AreEqual(metafile, Saved(metafile));
    }

    // its table of the 2 colors it uses, no resolution, and the byte that pads each row cleared.
    [TestMethod]
    public void An8BitBitmap_KeepsItsColorTable_UnderAHeaderOfNoResolution_AndItsPaddingCleared()
    {
        var bitmap = Bitmap(3, 2, 8, 0, [0x112233, 0x445566], [1, 0, 1, 0xEE, 0, 1, 0, 0xEE], used: 2);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(38), 2835);
        BinaryPrimitives.WriteInt32LittleEndian(bitmap.AsSpan(42), 2835);

        var saved = Saved(bitmap);

        Assert.AreEqual("424D46000000000000003E000000", Convert.ToHexString(saved[..14]));
        Assert.AreEqual("28000000030000000200000001000800000000000800000000000000000000000200000000000000",
            Convert.ToHexString(saved[14..54]));
        Assert.AreEqual("33221100665544000100010000010000", Convert.ToHexString(saved[54..]));
    }

    [TestMethod]
    public void A32BitBitmap_IsSavedIn24Bits_AndA1BitRow_HasTheBitsPastItsLastPixelCleared()
    {
        var wide = Saved(Bitmap(2, 1, 32, 0, [], [1, 2, 3, 4, 5, 6, 7, 8]));
        var narrow = Saved(Bitmap(3, 1, 1, 0, [0x000000, 0xFFFFFF], [0xFF, 0xFF, 0xFF, 0xFF]));

        Assert.AreEqual(24, BinaryPrimitives.ReadUInt16LittleEndian(wide.AsSpan(28)));
        Assert.AreEqual("0102030506070000", Convert.ToHexString(wide[54..]));
        Assert.AreEqual("E0000000", Convert.ToHexString(narrow[^4..]));
    }

    // The length and the first 16 bytes MS-VBA's SavePicture writes for pictures of these sizes: a GIF of 7 by 5 as an 8-bit bitmap with a table of 256
    // colors, and an icon's or a cursor's 32 by 32 image as an icon of 16 colors. The PNG, 6 by 4, as a 24-bit bitmap, is RD-VBA's own.
    [TestMethod]
    public void AGif_APng_AnIcon_AndACursor_AreSavedInMSVBAsLayout()
    {
        var gif = (byte[])Gif1x1.Clone();
        BinaryPrimitives.WriteInt16LittleEndian(gif.AsSpan(6), 7);
        BinaryPrimitives.WriteInt16LittleEndian(gif.AsSpan(8), 5);
        var dib = IconDib(32, 32, 32, [], new byte[32 * 32 * 4], new byte[32 * 4]);

        Assert.AreEqual("len 1118 424D5E04000000000000360400002800", Head(Saved(gif)));
        Assert.AreEqual("len 134 424D8600000000000000360000002800", Head(Saved(PngHeader(6, 4))));
        Assert.AreEqual("len 766 0000010001002020100000000000E802", Head(Saved(Icon(1, dib))));
        Assert.AreEqual("len 766 0000010001002020100000000000E802", Head(Saved(Icon(2, dib))));
    }

    // the image LoadPicture chose: of the size asked for, here the 16 by 16 one, whose pixels are white.
    [TestMethod]
    public void AnIcon_IsSavedFromTheImageThePictureHolds()
    {
        var small = IconDib(16, 16, 24, [], Enumerable.Repeat((byte)0xFF, 16 * 16 * 3).ToArray(), new byte[16 * 4]);
        var large = IconDib(32, 32, 24, [], new byte[32 * 32 * 3], new byte[32 * 4]);
        Assert.AreEqual(StdPictureLoadFailure.None, StdPictureObject.TryLoad(Icon(1, large, small), 16, 16, () => 1, out var picture));

        var saved = SavedPicture.BytesOf(picture!)!;

        Assert.AreEqual(16, saved[6]);
        Assert.IsTrue(Decoded(saved).Indexes!.Value.ToArray().All(number => number == 15));
    }

    [TestMethod]
    public void ACompressedBitmap_IsSavedUncompressed_AsItsDecodedPixels()
    {
        var rle = Bitmap(4, 1, 8, 1, [0x000000, 0xFF0000], [3, 1, 1, 0, 0, 1], used: 2);
        var fields = Bitmap(2, 1, 16, 3, [0xF800, 0x07E0, 0x001F], [0x00, 0xF8, 0xE0, 0x07]);

        var savedRle = Saved(rle);
        var savedFields = Saved(fields);

        Assert.AreEqual(0, BinaryPrimitives.ReadInt32LittleEndian(savedRle.AsSpan(30)));
        Assert.AreEqual("1 1 1 0", Numbers(savedRle));
        Assert.AreEqual(24, BinaryPrimitives.ReadUInt16LittleEndian(savedFields.AsSpan(28)));
        CollectionAssert.AreEqual(Decoded(fields).Bgra.ToArray(), Decoded(savedFields).Bgra.ToArray());
    }

    [TestMethod]
    public void AJpeg_IsSavedAsA24BitBitmap()
        => Assert.AreEqual("len 362 424D6A01000000000000360000002800", Head(Saved(JpegHeader(9, 11))));

    // a frame of 2 by 2 at 1,1 on a screen of 4 by 3, background 0, its color 1 transparent: what number fills outside the frame and the transparent pixel.
    [TestMethod]
    [Ignore(NotMeasured)]
    public void AGifsScreenOutsideItsFrame_IsSavedAsNumber0_AndItsTransparentPixel_AsItsOwnNumber()
        => Assert.AreEqual("0 0 0 0 0 2 1 0 0 3 2 0",Numbers(Saved(Gif(4, 3, [0x000000, 0x404040, 0x808080, 0xFFFFFF], [2, 1, 3, 2], 2, 2, 1, 1, 1))));

    // a 32-bit PNG image of an icon: a gray between two of the 16 colors, and a pixel of alpha 0, masked.
    [TestMethod]
    [Ignore(NotMeasured)]
    public void AnIconsColors_AreTheNearestOf16_AndItsPngImagesClearPixels_AreMasked()
    {
        var saved = Saved(Icon(1, Png(2, 1, 6, 8, [0, 0x60, 0x60, 0x60, 255, 0, 0, 0, 0])));

        Assert.AreEqual("8 0", Numbers(saved));
        Assert.AreEqual("0 1", string.Join(" ", Decoded(saved).Mask!.Value.ToArray()));
    }

    // RLE8 of 4 by 1 with a table of 2 colors: uncompressed at 8 bits, its table padded to 256 colors with black.
    [TestMethod]
    [Ignore(NotMeasured)]
    public void ACompressedBitmap_IsSavedAt8Bits_WithATableOf256Colors()
        => Assert.AreEqual("len 1082 424D3A04000000000000360400002800", Head(Saved(Bitmap(4, 1, 8, 1, [0x000000, 0xFF0000], [4, 1, 0, 1], used: 2))));

    // a GIF of 4 by 3 whose LZW data is cut after its first codes: the pixels never decoded.
    [TestMethod]
    [Ignore(NotMeasured)]
    public void AGifCutShort_IsSavedWithThePixelsItLacksAsNumber0()
    {
        var gif = Gif(4, 3, [0x000000, 0xFFFFFF], [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1], 4, 3);
        var cut = gif[..^6];

        StringAssert.EndsWith(Numbers(Saved(cut)), " 0");
    }
}
