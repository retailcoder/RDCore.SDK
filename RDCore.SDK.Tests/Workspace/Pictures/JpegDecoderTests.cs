using RDCore.SDK.Workspace.Pictures;
using System.Buffers.Binary;
using static RDCore.Tests.Workspace.Pictures.TestJpegs;
using static RDCore.Tests.Workspace.Pictures.TestPictures;

namespace RDCore.Tests.Workspace.Pictures;

/// <summary>
/// A JPEG's pixels (<see cref="PictureDecoder"/>): baseline and progressive Huffman coding, gray and color, chroma upsampling, restart intervals, bodies cut
/// short, and the kinds of JPEG it does not decode.
/// </summary>
[TestClass]
public sealed class JpegDecoderTests
{
    private static DecodedPicture Decoded(byte[] bytes)
    {
        Assert.AreEqual(PictureDecodeFailure.None, PictureDecoder.TryDecode(bytes, out var picture));
        return picture!;
    }

    private static uint Argb(DecodedPicture picture, int x, int y)
    {
        var at = (y * picture.Width + x) * 4;
        var bgra = picture.Bgra.Span;
        return (uint)(bgra[at + 3] << 24 | bgra[at + 2] << 16 | bgra[at + 1] << 8 | bgra[at]);
    }

    private static Plane[] Color(int width, int height, int h, int v, int seed)
    {
        var (cw, ch) = SizeOf(width, height, 1, 1, h, v);
        return [new Plane(h, v, Pattern(width, height, seed)), new Plane(1, 1, Pattern(cw, ch, seed + 1)), new Plane(1, 1, Pattern(cw, ch, seed + 2))];
    }

    // a gradient of 13 by 10, at a quantizer of 1: each pixel within 2 of its sample, in every channel, and opaque.
    [TestMethod]
    public void AGrayJpeg_IsItsSamples_AsGrayAndOpaque()
    {
        var samples = new byte[13 * 10];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (byte)(40 + 3 * (i % 13) + 2 * (i / 13));

        var picture = Decoded(Jpeg(13, 10, [new Plane(1, 1, samples)]));

        Assert.AreEqual((13, 10, true), (picture.Width, picture.Height, picture.IsComplete));
        Assert.IsNull(picture.Indexes);
        Assert.AreEqual(0, picture.Palette.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            var argb = Argb(picture, i % 13, i / 13);
            Assert.AreEqual(0xFFu, argb >> 24);
            Assert.AreEqual(argb & 0xFF, argb >> 8 & 0xFF);
            Assert.AreEqual(argb & 0xFF, argb >> 16 & 0xFF);
            Assert.IsTrue(Math.Abs((int)(argb & 0xFF) - samples[i]) <= 2, $"pixel {i}: {argb & 0xFF} for {samples[i]}");
        }
    }

    // flat components, whose blocks decode exactly: YCbCr 100, 128, 200 is RGB 201, 49, 100; an Adobe segment of transform 0, or components named R, G and
    // B, are RGB as they are.
    [TestMethod]
    public void AColorJpeg_IsYCbCr_OrRgbWhereItsAdobeSegmentOrItsComponentsSaySo()
    {
        Plane[] Planes(byte a, byte b, byte c) => [new Plane(1, 1, Flat(16, 8, a)), new Plane(1, 1, Flat(16, 8, b)), new Plane(1, 1, Flat(16, 8, c))];

        var ycbcr = Decoded(Jpeg(16, 8, Planes(100, 128, 200)));
        var adobe = Decoded(Jpeg(16, 8, Planes(200, 100, 50), new Options { Adobe = 0 }));
        var named = Decoded(Jpeg(16, 8, Planes(200, 100, 50), new Options { Ids = ['R', 'G', 'B'] }));
        var adobeYcbcr = Decoded(Jpeg(16, 8, Planes(100, 128, 200), new Options { Adobe = 1, Ids = ['R', 'G', 'B'] }));

        Assert.AreEqual(0xFFC93164u, Argb(ycbcr, 15, 7));
        Assert.AreEqual(0xFFC86432u, Argb(adobe, 0, 0));
        Assert.AreEqual(0xFFC86432u, Argb(named, 9, 3));
        Assert.AreEqual(0xFFC93164u, Argb(adobeYcbcr, 4, 4));
    }

    // 32 by 16, gray but for a step in Cr from 128 to 208 at the chroma's 16th column (the 8th at half the width): the triangle filter puts two values
    // between them, which red shows; at the full width, the step is sharp. Each of the 8 luma blocks has its own level, so the blocks of an MCU are in order.
    [TestMethod]
    [DataRow(2, 2, DisplayName = "4:2:0")]
    [DataRow(2, 1, DisplayName = "4:2:2")]
    [DataRow(1, 2, DisplayName = "4:4:0, repeated")]
    public void ChromaAtHalfTheWidth_IsUpsampledByTheTriangleFilter_AndOtherwiseRepeated(int h, int v)
    {
        var luma = new byte[32 * 16];
        for (var i = 0; i < luma.Length; i++)
            luma[i] = (byte)(16 + 24 * (i % 32 / 8 + 4 * (i / 32 / 8)));
        var (cw, ch) = SizeOf(32, 16, 1, 1, h, v);
        var cr = new byte[cw * ch];
        for (var i = 0; i < cr.Length; i++)
            cr[i] = (byte)(i % cw < cw / 2 ? 128 : 208);
        var wholeLuma = Enumerable.Range(0, 32 * 16).Select(i => (byte)128).ToArray();

        var levels = Decoded(Jpeg(32, 16, [new Plane(h, v, luma), new Plane(1, 1, Flat(cw, ch, 128)), new Plane(1, 1, Flat(cw, ch, 128))]));
        var step = Decoded(Jpeg(32, 16, [new Plane(h, v, wholeLuma), new Plane(1, 1, Flat(cw, ch, 128)), new Plane(1, 1, cr)]));

        for (var block = 0; block < 8; block++)
        {
            var level = (uint)(16 + 24 * block);
            Assert.AreEqual(0xFF000000u | level << 16 | level << 8 | level, Argb(levels, block % 4 * 8 + 3, block / 4 * 8 + 5), $"block {block}");
        }

        var reds = string.Join(" ", Enumerable.Range(14, 4).Select(x => Argb(step, x, 9) >> 16 & 0xFF));
        Assert.AreEqual(h == 2 ? "128 156 212 240" : "128 128 240 240", reds);
    }

    // the same coefficients in a sequential and in a progressive picture, through DC and AC bands of successive approximation: the same pixels.
    [TestMethod]
    [DataRow(1, DisplayName = "gray")]
    [DataRow(3, DisplayName = "4:2:0 color")]
    public void AProgressiveJpeg_IsTheSameAsItsSequentialJpeg(int components)
    {
        var planes = components == 1 ? [new Plane(1, 1, Pattern(37, 29, 4))] : Color(37, 29, 2, 2, 4);
        var sequential = Decoded(Jpeg(37, 29, planes, new Options { Quant = 3 }));
        var progressive = Decoded(Jpeg(37, 29, planes, new Options { Quant = 3, Progressive = true }));

        Assert.IsTrue(progressive.IsComplete);
        CollectionAssert.AreEqual(sequential.Bgra.ToArray(), progressive.Bgra.ToArray());
    }

    // restart markers every 1, 2 and 5 MCUs, and one scan for each component, give the pixels of one scan with none.
    [TestMethod]
    public void RestartIntervals_AndAScanForEachComponent_GiveTheSamePixels()
    {
        var planes = Color(33, 27, 2, 2, 9);
        var plain = Decoded(Jpeg(33, 27, planes, new Options { Quant = 2 })).Bgra.ToArray();
        var codings = new[]
        {
            new Options { Quant = 2, Restart = 1 }, new Options { Quant = 2, Restart = 5 }, new Options { Quant = 2, Interleaved = false, Restart = 2 },
            new Options { Quant = 2, Interleaved = false }, new Options { Quant = 2, Progressive = true, Restart = 2 },
        };

        foreach (var coding in codings)
        {
            var picture = Decoded(Jpeg(33, 27, planes, coding));
            Assert.IsTrue(picture.IsComplete, coding.ToString());
            CollectionAssert.AreEqual(plain, picture.Bgra.ToArray(), coding.ToString());
        }
    }

    // its body cut in its last blocks: the first row of blocks is that of the whole picture, and the last block is 0; its end-of-image marker missing; its
    // header alone.
    [TestMethod]
    public void AJpegCutShort_IsDecodedAsFarAsItGoes()
    {
        var whole = Jpeg(16, 32, [new Plane(1, 1, Pattern(16, 32, 1))]);
        var full = Decoded(whole);
        var cut = Decoded(whole[..^60]);
        var noEnd = Decoded(whole[..^2]);
        var header = Decoded(JpegHeader(9, 11)[..^16]);

        Assert.IsFalse(cut.IsComplete);
        CollectionAssert.AreEqual(full.Bgra.Slice(0, 16 * 8 * 4).ToArray(), cut.Bgra.Slice(0, 16 * 8 * 4).ToArray());
        Assert.IsTrue(cut.Bgra.Slice((16 * 31 + 8) * 4).ToArray().All(b => b == 0));
        Assert.IsFalse(noEnd.IsComplete);
        CollectionAssert.AreEqual(full.Bgra.ToArray(), noEnd.Bgra.ToArray());
        Assert.AreEqual((9, 11, false), (header.Width, header.Height, header.IsComplete));
        Assert.IsTrue(header.Bgra.ToArray().All(b => b == 0));
    }

    // a progressive picture cut after its first scan, of every block's DC: every pixel is there, at the DC's coarser values.
    [TestMethod]
    public void AProgressiveJpegCutAfterItsFirstScan_HasEveryPixel_NotComplete()
    {
        var whole = Jpeg(24, 16, Color(24, 16, 2, 2, 3), new Options { Progressive = true });
        var second = IndexOf(whole, [0xFF, 0xDA], IndexOf(whole, [0xFF, 0xDA], 0) + 2);

        var cut = Decoded(whole[..second]);

        Assert.IsFalse(cut.IsComplete);
        Assert.IsTrue(Enumerable.Range(0, 24 * 16).All(i => cut.Bgra.Span[i * 4 + 3] == 0xFF));
        CollectionAssert.AreNotEqual(Decoded(whole).Bgra.ToArray(), cut.Bgra.ToArray());
    }

    private static int IndexOf(byte[] bytes, byte[] value, int from)
    {
        for (var i = from; i + value.Length <= bytes.Length; i++)
            if (bytes.AsSpan(i, value.Length).SequenceEqual(value))
                return i;
        return -1;
    }

    // a picture of 16 by 16 whose frame states 16384 by 1024: its data gives the first blocks only, and the rest of the picture is 0. A file of 300 scans is
    // decoded to its 256th.
    [TestMethod]
    public void AJpegThatStatesMoreThanItsDataGives_IsDecodedToItsData()
    {
        var small = Jpeg(16, 16, [new Plane(1, 1, Pattern(16, 16, 5))]);
        var sof = IndexOf(small, [0xFF, 0xC0], 0);
        BinaryPrimitives.WriteUInt16BigEndian(small.AsSpan(sof + 5), 1024);
        BinaryPrimitives.WriteUInt16BigEndian(small.AsSpan(sof + 7), 16384);
        var progressive = Jpeg(16, 16, [new Plane(1, 1, Pattern(16, 16, 5))], new Options { Progressive = true });
        var last = IndexOf(progressive, [0xFF, 0xDA], IndexOf(progressive, [0xFF, 0xDA], 0) + 2);
        var scans = progressive[..last].Concat(Enumerable.Repeat(progressive[last..^2], 300).SelectMany(scan => scan)).Concat(progressive[^2..]).ToArray();

        var large = Decoded(small);
        var many = Decoded(scans);

        Assert.AreEqual((16384, 1024, false), (large.Width, large.Height, large.IsComplete));
        Assert.AreEqual(0xFFu, Argb(large, 0, 0) >> 24);
        Assert.AreEqual(0u, Argb(large, 16383, 1023));
        Assert.IsFalse(many.IsComplete);
    }

    // the kinds of JPEG the decoder does not take, from a header the header reader accepts: lossless, arithmetic-coded, 12-bit samples, 4 components, and
    // sampling factors that do not divide the largest; and a sampling factor of 0, which is refused.
    [TestMethod]
    [DataRow(21, 0xC3, PictureDecodeFailure.NotDecoded, DisplayName = "lossless")]
    [DataRow(21, 0xC9, PictureDecodeFailure.NotDecoded, DisplayName = "arithmetic coding")]
    [DataRow(24, 12, PictureDecodeFailure.NotDecoded, DisplayName = "12-bit samples")]
    [DataRow(34, 0x31, PictureDecodeFailure.NotDecoded, DisplayName = "3 to 2 sampling")]
    [DataRow(31, 0x02, PictureDecodeFailure.Refused, DisplayName = "a sampling factor of 0")]
    public void AJpegOfAKindNotDecoded_IsNotDecoded(int at, int value, PictureDecodeFailure expected)
    {
        var bytes = JpegHeader(9, 11);
        bytes[at] = (byte)value;
        if (at == 34)
            bytes[31] = 0x21;

        Assert.AreEqual(expected, PictureDecoder.TryDecode(bytes, out var picture));
        Assert.IsNull(picture);
        Assert.AreEqual(PictureDecodeFailure.NotDecoded, PictureDecoder.TryDecode(Hex("FFD8 FFC0 0014 08 000B 0009 04 011100 021100 031100 041100"), out _));
    }

    // every length of a progressive color picture, and the picture with bytes changed: never an exception.
    [TestMethod]
    public void AMalformedJpeg_NeverThrows()
    {
        var pictures = new[]
        {
            Jpeg(19, 13, Color(19, 13, 2, 2, 7), new Options { Progressive = true, Restart = 2, Quant = 4 }),
            Jpeg(19, 13, Color(19, 13, 2, 1, 8), new Options { Restart = 3, Quant = 4 }),
        };
        foreach (var picture in pictures)
        {
            foreach (var bytes in Malformed(picture, 23, 400))
            {
                if (PictureDecoder.TryDecode(bytes, out var decoded) == PictureDecodeFailure.None)
                    Assert.AreEqual((long)decoded!.Width * decoded.Height * 4, decoded.Bgra.Length);
            }
        }
    }
}
