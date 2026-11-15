using RDCore.SDK.Runtime.Stdole;
using RDCore.SDK.Workspace.Pictures;
using System.Buffers.Binary;
using static RDCore.Tests.Workspace.Pictures.TestPictures;

namespace RDCore.Tests.Runtime.Stdole;

/// <summary>
/// The file <c>SavePicture</c> writes for a <c>stdole.StdPicture</c> (<see cref="SavedPicture"/>), in the layouts MS-VBA writes (Excel 64-bit).
/// </summary>
/// <remarks>
/// The expected bytes of the GIFs, the compressed bitmaps and the icons are those MS-VBA's <c>SavePicture</c> writes for the same pictures.
/// </remarks>
[TestClass]
public sealed class SavedPictureTests
{
    // the 16 standard colors, black to white, silver before gray.
    private static readonly uint[] Standard =
        [0x000000, 0x800000, 0x008000, 0x808000, 0x000080, 0x800080, 0x008080, 0xC0C0C0, 0x808080, 0xFF0000, 0x00FF00, 0xFFFF00, 0x0000FF, 0xFF00FF, 0x00FFFF, 0xFFFFFF];

    private static readonly uint[] Grays = [0x000000, 0x404040, 0x808080, 0xFFFFFF];

    private static readonly byte[] Pixels4x3 = [0, 1, 2, 3, 3, 2, 1, 0, 1, 1, 2, 2];

    // a GIF of 4 by 3 in Grays, Pixels4x3: at 4 bits, its table of 4 grays filled with black.
    private const string Gray4x3Saved =
        "424D82000000000000007600000028000000040000000300000001000400000000000C00000000000000000000000000000000000000000000004040"
        + "400080808000FFFFFF000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001122"
        + "00003210000001230000";

    // an icon's table: black, maroon, green, olive, navy, purple, teal, gray, silver, red, lime, yellow, blue, fuchsia, aqua, white.
    private const string IconTable =
        "0000000000008000008000000080800080000000800080008080000080808000C0C0C0000000FF0000FF000000FFFF00FF000000FF00FF00FFFF0000FFFFFF00";

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

    // an icon of one 32 by 32 image of 32 bits: each pixel as 0xAARRGGBB from the top left, and its mask, set where asked.
    private static byte[] Icon32(Func<int, int, uint> argb, Func<int, int, bool> masked)
    {
        var rows = new byte[32 * 32 * 4];
        var mask = new byte[32 * 4];
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var row = 31 - y;
                BinaryPrimitives.WriteUInt32LittleEndian(rows.AsSpan((row * 32 + x) * 4), argb(x, y));
                if (masked(x, y))
                    mask[row * 4 + x / 8] |= (byte)(0x80 >> x % 8);
            }
        }

        return Icon(1, IconDib(32, 32, 32, [], rows, mask));
    }

    // a saved icon of one 32 by 32 image: its color table, its rows of 4 bits and its mask's rows from the bottom up, in hex.
    private static (string Table, string[] Colors, string[] Mask) IconParts(byte[] icon)
    {
        Assert.AreEqual(766, icon.Length);
        const int Table = 22 + 40, Colors = Table + 64, Mask = Colors + 32 * 16;
        return (Convert.ToHexString(icon, Table, 64),
            Enumerable.Range(0, 32).Select(row => Convert.ToHexString(icon, Colors + row * 16, 16)).ToArray(),
            Enumerable.Range(0, 32).Select(row => Convert.ToHexString(icon, Mask + row * 4, 4)).ToArray());
    }

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

    // The length and the first 16 bytes MS-VBA's SavePicture writes for pictures of these sizes: a GIF of 7 by 5 with a table of 256 colors as an 8-bit
    // bitmap, and an icon's or a cursor's 32 by 32 image as an icon of 16 colors. The PNG, 6 by 4, as a 24-bit bitmap, is RD-VBA's own.
    [TestMethod]
    public void AGif_APng_AnIcon_AndACursor_AreSavedInMSVBAsLayout()
    {
        var gif = Gif(7, 5, Enumerable.Range(0, 256).Select(i => (uint)i).ToArray(), new byte[35], 7, 5);
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
    [Ignore("JPEG pixels are not decoded")]
    public void AJpeg_IsSavedAsA24BitBitmap()
        => Assert.AreEqual("len 362 424D6A01000000000000360000002800", Head(Saved(JpegHeader(9, 11))));

    // a GIF of 4 by 3 with a table of 4 grays, and one with a table of 2 colors: the least depth that numbers its table, the table filled with black.
    [TestMethod]
    public void AGif_IsSavedAtTheLeastDepthThatNumbersItsTable_ItsTableFilledWithBlack()
    {
        Assert.AreEqual(Gray4x3Saved, Convert.ToHexString(Saved(Gif(4, 3, Grays, Pixels4x3, 4, 3))));
        Assert.AreEqual(
            "424D4A000000000000003E00000028000000040000000300000001000100000000000C0000000000000000000000000000000000000056341200EFCD"
            + "AB0030000000A000000050000000",
            Convert.ToHexString(Saved(Gif(4, 3, [0x123456, 0xABCDEF], [0, 1, 0, 1, 1, 0, 1, 0, 0, 0, 1, 1], 4, 3))));
    }

    // the same frame at 1,1 on a screen of 6 by 5, its color 1 transparent: the picture is the frame, and the transparent pixels keep their number.
    [TestMethod]
    public void AGifsFrame_IsSavedAtItsOwnSize_AndItsTransparentColor_AsItsOwnNumber()
        => Assert.AreEqual(Gray4x3Saved, Convert.ToHexString(Saved(Gif(6, 5, Grays, Pixels4x3, 4, 3, left: 1, top: 1, transparent: 1))));

    // a local table of red, lime, blue and yellow: the standard colors it lacks fill its table.
    [TestMethod]
    public void AGifsTableOfStandardColors_IsFilledWithTheStandardColorsItLacks()
        => Assert.AreEqual(
            "424D82000000000000007600000028000000040000000300000001000400000000000C000000000000000000000000000000000000000000FF0000FF"
            + "0000FF00000000FFFF0000000000000080000080000000808000800000008000800080800000C0C0C00080808000FF00FF00FFFF0000FFFFFF001122"
            + "00003210000001230000",
            Convert.ToHexString(Saved(Gif(4, 3, [0xFF0000, 0x00FF00, 0x0000FF, 0xFFFF00], Pixels4x3, 4, 3, local: true))));

    // its LZW data cut to its first 2 bytes, which give its first row: the pixels never decoded are number 0.
    [TestMethod]
    public void AGifCutShort_IsSavedWithThePixelsItLacksAsNumber0()
    {
        var gif = Gif(4, 3, Grays, Pixels4x3, 4, 3)[..39];
        gif[36] = 2;

        Assert.AreEqual(Gray4x3Saved[..^24] + "000000000000000001230000", Convert.ToHexString(Saved(gif)));
    }

    // RLE8 of 4 by 2 with a table of 4 colors (a run, a delta, the end of a line, an absolute run, the end of the bitmap), and RLE4 of 4 by 1: each at 4
    // bits, its table filled with the standard colors it lacks, and the pixels the compression skips are number 0.
    [TestMethod]
    public void ACompressedBitmap_IsSavedAtTheLeastDepthThatNumbersItsTable_ItsSkippedPixelsAsNumber0()
    {
        uint[] colors = [0x000000, 0xFF0000, 0x00FF00, 0x0000FF];

        Assert.AreEqual(
            "424D7E000000000000007600000028000000040000000200000001000400000000000800000000000000000000000000000000000000000000000000"
            + "FF0000FF0000FF000000000080000080000000808000800000008000800080800000C0C0C0008080800000FFFF00FF00FF00FFFF0000FFFFFF001100"
            + "000032100000",
            Convert.ToHexString(Saved(Bitmap(4, 2, 8, 1, colors, [2, 1, 0, 2, 1, 0, 0, 0, 0, 3, 3, 2, 1, 0, 0, 1], used: 4))));
        Assert.AreEqual(
            "424D7A000000000000007600000028000000040000000100000001000400000000000400000000000000000000000000000000000000000000000000"
            + "FF0000FF0000FF000000000080000080000000808000800000008000800080800000C0C0C0008080800000FFFF00FF00FF00FFFF0000FFFFFF001212"
            + "0000",
            Convert.ToHexString(Saved(Bitmap(4, 1, 4, 2, colors, [4, 0x12, 0, 1], used: 4))));
    }

    // the 16 colors across, 2 pixels each, and below them their midpoints toward 40 40 40, opaque: each pixel is the nearest of the icon's 16 colors, the
    // first where several are as near.
    [TestMethod]
    public void AnIconsPixels_AreTheNearestOf16Colors_TheFirstWhereSeveralAreAsNear()
    {
        var (table, colors, mask) = IconParts(Saved(Icon32(
            (x, y) => 0xFF000000 | (y < 16 ? Standard[x / 2] : (Standard[x / 2] & 0xFEFEFE) >> 1 | 0x404040), (_, _) => false)));

        Assert.AreEqual(IconTable, table);
        Assert.IsTrue(colors[..16].All(row => row == "00000000000000770011223344556677"), string.Join(" ", colors[..16]));
        Assert.IsTrue(colors[16..].All(row => row == "00112233445566887799AABBCCDDEEFF"), string.Join(" ", colors[16..]));
        Assert.IsTrue(mask.All(row => row == "00000000"), string.Join(" ", mask));
    }

    // a 32-bit image of red whose alpha is 0 on the left, 128 top right and 255 bottom right, its mask clear: the mask is the image's, whatever its alpha.
    [TestMethod]
    public void AnIconsMask_IsItsImagesMask_WhateverItsAlpha()
    {
        var (_, colors, mask) = IconParts(Saved(Icon32((x, y) => (x < 16 ? 0u : y < 16 ? 128u : 255u) << 24 | 0xFF0000, (_, _) => false)));

        Assert.IsTrue(colors.All(row => row == new string('9', 32)), string.Join(" ", colors));
        Assert.IsTrue(mask.All(row => row == "00000000"), string.Join(" ", mask));
    }

    // a 32-bit image of lime, alpha 0, its mask set on a checkerboard of 4-pixel squares: the mask is kept, and the pixels it is set for keep their color.
    [TestMethod]
    public void AnIconsMaskedPixels_KeepTheirColor()
    {
        var (_, colors, mask) = IconParts(Saved(Icon32((_, _) => 0x00FF00, (x, y) => (x / 4 + y / 4) % 2 == 1)));

        Assert.IsTrue(colors.All(row => row == new string('A', 32)), string.Join(" ", colors));
        for (var row = 0; row < 32; row++)
            Assert.AreEqual((31 - row) / 4 % 2 == 1 ? "F0F0F0F0" : "0F0F0F0F", mask[row], $"row {row}");
    }

    // a 4-bit image whose table is the 16 colors reversed, 2 pixels of each number across, its 2 right columns masked: the table is replaced, each number is
    // the same color's in it, and the mask is kept.
    [TestMethod]
    public void AnIcons4BitImage_IsSavedInTheIconColors_ItsNumbersRemapped()
    {
        var rows = Enumerable.Range(0, 32 * 16).Select(i => (byte)(i % 16 * 0x11)).ToArray();
        var masked = Enumerable.Range(0, 32 * 4).Select(i => (byte)(i % 4 == 3 ? 0x03 : 0)).ToArray();

        var (table, colors, mask) = IconParts(Saved(Icon(1, IconDib(32, 32, 4, Standard.Reverse().ToArray(), rows, masked))));

        Assert.AreEqual(IconTable, table);
        Assert.IsTrue(colors.All(row => row == "FFEEDDCCBBAA99778866554433221100"), string.Join(" ", colors));
        Assert.IsTrue(mask.All(row => row == "00000003"), string.Join(" ", mask));
    }
}
