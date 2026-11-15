using RDCore.SDK.Workspace.Pictures;
using System.Buffers.Binary;
using static RDCore.Tests.Workspace.Pictures.TestPictures;

namespace RDCore.Tests.Workspace.Pictures;

/// <summary>
/// A decoded picture written as a bitmap file or an icon file (<see cref="PictureWriter"/>), in the layouts MS-VBA's <c>SavePicture</c> writes.
/// </summary>
[TestClass]
public sealed class PictureWriterTests
{
    private static DecodedPicture Decoded(byte[] bytes)
    {
        Assert.AreEqual(PictureDecodeFailure.None, PictureDecoder.TryDecode(bytes, out var picture));
        return picture!;
    }

    private static string Head(byte[] file) => $"len {file.Length} {Convert.ToHexString(file[..16])}";

    // The length and the first 16 bytes of MS-VBA's SavePicture of these pictures, measured on the same headers: a GIF of 7 by 5 with a table of 256
    // colors as a bitmap of 8 bits, and an icon's 32 by 32 image as an icon of 16 colors. The PNG, 6 by 4, as a 24-bit bitmap, is RD-VBA's own: MS-VBA does
    // not load a PNG.
    [TestMethod]
    public void AGif_A24BitBitmap_AndAnIcon_AreWrittenInMSVBAsLayout()
    {
        var gif = Gif(7, 5, Enumerable.Range(0, 256).Select(i => (uint)i).ToArray(), new byte[35], 7, 5);
        var icon = Icon(1, IconDib(32, 32, 32, [], new byte[32 * 32 * 4], new byte[32 * 4]));
        Assert.IsTrue(PictureHeader.TryReadIconImages(icon, out var images));
        Assert.AreEqual(PictureDecodeFailure.None, PictureDecoder.TryDecodeIconImage(icon, images[0], out var image));

        Assert.AreEqual("len 1118 424D5E04000000000000360400002800", Head(PictureWriter.WriteBitmap(Decoded(gif), 8)));
        Assert.AreEqual("len 134 424D8600000000000000360000002800", Head(PictureWriter.WriteBitmap(Decoded(PngHeader(6, 4)), 24)));
        Assert.AreEqual("len 766 0000010001002020100000000000E802", Head(PictureWriter.WriteIcon(image!)));
    }

    [TestMethod]
    public void A24BitBitmap_IsReadBackAsItsPictures_WithItsAlphaLeftOut()
    {
        var png = Decoded(Png(3, 2, 6, 8, [0, 1, 2, 3, 0, 4, 5, 6, 128, 7, 8, 9, 255, 0, 10, 11, 12, 1, 13, 14, 15, 2, 16, 17, 18, 3]));
        var back = Decoded(PictureWriter.WriteBitmap(png, 24));

        Assert.AreEqual(3, back.Width);
        Assert.AreEqual(2, back.Height);
        var written = back.Bgra.ToArray();
        var source = png.Bgra.ToArray();
        for (var i = 0; i < source.Length; i++)
            Assert.AreEqual(i % 4 == 3 ? (byte)0xFF : source[i], written[i], $"byte {i}");
    }

    // a PNG of 3 by 2 whose alpha is 3, 128, 255, 1, 2 and 3: every byte read back as it was written, its alpha included, under an IHDR of 8 bits per
    // channel and color type 6.
    [TestMethod]
    public void APng_IsReadBackAsItsPictures_WithItsAlpha()
    {
        var png = Decoded(Png(3, 2, 6, 8, [0, 1, 2, 3, 3, 4, 5, 6, 128, 7, 8, 9, 255, 0, 10, 11, 12, 1, 13, 14, 15, 2, 16, 17, 18, 3]));
        var file = PictureWriter.WritePng(png);

        Assert.AreEqual("89504E470D0A1A0A0000000D4948445200000003000000020806000000", Convert.ToHexString(file, 0, 29));
        Assert.AreEqual("0000000049454E44AE426082", Convert.ToHexString(file[^12..]));
        CollectionAssert.AreEqual(png.Bgra.ToArray(), Decoded(file).Bgra.ToArray());
    }

    // the IHDR of a PNG of 3 by 1, and its CRC-32.
    [TestMethod]
    public void APngsChunk_EndsWithItsCrc()
    {
        var file = PictureWriter.WritePng(Decoded(Png(3, 1, 6, 8, [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12])));

        Assert.AreEqual("1BE014B4", Convert.ToHexString(file, 29, 4));
    }

    // an icon of one image stored as that PNG: its entry states 32 bits per pixel, and a side of 256 pixels as 0; read back, it is its pictures again,
    // with no mask.
    [TestMethod]
    public void APngIcon_IsReadBackAsItsPictures_WithItsAlphaAndNoMask()
    {
        var rows = new byte[1 + 256 * 4];
        for (var i = 1; i < rows.Length; i++)
            rows[i] = (byte)i;
        var png = Decoded(Png(256, 1, 6, 8, rows));

        var icon = PictureWriter.WritePngIcon(png);

        Assert.IsTrue(PictureHeader.TryReadIconImages(icon, out var images));
        Assert.AreEqual((256, 1, 32, 22, icon.Length - 22), (images[0].Width, images[0].Height, images[0].BitCount, images[0].Offset, images[0].Length));
        Assert.AreEqual("00010000010020", Convert.ToHexString(icon, 6, 7));
        Assert.AreEqual(PictureDecodeFailure.None, PictureDecoder.TryDecodeIconImage(icon, images[0], out var back));
        CollectionAssert.AreEqual(png.Bgra.ToArray(), back!.Bgra.ToArray());
        Assert.IsNull(back.Mask);
    }

    // a PNG of 3 by 2 whose alpha is 3, 128, 255, 1, 2 and 3, as a 32-bit bitmap: every byte read back as it was written, its alpha included.
    [TestMethod]
    public void A32BitBitmap_IsReadBackAsItsPictures_WithItsAlpha()
    {
        var png = Decoded(Png(3, 2, 6, 8, [0, 1, 2, 3, 3, 4, 5, 6, 128, 7, 8, 9, 255, 0, 10, 11, 12, 1, 13, 14, 15, 2, 16, 17, 18, 3]));
        var file = PictureWriter.WriteBitmap(png, 32);

        Assert.AreEqual(14 + 124 + 3 * 4 * 2, file.Length);
        CollectionAssert.AreEqual(png.Bgra.ToArray(), Decoded(file).Bgra.ToArray());
    }

    // the same PNG as an icon image of 32 bits: its pixels as they are, and no pixel masked, none of them having an alpha of 0.
    [TestMethod]
    public void A32BitIcon_IsReadBackAsItsPictures_WithItsAlpha()
    {
        var png = Decoded(Png(3, 2, 6, 8, [0, 1, 2, 3, 3, 4, 5, 6, 128, 7, 8, 9, 255, 0, 10, 11, 12, 1, 13, 14, 15, 2, 16, 17, 18, 3]));
        var icon = PictureWriter.WriteIcon(png, 32);
        var back = Decoded(icon);

        Assert.AreEqual(6 + 16 + 40 + 3 * 4 * 2 + 4 * 2, icon.Length);
        CollectionAssert.AreEqual(png.Bgra.ToArray(), back.Bgra.ToArray());
        Assert.AreEqual("0 0 0 0 0 0", string.Join(" ", back.Mask!.Value.ToArray()));
    }

    // each of a cursor's two images, a DIB and a PNG, as a cursor of that one image: its directory entry, its hot spot included, and its bytes as they are.
    [TestMethod]
    [DataRow(0, DisplayName = "the DIB")]
    [DataRow(1, DisplayName = "the PNG")]
    public void AnIconImage_IsWrittenAsAFileOfThatImage_AsItIs(int which)
    {
        var dib = IconDib(2, 1, 24, [], [255, 255, 255, 0, 0, 0, 0, 0], [0x40, 0, 0, 0]);
        var cursor = Icon(2, dib, Png(3, 1, 6, 8, [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]));
        BinaryPrimitives.WriteUInt32LittleEndian(cursor.AsSpan(6 + 16 * which + 4), 0x00070005);
        cursor[6 + 16 * which] = (byte)(which == 0 ? 2 : 3);
        Assert.IsTrue(PictureHeader.TryReadIconImages(cursor, out var images));
        var image = images[which];

        var file = PictureWriter.WriteIconImage(cursor, image);

        Assert.AreEqual("000002000100", Convert.ToHexString(file, 0, 6));
        Assert.AreEqual(Convert.ToHexString(cursor, 6 + 16 * which, 8), Convert.ToHexString(file, 6, 8));
        Assert.AreEqual((image.Length, 22), (BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(14)), BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(18))));
        Assert.AreEqual(Convert.ToHexString(cursor, image.Offset, image.Length), Convert.ToHexString(file, 22, file.Length - 22));
        Assert.IsTrue(PictureHeader.TryReadIconImages(file, out var again));
        Assert.AreEqual(image with { Offset = 22 }, again.Single());
    }

    [TestMethod]
    public void AnIconImage_OfAFileThatIsNoIcon_OrNotOfTheFile_IsRefused()
    {
        var icon = Icon(1, IconDib(2, 1, 24, [], new byte[8], new byte[4]));
        Assert.IsTrue(PictureHeader.TryReadIconImages(icon, out var images));

        Assert.ThrowsExactly<ArgumentException>(() => PictureWriter.WriteIconImage(Png(1, 1, 6, 8, [0, 1, 2, 3, 4]), images[0]));
        Assert.ThrowsExactly<ArgumentException>(() => PictureWriter.WriteIconImage(icon, images[0] with { Offset = 23 }));
    }

    // the numbers as they are, and the table filled to the depth's colors with black.
    [TestMethod]
    [DataRow(8, 256)]
    [DataRow(4, 16)]
    [DataRow(1, 2)]
    public void AnIndexedBitmap_KeepsItsNumbers_AndItsTableFilledWithBlack(int bits, int colors)
    {
        var gif = Decoded(Gif(3, 2, [0x102030, 0x405060], [1, 0, 1, 0, 0, 1], 3, 2));
        var file = PictureWriter.WriteBitmap(gif, bits);
        var back = Decoded(file);

        Assert.AreEqual("1 0 1 0 0 1", string.Join(" ", back.Indexes!.Value.ToArray()));
        Assert.AreEqual(colors, back.Palette.Length);
        Assert.AreEqual(0xFF102030u, back.Palette[0]);
        Assert.AreEqual(0xFF405060u, back.Palette[1]);
        Assert.IsTrue(back.Palette.Skip(2).All(entry => entry == 0xFF000000));
        Assert.AreEqual(14 + 40 + colors * 4 + (int)((3L * bits + 31) / 32 * 4) * 2, file.Length);
    }

    // each pixel as the nearest of the 16 colors, a clear one masked and its color kept.
    [TestMethod]
    public void AnIcon_IsEachPixelsNearestColor_AndItsMask()
    {
        var png = Decoded(Png(3, 1, 6, 8, [0, 250, 10, 10, 255, 0, 0, 255, 0, 200, 190, 180, 255]));
        var icon = PictureWriter.WriteIcon(png);
        var back = Decoded(icon);

        Assert.AreEqual("9 12 8", string.Join(" ", back.Indexes!.Value.ToArray()));
        Assert.AreEqual("0 1 0", string.Join(" ", back.Mask!.Value.ToArray()));
        CollectionAssert.AreEqual(PictureWriter.IconColors.ToArray(), back.Palette.ToArray());
        Assert.AreEqual(6 + 16 + 40 + 64 + 4 + 4, icon.Length);
    }

    // a table of 2 of the standard colors: filled with the 14 it lacks at 4 bits, which fill it exactly, and with black at 8 bits.
    [TestMethod]
    public void AnIndexedBitmapsTableOfStandardColors_IsFilledWithTheStandardColorsItLacks_WhereTheyFillItExactly()
    {
        var gif = Decoded(Gif(3, 2, [0xFF0000, 0x00FF00], [1, 0, 1, 0, 0, 1], 3, 2));

        var four = Decoded(PictureWriter.WriteBitmap(gif, 4)).Palette;
        var eight = Decoded(PictureWriter.WriteBitmap(gif, 8)).Palette;

        CollectionAssert.AreEqual(new[] { 0xFFFF0000u, 0xFF00FF00u }.Concat(PictureWriter.StandardColors.Where(color => color is not (0xFFFF0000 or 0xFF00FF00))).ToArray(),
            four.ToArray());
        Assert.IsTrue(eight.Skip(2).All(entry => entry == 0xFF000000));
    }

    [TestMethod]
    public void AnIcon_KeepsTheMaskOfTheImageItIsWrittenFrom()
    {
        var dib = IconDib(2, 1, 24, [], [255, 255, 255, 0, 0, 0, 0, 0], [0x40, 0, 0, 0]);
        var back = Decoded(PictureWriter.WriteIcon(Decoded(Icon(1, dib))));

        Assert.AreEqual("15 0", string.Join(" ", back.Indexes!.Value.ToArray()));
        Assert.AreEqual("0 1", string.Join(" ", back.Mask!.Value.ToArray()));
    }

    [TestMethod]
    public void ABitmap_OfADepthItIsNotWrittenAt_OrOfNoNumbers_IsRefused()
    {
        var png = Decoded(Png(1, 1, 6, 8, [0, 1, 2, 3, 4]));
        var four = Decoded(Png(1, 1, 3, 8, [0, 3], chunks: [("PLTE", new byte[12])]));

        Assert.ThrowsExactly<ArgumentNullException>(() => PictureWriter.WriteBitmap(null!, 24));
        Assert.ThrowsExactly<ArgumentNullException>(() => PictureWriter.WriteIcon(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => PictureWriter.WritePng(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => PictureWriter.WritePngIcon(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => PictureWriter.WriteIcon(null!, 32));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PictureWriter.WriteBitmap(png, 16));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PictureWriter.WriteIcon(png, 24));
        Assert.ThrowsExactly<ArgumentException>(() => PictureWriter.WriteBitmap(png, 8));
        Assert.ThrowsExactly<ArgumentException>(() => PictureWriter.WriteBitmap(four, 1));
        Assert.AreEqual(3, Decoded(PictureWriter.WriteBitmap(four, 4)).Indexes!.Value.Span[0]);
    }
}
