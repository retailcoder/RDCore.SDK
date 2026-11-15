using RDCore.SDK.Workspace.Pictures;
using static RDCore.Tests.Workspace.Pictures.TestPictures;

namespace RDCore.Tests.Workspace.Pictures;

/// <summary>
/// A picture's pixels (<see cref="PictureDecoder"/>): a bitmap, a PNG, a GIF and an icon's image, each to 32-bit BGRA, with the color numbers, the mask and
/// the completeness kept; and never an exception, nor more pixels than the header states.
/// </summary>
[TestClass]
public sealed class PictureDecoderTests
{
    private static DecodedPicture Decoded(byte[] bytes)
    {
        Assert.AreEqual(PictureDecodeFailure.None, PictureDecoder.TryDecode(bytes, out var picture));
        return picture!;
    }

    // each pixel as AARRGGBB, row by row from the top.
    private static string Pixels(DecodedPicture picture)
    {
        var bgra = picture.Bgra.ToArray();
        return string.Join(" ", Enumerable.Range(0, picture.Width * picture.Height)
            .Select(i => $"{bgra[i * 4 + 3]:X2}{bgra[i * 4 + 2]:X2}{bgra[i * 4 + 1]:X2}{bgra[i * 4]:X2}"));
    }

    private static string Numbers(ReadOnlyMemory<byte>? numbers) => string.Join(" ", numbers!.Value.ToArray());

    [TestMethod]
    public void ABitmapOf24Bits_IsItsColors_FromTheTopDown_AndOpaque()
    {
        var picture = Decoded(Bitmap2x2);

        Assert.AreEqual("FF0000FF FFFFFFFF FFFF0000 FF00FF00", Pixels(picture));
        Assert.IsTrue(picture.IsComplete);
        Assert.IsTrue(picture.Palette.IsEmpty);
        Assert.IsNull(picture.Indexes);
    }

    [TestMethod]
    public void ABitmapOf8Bits_KeepsItsNumbersAndItsTable_OfTheColorsItUses()
    {
        var picture = Decoded(Bitmap(3, 1, 8, 0, [0x0000FF, 0x00FF00, 0xFF0000], [2, 0, 1, 0], used: 3));

        Assert.AreEqual("2 0 1", Numbers(picture.Indexes));
        CollectionAssert.AreEqual(new uint[] { 0xFF0000FF, 0xFF00FF00, 0xFFFF0000 }, picture.Palette.ToArray());
        Assert.AreEqual("FFFF0000 FF0000FF FF00FF00", Pixels(picture));
    }

    // 1 and 4 bits a pixel, from the high bits of each byte.
    [TestMethod]
    [DataRow(1, new byte[] { 0b1010_0000, 0, 0, 0 }, "1 0 1")]
    [DataRow(4, new byte[] { 0x10, 0x10, 0, 0 }, "1 0 1")]
    public void ABitmapOfFewerBits_PacksItsNumbersFromTheHighBits(int bits, byte[] row, string expected)
    {
        Assert.AreEqual(expected, Numbers(Decoded(Bitmap(3, 1, bits, 0, [0, 0xFFFFFF], row)).Indexes));
    }

    // RLE8, from the bottom row up: a run of 3, then 3 pixels as they are, padded, the last past the edge; the end of the row; a move across 1; a run of 2;
    // the end of the bitmap. What no code gives is 0.
    private static readonly byte[] Rle8 = [3, 5, 0, 3, 6, 7, 8, 0, 0, 0, 0, 2, 1, 0, 2, 4, 0, 1];

    [TestMethod]
    public void AnRle8Bitmap_IsItsRunsAbsoluteRunsAndDeltas_FromTheBottomUp()
    {
        var table = Enumerable.Range(0, 9).Select(i => (uint)(i * 0x101010)).ToArray();
        var picture = Decoded(Bitmap(5, 2, 8, 1, table, Rle8));

        Assert.AreEqual("0 4 4 0 0 5 5 5 6 7", Numbers(picture.Indexes));
        Assert.IsTrue(picture.IsComplete);
    }

    [TestMethod]
    public void AnRle4Bitmap_AlternatesTheTwoNumbersOfARun()
    {
        var picture = Decoded(Bitmap(5, 1, 4, 2, Enumerable.Repeat(0u, 16).ToArray(), [5, 0x12, 0, 1]));

        Assert.AreEqual("1 2 1 2 1", Numbers(picture.Indexes));
    }

    [TestMethod]
    public void ABitmapWithBitFields_ScalesEachFieldTo8Bits()
    {
        // 5-6-5: a pure red, and a pure green.
        var picture = Decoded(Bitmap(2, 1, 16, 3, [0xF800, 0x07E0, 0x001F], [0x00, 0xF8, 0xE0, 0x07]));

        Assert.AreEqual("FFFF0000 FF00FF00", Pixels(picture));
    }

    // a bitmap file's fourth byte of a 32-bit pixel is not alpha.
    [TestMethod]
    public void ABitmapOf32Bits_IsOpaque()
    {
        Assert.AreEqual("FF112233", Pixels(Decoded(Bitmap(1, 1, 32, 0, [], [0x33, 0x22, 0x11, 0x00]))));
    }

    [TestMethod]
    public void ABitmapCutShort_GivesTheRowsItHas_AndTheOthersAre0()
    {
        var bytes = Bitmap(1, 2, 24, 0, [], [0xFF, 0, 0, 0, 0, 0xFF, 0, 0]);
        var picture = Decoded(bytes[..^4]);

        Assert.AreEqual("00000000 FF0000FF", Pixels(picture));
        Assert.IsFalse(picture.IsComplete);
    }

    // RGBA, each row with another filter: none, sub, up, average, Paeth.
    [TestMethod]
    public void APngsRows_AreUnfiltered_WithEveryFilter()
    {
        var pixels = new byte[][] { [10, 20, 30, 255, 40, 50, 60, 128], [1, 2, 3, 4, 5, 6, 7, 8], [9, 9, 9, 9, 200, 100, 50, 25], [3, 1, 4, 1, 5, 9, 2, 6], [7, 7, 7, 7, 1, 1, 1, 1] };
        var rows = new List<byte>();
        byte[] previous = new byte[8];
        for (var r = 0; r < pixels.Length; r++)
        {
            rows.Add((byte)r);
            var row = pixels[r];
            for (var i = 0; i < 8; i++)
            {
                int left = i >= 4 ? row[i - 4] : 0, up = previous[i], upLeft = i >= 4 ? previous[i - 4] : 0;
                int p = left + up - upLeft, pa = Math.Abs(p - left), pb = Math.Abs(p - up), pc = Math.Abs(p - upLeft);
                var predicted = r switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    _ => pa <= pb && pa <= pc ? left : pb <= pc ? up : upLeft,
                };
                rows.Add((byte)(row[i] - predicted));
            }
            previous = row;
        }

        var picture = Decoded(Png(2, 5, 6, 8, [.. rows]));

        Assert.AreEqual("FF0A141E 8028323C 04010203 08050607 09090909 19C86432 01030104 06050902 07070707 01010101", Pixels(picture));
        Assert.IsTrue(picture.IsComplete);
    }

    // 2-bit indexed color with tRNS: the numbers and the table are kept, each entry with its alpha.
    [TestMethod]
    public void AnIndexedPng_KeepsItsNumbersAndItsTable_WithTheAlphaOfTRNS()
    {
        var picture = Decoded(Png(4, 1, 3, 2, [0, 0b00_01_10_01], chunks: [("PLTE", [255, 0, 0, 0, 255, 0, 0, 0, 255]), ("tRNS", [255, 0])]));

        Assert.AreEqual("0 1 2 1", Numbers(picture.Indexes));
        CollectionAssert.AreEqual(new uint[] { 0xFFFF0000, 0x0000FF00, 0xFF0000FF }, picture.Palette.ToArray());
        Assert.AreEqual("FFFF0000 0000FF00 FF0000FF 0000FF00", Pixels(picture));
    }

    // gray of 16 bits keeps its high byte, its tRNS gray transparent; gray of 1 bit is black or white.
    [TestMethod]
    public void AGrayPng_IsScaledTo8Bits_AndItsTRNSGray_IsTransparent()
    {
        Assert.AreEqual("FF121212 00ABABAB", Pixels(Decoded(Png(2, 1, 0, 16, [0, 0x12, 0x34, 0xAB, 0xCD], chunks: [("tRNS", [0xAB, 0xCD])]))));
        Assert.AreEqual("FFFFFFFF FF000000 FFFFFFFF", Pixels(Decoded(Png(3, 1, 0, 1, [0, 0b1010_0000]))));
    }

    [TestMethod]
    public void ATruecolorPng_ItsTRNSColor_IsTransparent_AndGrayWithAlpha_HasItsAlpha()
    {
        Assert.AreEqual("FF010203 00040506", Pixels(Decoded(Png(2, 1, 2, 8, [0, 1, 2, 3, 4, 5, 6], chunks: [("tRNS", [0, 4, 0, 5, 0, 6])]))));
        Assert.AreEqual("80404040", Pixels(Decoded(Png(1, 1, 4, 8, [0, 0x40, 0x80]))));
    }

    // Adam7 places the same pixels as a picture that is not interlaced: 9 by 9 RGB, each pixel its own color.
    [TestMethod]
    public void AnInterlacedPng_IsThePictureItInterlaces()
    {
        const int side = 9;
        byte[] Color(int x, int y) => [(byte)(x * 20), (byte)(y * 20), (byte)(x + y)];
        var plain = new List<byte>();
        for (var y = 0; y < side; y++)
        {
            plain.Add(0);
            for (var x = 0; x < side; x++)
                plain.AddRange(Color(x, y));
        }

        var interlaced = new List<byte>();
        foreach (var (startX, startY, stepX, stepY) in new[] { (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2) })
        {
            for (var y = startY; y < side; y += stepY)
            {
                if (startX >= side)
                    continue;
                interlaced.Add(0);
                for (var x = startX; x < side; x += stepX)
                    interlaced.AddRange(Color(x, y));
            }
        }

        Assert.AreEqual(Pixels(Decoded(Png(side, side, 2, 8, [.. plain]))), Pixels(Decoded(Png(side, side, 2, 8, [.. interlaced], interlaced: true))));
    }

    [TestMethod]
    public void APngCutShort_GivesTheRowsItHas_AndOneWithNoIDAT_IsAll0()
    {
        // a stored block: 2 bytes of zlib header and 5 of the block's, then the first row's 4 bytes, and no more.
        var rows = new byte[] { 0, 1, 2, 3, 0, 4, 5, 6 };
        var shortened = Decoded(Png(1, 2, 2, 8, rows, idat: Deflated(rows, stored: true)[..11]));

        Assert.AreEqual("FF010203 00000000", Pixels(shortened));
        Assert.IsFalse(shortened.IsComplete);
        var empty = Decoded(PngHeader(6, 4));
        Assert.AreEqual(string.Join(" ", Enumerable.Repeat("00000000", 24)), Pixels(empty));
        Assert.IsFalse(empty.IsComplete);
    }

    // 8 MiB of zeros deflated into an IDAT for a picture of one pixel: one row is inflated, and the rest is never read.
    [TestMethod]
    public void APngWhoseIDATInflatesPastItsPixels_IsCutOffAtThem()
    {
        var rows = new byte[8 * 1024 * 1024];
        rows[1] = 0x7F;
        var picture = Decoded(Png(1, 1, 0, 8, rows));

        Assert.AreEqual(4, picture.Bgra.Length);
        Assert.AreEqual("FF7F7F7F", Pixels(picture));
    }

    [TestMethod]
    [DataRow((byte)2, (byte)4, DisplayName = "truecolor of 4 bits")]
    [DataRow((byte)3, (byte)16, DisplayName = "indexed of 16 bits")]
    [DataRow((byte)5, (byte)8, DisplayName = "color type 5")]
    public void APngOfADepthOrATypeNoPngHas_IsRefused(byte type, byte depth)
    {
        Assert.AreEqual(PictureDecodeFailure.Refused, PictureDecoder.TryDecode(Png(1, 1, type, depth, [0, 0, 0, 0, 0, 0, 0]), out var picture));
        Assert.IsNull(picture);
    }

    // 20 by 20 of a pattern long enough for the LZW codes to grow past their first size.
    [TestMethod]
    public void AGifsFrame_IsItsLzwCodesNumbers_InItsTable()
    {
        var pixels = Enumerable.Range(0, 400).Select(i => (byte)(i * 7 % 13 % 4)).ToArray();
        var picture = Decoded(Gif(20, 20, [0xFF0000, 0x00FF00, 0x0000FF, 0xFFFFFF], pixels, 20, 20));
        var colors = new[] { "FFFF0000", "FF00FF00", "FF0000FF", "FFFFFFFF" };

        CollectionAssert.AreEqual(pixels, picture.Indexes!.Value.ToArray());
        CollectionAssert.AreEqual(pixels.Select(p => colors[p]).ToArray(), Pixels(picture).Split(' '));
        Assert.IsTrue(picture.IsComplete);
        Assert.AreEqual(-1, picture.TransparentIndex);
    }

    // a frame of 2 by 2 at 1,1 on a screen of 4 by 3, its color 1 transparent: a transparent pixel, and every pixel outside the frame, is 0.
    [TestMethod]
    public void AGifsFrame_IsPlacedOnItsScreen_ItsTransparentColorClear_AndTheRestIs0()
    {
        var picture = Decoded(Gif(4, 3, [0x112233, 0x445566], [0, 1, 1, 0], 2, 2, left: 1, top: 1, transparent: 1));

        Assert.AreEqual("0 0 0 0 0 0 1 0 0 1 0 0", Numbers(picture.Indexes));
        Assert.AreEqual(1, picture.TransparentIndex);
        Assert.AreEqual(string.Join(" ", "00000000 00000000 00000000 00000000", "00000000 FF112233 00000000 00000000", "00000000 00000000 FF112233 00000000"),
            Pixels(picture));
    }

    [TestMethod]
    public void AnInterlacedGif_PlacesItsRowsInItsFourPasses()
    {
        // ten rows of one pixel each, stored in the order 0, 8, 4, 2, 6, 1, 3, 5, 7, 9: each row's number is its place.
        var stored = new byte[] { 0, 8, 4, 2, 6, 1, 3, 5, 7, 9 };
        var table = Enumerable.Range(0, 16).Select(i => (uint)i).ToArray();
        var picture = Decoded(Gif(1, 10, table, stored, 1, 10, interlaced: true));

        Assert.AreEqual("0 1 2 3 4 5 6 7 8 9", Numbers(picture.Indexes));
    }

    [TestMethod]
    public void AGifCutShort_GivesThePixelsItHas()
    {
        var pixels = Enumerable.Range(0, 400).Select(i => (byte)(i % 4)).ToArray();
        var whole = Gif(20, 20, [0, 0xFFFFFF, 0xFF, 0xFF00], pixels, 20, 20);
        var picture = Decoded(whole[..40]);

        Assert.IsFalse(picture.IsComplete);
        Assert.AreEqual(1, picture.Indexes!.Value.Span[1]);
        Assert.AreEqual(0, picture.Indexes!.Value.Span[399]);
        Assert.AreEqual("00000000", Pixels(picture).Split(' ')[399]);
    }

    // LZW codes for 1000 pixels in a frame of 2 by 2: four are decoded.
    [TestMethod]
    public void AGifWhoseCodesGivePastItsFrame_IsCutOffAtIt()
    {
        var picture = Decoded(Gif(2, 2, [0, 0xFFFFFF], Enumerable.Range(0, 1000).Select(i => (byte)(i % 2)).ToArray(), 2, 2));

        Assert.AreEqual("0 1 0 1", Numbers(picture.Indexes));
        Assert.IsTrue(picture.IsComplete);
    }

    [TestMethod]
    public void TheSmallestGif_IsOneTransparentPixel()
    {
        var picture = Decoded(Gif1x1);

        Assert.AreEqual("00000000", Pixels(picture));
        Assert.AreEqual(0, picture.TransparentIndex);
        Assert.IsTrue(picture.IsComplete);
    }

    // a 2 by 2 image of 4 bits: its colors, and its mask set on the top right; that pixel is clear.
    [TestMethod]
    public void AnIconsDibImage_KeepsItsMask_AndIsClearWhereTheMaskIsSet()
    {
        var dib = IconDib(2, 2, 4, [0x000000, 0xFF0000, 0x00FF00, 0x0000FF], [0x23, 0, 0, 0, 0x10, 0, 0, 0], [0, 0, 0, 0, 0x40, 0, 0, 0]);
        var picture = Decoded(Icon(1, dib));

        Assert.AreEqual("1 0 2 3", Numbers(picture.Indexes));
        Assert.AreEqual("0 1 0 0", Numbers(picture.Mask));
        Assert.AreEqual("FFFF0000 00000000 FF00FF00 FF0000FF", Pixels(picture));
    }

    // a 32-bit image's alpha is its transparency, unless every pixel's is 0: then the mask is.
    [TestMethod]
    public void AnIconsImageOf32Bits_HasItsAlpha_OrTheMasksWhereItsAlphaIsAll0()
    {
        var alpha = IconDib(1, 2, 32, [], [1, 2, 3, 0x80, 4, 5, 6, 0x40], [0, 0, 0, 0, 0, 0, 0, 0]);
        var none = IconDib(1, 2, 32, [], [1, 2, 3, 0, 4, 5, 6, 0], [0x80, 0, 0, 0, 0, 0, 0, 0]);

        Assert.AreEqual("40060504 80030201", Pixels(Decoded(Icon(1, alpha))));
        Assert.AreEqual("FF060504 00030201", Pixels(Decoded(Icon(1, none))));
    }

    [TestMethod]
    public void AnIconsPngImage_IsAPng_WithNoMask()
    {
        var picture = Decoded(Icon(1, Png(1, 1, 6, 8, [0, 1, 2, 3, 4])));

        Assert.AreEqual("04010203", Pixels(picture));
        Assert.IsNull(picture.Mask);
    }

    // each image of an icon or a cursor, by its directory entry; an entry the file does not list is refused.
    [TestMethod]
    public void AnIconsImages_AreDecodedEachByItsEntry_AndAnotherEntryIsRefused()
    {
        var one = IconDib(1, 1, 24, [], [1, 2, 3, 0], [0, 0, 0, 0]);
        var two = IconDib(2, 1, 24, [], [4, 5, 6, 7, 8, 9, 0, 0], [0, 0, 0, 0]);
        var cursor = Icon(2, one, two);
        Assert.IsTrue(PictureHeader.TryReadIconImages(cursor, out var images));

        Assert.AreEqual(PictureDecodeFailure.None, PictureDecoder.TryDecodeIconImage(cursor, images[1], out var second));
        Assert.AreEqual("FF060504 FF090807", Pixels(second!));
        Assert.AreEqual("FF030201", Pixels(Decoded(cursor)));
        Assert.AreEqual(PictureDecodeFailure.Refused, PictureDecoder.TryDecodeIconImage(cursor, images[1] with { Offset = 1 }, out var none));
        Assert.IsNull(none);
        Assert.AreEqual(PictureDecodeFailure.Refused, PictureDecoder.TryDecodeIconImage(Bitmap2x2, images[0], out _));
    }

    [TestMethod]
    public void AMetafile_IsNotDecoded_AndARefusedPicture_IsRefused()
    {
        Assert.AreEqual(PictureDecodeFailure.NotDecoded, PictureDecoder.TryDecode(Hex("D7CDC69A 0000 0000 0000 A005 D002 A005 00000000 0000"), out var metafile));
        Assert.IsNull(metafile);
        Assert.AreEqual(PictureDecodeFailure.Refused, PictureDecoder.TryDecode(PngHeader(PictureHeader.MaxSide + 1, 1), out _));
        Assert.AreEqual(PictureDecodeFailure.Refused, PictureDecoder.TryDecode(ReadOnlySpan<byte>.Empty, out _));
        Assert.AreEqual(PictureDecodeFailure.NotDecoded, PictureDecoder.TryDecode(Bitmap(1, 1, 2, 0, [0, 0, 0, 0], [0, 0, 0, 0]), out _));
    }

    // every length of each picture cut short, and each with bytes changed: never an exception, and never more pixels than its header states.
    [TestMethod]
    public void AMalformedPicture_NeverThrows()
    {
        var pictures = new[]
        {
            Bitmap2x2, Bitmap(5, 2, 8, 1, new uint[9], Rle8), Png(3, 2, 6, 8, new byte[26]),
            Png(4, 1, 3, 2, [0, 0x19], chunks: [("PLTE", [1, 2, 3, 4, 5, 6])]), Gif(20, 20, [0, 1, 2, 3], new byte[400], 20, 20), Gif1x1,
            Icon(1, IconDib(2, 2, 4, [0, 1], new byte[8], new byte[8])), Icon(1, Png(1, 1, 6, 8, [0, 1, 2, 3, 4])),
        };
        foreach (var picture in pictures)
        {
            foreach (var bytes in Malformed(picture, 15, 300))
            {
                if (PictureDecoder.TryDecode(bytes, out var decoded) == PictureDecodeFailure.None)
                {
                    Assert.IsTrue(PictureHeader.TryRead(bytes, out var info));
                    Assert.AreEqual((long)decoded!.Width * decoded.Height * 4, decoded.Bgra.Length);
                    Assert.IsTrue((long)decoded.Width * decoded.Height <= (long)info.Width * info.Height);
                }
            }
        }
    }
}
