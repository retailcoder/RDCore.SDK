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

    // The length and the first 16 bytes of MS-VBA's SavePicture of these pictures, measured on the same headers: a GIF of 7 by 5 as a bitmap of 8 bits
    // with a table of 256 colors, and an icon's 32 by 32 image as an icon of 16 colors. The PNG, 6 by 4, as a 24-bit bitmap, is RD-VBA's own: MS-VBA does
    // not load a PNG.
    [TestMethod]
    public void AGif_A24BitBitmap_AndAnIcon_AreWrittenInMSVBAsLayout()
    {
        var gif = (byte[])Gif1x1.Clone();
        BinaryPrimitives.WriteInt16LittleEndian(gif.AsSpan(6), 7);
        BinaryPrimitives.WriteInt16LittleEndian(gif.AsSpan(8), 5);
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

    // each pixel as the nearest of the 16 colors; a clear one masked, and black.
    [TestMethod]
    public void AnIcon_IsEachPixelsNearestColor_AndItsMask()
    {
        var png = Decoded(Png(3, 1, 6, 8, [0, 250, 10, 10, 255, 0, 0, 0, 0, 200, 190, 180, 255]));
        var icon = PictureWriter.WriteIcon(png);
        var back = Decoded(icon);

        Assert.AreEqual("9 0 7", string.Join(" ", back.Indexes!.Value.ToArray()));
        Assert.AreEqual("0 1 0", string.Join(" ", back.Mask!.Value.ToArray()));
        CollectionAssert.AreEqual(PictureWriter.IconColors.ToArray(), back.Palette.ToArray());
        Assert.AreEqual(6 + 16 + 40 + 64 + 4 + 4, icon.Length);
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
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PictureWriter.WriteBitmap(png, 32));
        Assert.ThrowsExactly<ArgumentException>(() => PictureWriter.WriteBitmap(png, 8));
        Assert.ThrowsExactly<ArgumentException>(() => PictureWriter.WriteBitmap(four, 1));
        Assert.AreEqual(3, Decoded(PictureWriter.WriteBitmap(four, 4)).Indexes!.Value.Span[0]);
    }
}
