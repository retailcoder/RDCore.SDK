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
    private static byte[] Icon32(Func<int, int, uint> argb, Func<int, int, bool> masked, short type = 1)
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

        return Icon(type, IconDib(32, 32, 32, [], rows, mask));
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

    // a new picture: a file of no bytes; the empty one LoadPicture gives for no file name: none, as MS-VBA raises 380 for it; a picture whose pixels are not
    // decoded (a bitmap of BI_JPEG pixels): none either.
    [TestMethod]
    public void ThePictureSaved_OrWhyNoneIs()
    {
        Assert.AreEqual(SavedPictureFailure.None, SavedPicture.TryBytesOf(StdPictureObject.CreateNew(), out var none));
        Assert.AreEqual(0, none!.Length);
        Assert.AreEqual(SavedPictureFailure.NoPicture, SavedPicture.TryBytesOf(StdPictureObject.CreateEmpty(), out var empty));
        Assert.IsNull(empty);
        Assert.AreEqual(SavedPictureFailure.None, SavedPicture.TryBytesOf(Loaded(Bitmap2x2), out var bitmap));
        CollectionAssert.AreEqual(SavedPicture.BytesOf(Loaded(Bitmap2x2)), bitmap);
        Assert.AreEqual(SavedPictureFailure.NotDecoded, SavedPicture.TryBytesOf(Loaded(Bitmap(1, 1, 24, 4, [], [0, 0, 0, 0])), out var jpeg));
        Assert.IsNull(jpeg);
        Assert.ThrowsExactly<ArgumentNullException>(() => SavedPicture.TryBytesOf(null!, out _));
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

    // RD-VBA's own, where MS-VBA writes 24 bits: a bitmap whose bit fields state an alpha field (BI_ALPHABITFIELDS), of red opaque, blue clear and green at
    // alpha 128, saved at 32 bits per pixel under a BITMAPV5HEADER whose fields state the alpha; saved again from that bitmap, it is the same file.
    [TestMethod]
    [DataRow(32, new uint[] { 0xFF0000, 0xFF00, 0xFF, 0xFF000000 }, "0000FFFFFF00000000FF0080", "0000FFFF FF000000 00FF0080", DisplayName = "32 bits")]
    [DataRow(16, new uint[] { 0x7C00, 0x3E0, 0x1F, 0x8000 }, "00FC1F00", "0000FFFF FF000000", DisplayName = "16 bits, 1 of alpha")]
    public void ABitmapWhoseBitFieldsStateAnAlphaField_IsSavedAt32Bits_KeepingItsAlpha(int bits, uint[] fields, string pixels, string saved)
    {
        var bitmap = Bitmap(bits == 32 ? 3 : 2, 1, bits, 6, fields, Convert.FromHexString(pixels));

        var once = Saved(bitmap);
        var twice = Saved(once);

        Assert.AreEqual((124, 32, 3), (BinaryPrimitives.ReadInt32LittleEndian(once.AsSpan(14)), (int)BinaryPrimitives.ReadUInt16LittleEndian(once.AsSpan(28)),
            BinaryPrimitives.ReadInt32LittleEndian(once.AsSpan(30))));
        Assert.AreEqual("0000FF0000FF0000FF000000000000FF42475273", Convert.ToHexString(once, 54, 20));
        Assert.AreEqual(saved, string.Join(" ", Enumerable.Range(0, bits == 32 ? 3 : 2).Select(pixel => Convert.ToHexString(once, 138 + pixel * 4, 4))));
        Assert.AreEqual(Convert.ToHexString(Decoded(bitmap).Bgra.Span), Convert.ToHexString(Decoded(once).Bgra.Span));
        CollectionAssert.AreEqual(once, twice);
    }

    // The length and the first 16 bytes MS-VBA's SavePicture writes for pictures of these sizes: a GIF of 7 by 5 with a table of 256 colors as an 8-bit
    // bitmap, and an icon's or a cursor's 32 by 32 image as an icon of 16 colors.
    [TestMethod]
    public void AGif_AnIcon_AndACursor_AreSavedInMSVBAsLayout()
    {
        var gif = Gif(7, 5, Enumerable.Range(0, 256).Select(i => (uint)i).ToArray(), new byte[35], 7, 5);
        var dib = IconDib(32, 32, 32, [], new byte[32 * 32 * 4], new byte[32 * 4]);

        Assert.AreEqual("len 1118 424D5E04000000000000360400002800", Head(Saved(gif)));
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

    // a PNG of RGBA pixels, row by row from the top, each as 0xRRGGBBAA, with each row's filter byte (none) first.
    private static byte[] Rgba(int width, params uint[] pixels)
    {
        var height = pixels.Length / width;
        var rows = new byte[height * (1 + width * 4)];
        for (var i = 0; i < pixels.Length; i++)
            BinaryPrimitives.WriteUInt32BigEndian(rows.AsSpan(i / width * (1 + width * 4) + 1 + i % width * 4), pixels[i]);
        return Png(width, height, 6, 8, rows);
    }

    // red opaque, blue clear (alpha 0) and green at alpha 128.
    private static readonly uint[] OpaqueClearPartial = [0xFF0000FF, 0x0000FF00, 0x00FF0080];

    // a 32 by 32 picture whose top rows are OpaqueClearPartial across, and whose other pixels are clear.
    private static uint[] Pixels32()
    {
        var pixels = new uint[32 * 32];
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 30; x++)
                pixels[y * 32 + x] = OpaqueClearPartial[x % 3];
        return pixels;
    }

    // RD-VBA's own, as loading a PNG is: a PNG of 8 bits per channel and color type 6, whatever it was loaded from; here a gray one of 8 bits.
    [TestMethod]
    public void APng_IsSavedAsAPngOf8BitsPerChannel_WithItsAlpha()
    {
        var saved = Saved(Png(3, 1, 0, 8, [0, 0x10, 0x80, 0xF0]));

        Assert.AreEqual("89504E470D0A1A0A0000000D49484452000000030000000108060000001BE014B4", Convert.ToHexString(saved, 0, 33));
        Assert.AreEqual("0000000049454E44AE426082", Convert.ToHexString(saved[^12..]));
        Assert.AreEqual("101010FF808080FFF0F0F0FF", Convert.ToHexString(Decoded(saved).Bgra.Span));
        Assert.AreEqual((PictureFormat.Png, (short)1), (Loaded(saved).Format, Loaded(saved).Type));
    }

    // LoadPicture of the PNG, SavePicture, LoadPicture of the file saved, and SavePicture again: each pixel keeps its color and its alpha, and the second file
    // is the first again.
    [TestMethod]
    [DataRow(0, DisplayName = "opaque")]
    [DataRow(1, DisplayName = "clear")]
    [DataRow(2, DisplayName = "alpha 128")]
    public void APngSavedAndLoadedAgain_KeepsEachPixelsAlpha(int pixel)
    {
        var png = Rgba(3, OpaqueClearPartial);

        var once = Saved(png);
        var twice = Saved(once);

        Assert.AreEqual(Convert.ToHexString(Decoded(png).Bgra.Slice(pixel * 4, 4).Span), Convert.ToHexString(Decoded(once).Bgra.Slice(pixel * 4, 4).Span));
        Assert.AreEqual(Convert.ToHexString(Decoded(png).Bgra.Slice(pixel * 4, 4).Span), Convert.ToHexString(Decoded(twice).Bgra.Slice(pixel * 4, 4).Span));
        CollectionAssert.AreEqual(once, twice);
    }

    // RD-VBA's own, as loading it is: an icon or a cursor whose image is a 32 by 32 PNG, saved as an icon of that one image stored as a PNG, which keeps
    // each pixel's color and alpha, with no AND mask; saved again from that icon, it is the same file.
    [TestMethod]
    [DataRow((short)1, DisplayName = "an icon")]
    [DataRow((short)2, DisplayName = "a cursor")]
    public void AnIconOrACursorWhoseImageIsAPng_IsSavedAsAnIconOfThatPng_KeepingItsAlpha(short type)
    {
        var icon = Icon(type, Rgba(32, Pixels32()));

        var saved = Saved(icon);
        var again = Saved(saved);

        Assert.AreEqual("0000010001002020000001002000", Convert.ToHexString(saved, 0, 14));
        Assert.AreEqual(saved.Length - 22, BinaryPrimitives.ReadInt32LittleEndian(saved.AsSpan(14)));
        Assert.AreEqual(22, BinaryPrimitives.ReadInt32LittleEndian(saved.AsSpan(18)));
        Assert.AreEqual("89504E470D0A1A0A", Convert.ToHexString(saved, 22, 8));
        Assert.AreEqual(Convert.ToHexString(Decoded(icon).Bgra.Span), Convert.ToHexString(Decoded(saved).Bgra.Span));
        Assert.IsNull(Decoded(saved).Mask);
        CollectionAssert.AreEqual(saved, again);
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

    // an 8 by 8 JPEG of 4 colored squares, as System.Drawing encodes it: the bytes MS-VBA's SavePicture writes for it, to the last pixel.
    [TestMethod]
    public void AJpeg_IsSavedAsTheBitmapOfThePixelsMSVBADecodes()
    {
        var jpeg = Hex(
            "FFD8FFE000104A46494600010101006000600000FFDB0043000302020302020303030304030304050805050404050A070706080C0A0C0C0B0A0B0B0D",
            "0E12100D0E110E0B0B1016101113141515150C0F171816141812141514FFDB00430103040405040509050509140D0B0D141414141414141414141414",
            "1414141414141414141414141414141414141414141414141414141414141414141414141414FFC00011080008000803012200021101031101FFC400",
            "1F0000010501010101010100000000000000000102030405060708090A0BFFC400B5100002010303020403050504040000017D010203000411051221",
            "31410613516107227114328191A1082342B1C11552D1F02433627282090A161718191A25262728292A3435363738393A434445464748494A53545556",
            "5758595A636465666768696A737475767778797A838485868788898A92939495969798999AA2A3A4A5A6A7A8A9AAB2B3B4B5B6B7B8B9BAC2C3C4C5C6",
            "C7C8C9CAD2D3D4D5D6D7D8D9DAE1E2E3E4E5E6E7E8E9EAF1F2F3F4F5F6F7F8F9FAFFC4001F0100030101010101010101010000000000000102030405",
            "060708090A0BFFC400B51100020102040403040705040400010277000102031104052131061241510761711322328108144291A1B1C109233352F015",
            "6272D10A162434E125F11718191A262728292A35363738393A434445464748494A535455565758595A636465666768696A737475767778797A828384",
            "85868788898A92939495969798999AA2A3A4A5A6A7A8A9AAB2B3B4B5B6B7B8B9BAC2C3C4C5C6C7C8C9CAD2D3D4D5D6D7D8D9DAE2E3E4E5E6E7E8E9EA",
            "F2F3F4F5F6F7F8F9FAFFDA000C03010002110311003F004F057FC13BFF00E17EFDB3FE2E07F617F64ECFF982FDA3CDF3777FD3C26DC795EF9CF6C514",
            "515DDE1DF889C4F96F0C6130B85C5F2C23CF65C94DEF526DEAE0DEEDF53E5323CEF30E34CBE9E7D9F54F6D8AAD7E79DA31BF2C9C23EEC1462AD18C56",
            "9157B5DEB767FFD9");

        Assert.AreEqual(string.Concat(
            "424DF600000000000000360000002800000008000000080000000100180000000000C00000000000000000000000000000000000000000FF0700FE02",
            "06FF0224E01DDCFFDCFCFAFFFFFCFFFFFBFF00FC0200FE0000FF002BE926DBFFDAFFFCFFFFFDFFFFFEFF05FF0200FD0000FF0025E722E3FFDCFFF7FF",
            "FFFEFFF8FDFE15D25115D8520CD44B3EC35CFFFFE9FFF6F8FFF8F6FFFAF5002FAB002AA50033AC2F338B81051DC00600C70E04C209000000FB0004FF",
            "0000FB480FC6AF002FFC0800FB0200FF06000501FF0200FD0003FF450BC4B30034F90000FB0200F700000600FD0300FE0000FC4C12CEAD002DFE0300",
            "F90101F80202"),
            Convert.ToHexString(Saved(jpeg)));
    }

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

    // the same frame at 1,1 on a screen of 6 by 5, its color 9 transparent, which none of its pixels is: the picture is the frame, as MS-VBA writes it.
    [TestMethod]
    public void AGifsFrame_IsSavedAtItsOwnSize()
        => Assert.AreEqual(Gray4x3Saved, Convert.ToHexString(Saved(Gif(6, 5, Grays, Pixels4x3, 4, 3, left: 1, top: 1, transparent: 9))));

    // RD-VBA's own, where MS-VBA writes the transparent color as its own number in 4 bits: the same frame, its color 1 transparent, written at 32 bits per
    // pixel under a BITMAPV5HEADER, its pixels of color 1 clear and the others opaque.
    [TestMethod]
    public void AGifWhosePixelsAreItsTransparentColor_IsSavedAt32Bits_ThosePixelsClear()
    {
        var gif = Gif(6, 5, Grays, Pixels4x3, 4, 3, left: 1, top: 1, transparent: 1);

        var saved = Saved(gif);

        Assert.AreEqual((124, 4, 3, 32), (BinaryPrimitives.ReadInt32LittleEndian(saved.AsSpan(14)), BinaryPrimitives.ReadInt32LittleEndian(saved.AsSpan(18)),
            BinaryPrimitives.ReadInt32LittleEndian(saved.AsSpan(22)), (int)BinaryPrimitives.ReadUInt16LittleEndian(saved.AsSpan(28))));
        Assert.AreEqual("255 0 255 255 255 255 0 255 0 0 255 255", string.Join(" ", Pixels4x3.Select((_, pixel) => Decoded(saved).Bgra.Span[pixel * 4 + 3])));
        Assert.AreEqual(Convert.ToHexString(Decoded(gif).Bgra.Span), Convert.ToHexString(Decoded(saved).Bgra.Span));
    }

    // SavePicture keeps the alpha the decoder reads from a picture, and here every color too: the file saved decodes to the pixels the picture decodes to,
    // alpha included, and saved again from that file, it decodes to them again.
    [TestMethod]
    [DataRow("a 32-bit bitmap of BI_RGB, opaque")]
    [DataRow("an 8-bit bitmap")]
    [DataRow("a bitmap with an alpha field")]
    [DataRow("a GIF")]
    [DataRow("a GIF with a transparent color")]
    [DataRow("a gray PNG")]
    [DataRow("a gray PNG with alpha")]
    [DataRow("a PNG of opaque, clear and partial alpha")]
    [DataRow("an icon's 24-bit image and its mask")]
    [DataRow("an icon's 32-bit image of alpha 0 and its mask")]
    [DataRow("an icon's 32-bit image with alpha")]
    [DataRow("an icon's PNG image")]
    [DataRow("a cursor's PNG image")]
    public void ThePictureSaved_DecodesToThePicturesPixels_AlphaIncluded(string kind)
    {
        var picture = kind switch
        {
            "a 32-bit bitmap of BI_RGB, opaque" => Bitmap(2, 1, 32, 0, [], [1, 2, 3, 4, 5, 6, 7, 8]),
            "an 8-bit bitmap" => Bitmap(3, 2, 8, 0, [0x112233, 0x445566], [1, 0, 1, 0, 0, 1, 0, 0], used: 2),
            "a bitmap with an alpha field" => Bitmap(3, 1, 32, 6, [0xFF0000, 0xFF00, 0xFF, 0xFF000000], Convert.FromHexString("0000FFFFFF00000000FF0080")),
            "a GIF" => Gif(4, 3, Grays, Pixels4x3, 4, 3),
            "a GIF with a transparent color" => Gif(4, 3, Grays, Pixels4x3, 4, 3, transparent: 2),
            "a gray PNG" => Png(3, 1, 0, 8, [0, 0x10, 0x80, 0xF0]),
            "a gray PNG with alpha" => Png(2, 1, 4, 8, [0, 0x40, 0x00, 0xC0, 0x80]),
            "a PNG of opaque, clear and partial alpha" => Rgba(3, OpaqueClearPartial),
            "an icon's 24-bit image and its mask" => Icon(1, IconDib(2, 1, 24, [], [255, 255, 255, 0, 0, 0, 0, 0], [0x40, 0, 0, 0])),
            "an icon's 32-bit image of alpha 0 and its mask" => Icon32((_, _) => 0x00FF00, (x, y) => (x / 4 + y / 4) % 2 == 1),
            "an icon's 32-bit image with alpha" => Icon32((x, y) => (x < 16 ? 0u : y < 16 ? 128u : 255u) << 24 | 0xFF0000, (_, y) => y == 31),
            "an icon's PNG image" => Icon(1, Rgba(32, Pixels32())),
            "a cursor's PNG image" => Icon(2, Rgba(32, Pixels32())),
            _ => throw new AssertFailedException(kind),
        };

        var once = Saved(picture);

        Assert.AreEqual(Convert.ToHexString(Decoded(picture).Bgra.Span), Convert.ToHexString(Decoded(once).Bgra.Span));
        Assert.AreEqual(Convert.ToHexString(Decoded(picture).Bgra.Span), Convert.ToHexString(Decoded(Saved(once)).Bgra.Span));
    }

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

    // RD-VBA's own, where MS-VBA writes the 16 colors: an icon's or a cursor's 32-bit image of red whose alpha is 0 on the left, 128 top right and 255 bottom
    // right, its mask set on the bottom row only, saved as an icon of that image at 32 bits per pixel, each pixel's color and alpha kept, with the image's
    // own mask; saved again from that icon, it is the same file.
    [TestMethod]
    [DataRow((short)1, DisplayName = "an icon")]
    [DataRow((short)2, DisplayName = "a cursor")]
    public void A32BitIconImageWhoseAlphaIsItsOwn_IsSavedAt32Bits_WithItsAlphaAndItsMask(short type)
    {
        var icon = Icon32((x, y) => (x < 16 ? 0u : y < 16 ? 128u : 255u) << 24 | 0xFF0000, (_, y) => y == 31, type);

        var saved = Saved(icon);
        var again = Saved(saved);

        Assert.AreEqual(22 + 40 + 32 * 32 * 4 + 32 * 4, saved.Length);
        Assert.AreEqual("0000010001002020000001002000", Convert.ToHexString(saved, 0, 14));
        Assert.AreEqual((32, 1, 0), ((int)BinaryPrimitives.ReadUInt16LittleEndian(saved.AsSpan(22 + 14)), (int)BinaryPrimitives.ReadUInt16LittleEndian(saved.AsSpan(22 + 12)),
            BinaryPrimitives.ReadInt32LittleEndian(saved.AsSpan(22 + 16))));
        Assert.AreEqual(Convert.ToHexString(Decoded(icon).Bgra.Span), Convert.ToHexString(Decoded(saved).Bgra.Span));
        Assert.AreEqual("0000FF00 0000FF80 0000FFFF", string.Join(" ", new[] { 0, 16, 31 * 32 + 16 }.Select(pixel => Convert.ToHexString(Decoded(saved).Bgra.Slice(pixel * 4, 4).Span))));
        CollectionAssert.AreEqual(Decoded(icon).Mask!.Value.ToArray(), Decoded(saved).Mask!.Value.ToArray());
        CollectionAssert.AreEqual(saved, again);
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
