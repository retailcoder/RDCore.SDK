using RDCore.SDK.Workspace.Pictures;
using static RDCore.Tests.Workspace.Pictures.TestPictures;

namespace RDCore.Tests.Workspace.Pictures;

/// <summary>
/// A picture's header, read before anything decodes the picture (<see cref="PictureHeader.TryRead"/>): its format and size, or a refusal, and never an
/// exception.
/// </summary>
[TestClass]
public sealed class PictureHeaderTests
{
    private static PictureInfo Accepted(byte[] bytes)
    {
        Assert.IsTrue(PictureHeader.TryRead(bytes, out var info));
        return info;
    }

    private static void Refused(byte[]? bytes)
    {
        Assert.IsFalse(PictureHeader.TryRead(bytes, out var info));
        Assert.AreEqual(default, info);
    }

    [TestMethod]
    public void EachFormatsHeader_GivesItsFormatAndSize()
    {
        var png = PngHeader(3, 2);

        Assert.AreEqual(new PictureInfo(PictureFormat.Bitmap, 2, 2), Accepted(Bitmap2x2));
        Assert.AreEqual(new PictureInfo(PictureFormat.Gif, 1, 1), Accepted(Gif1x1));
        Assert.AreEqual(new PictureInfo(PictureFormat.Png, 3, 2), Accepted(png));
        Assert.AreEqual(new PictureInfo(PictureFormat.Jpeg, 640, 480), Accepted(JpegHeader(640, 480)));
        Assert.AreEqual(new PictureInfo(PictureFormat.Icon, 3, 2), Accepted(Icon(1, png)));
        Assert.AreEqual(new PictureInfo(PictureFormat.Cursor, 3, 2), Accepted(Icon(2, png)));
    }

    [TestMethod]
    public void ABitmapsHeight_IsItsSizeWhenTopDown_AndAnIconsDibHoldsItsMaskBelowIt()
    {
        var dib = Bitmap2x2[14..];

        Assert.AreEqual(new PictureInfo(PictureFormat.Bitmap, 32, 16), Accepted(BitmapHeader(32, -16)));

        // the DIB of a 2x2 bitmap read as an icon's is 2 across and 1 down: half its height is the mask.
        Assert.AreEqual(new PictureInfo(PictureFormat.Icon, 2, 1), Accepted(Icon(1, dib)));
    }

    [TestMethod]
    public void AnIcon_IsTheSizeOfItsLargestImage_AndRefusedWhereAnyImageIsTooLarge()
    {
        Assert.AreEqual(new PictureInfo(PictureFormat.Icon, 48, 48), Accepted(Icon(1, PngHeader(16, 16), PngHeader(48, 48), PngHeader(32, 32))));
        Refused(Icon(1, PngHeader(16, 16), PngHeader(70000, 70000)));
    }

    [TestMethod]
    public void AMetafile_IsRecognized_AndHasNoPixelSize()
    {
        var placeable = Accepted(Hex("D7CDC69A 0000 00000000 64006400 6009 00000000 0000"));
        var plain = Accepted(Hex("0100 0900 0003 00000000 0000 00000000 0000"));
        var enhanced = new byte[88];
        enhanced[0] = 1;
        " EMF"u8.CopyTo(enhanced.AsSpan(40));

        Assert.AreEqual((PictureFormat.Metafile, false), (placeable.Format, placeable.HasPixelSize));
        Assert.AreEqual(PictureFormat.Metafile, plain.Format);
        Assert.AreEqual((PictureFormat.EnhancedMetafile, false), (Accepted(enhanced).Format, Accepted(enhanced).HasPixelSize));
    }

    [TestMethod]
    [DataRow("", DisplayName = "no bytes")]
    [DataRow("42", DisplayName = "one byte")]
    [DataRow("424D", DisplayName = "a bitmap's magic alone")]
    [DataRow("424D 00000000 00000000 FF000000 28000000", DisplayName = "a bitmap whose pixels start past its end")]
    [DataRow("424D 00000000 00000000 10000000 00000000 0000", DisplayName = "a bitmap with a DIB header of no size")]
    [DataRow("424D 00000000 00000000 10000000 28000000 0000", DisplayName = "a bitmap whose DIB header is longer than the file")]
    [DataRow("000001000000", DisplayName = "an icon of no images")]
    [DataRow("00000100E803", DisplayName = "an icon of a thousand images in six bytes")]
    [DataRow("000001000100 0000000000000000 10000000 FFFFFF7F", DisplayName = "an icon image past the end")]
    [DataRow("000001000100 0000000000000000 FFFFFFFF 16000000", DisplayName = "an icon image of 4 GiB")]
    [DataRow("000001000100 0000000000000000 00000000 16000000", DisplayName = "an icon image of no bytes")]
    [DataRow("474946383961 0100", DisplayName = "a GIF cut short")]
    [DataRow("474946383961 0000 0100", DisplayName = "a GIF of no width")]
    [DataRow("FFD8", DisplayName = "a JPEG's start alone")]
    [DataRow("FFD8 FFE0 0000", DisplayName = "a JPEG segment of length 0")]
    [DataRow("FFD8 FFE0 0001 00", DisplayName = "a JPEG segment of length 1")]
    [DataRow("FFD8 FFE0 FFFF", DisplayName = "a JPEG segment past the end")]
    [DataRow("FFD8 FFDA 0002", DisplayName = "a JPEG scan before any frame")]
    [DataRow("FFD8 FFD9", DisplayName = "a JPEG that ends before any frame")]
    [DataRow("FFD8 00", DisplayName = "a JPEG with no marker")]
    [DataRow("FFD8 FFC0 0004 0800", DisplayName = "a JPEG frame too short for its size")]
    [DataRow("89504E470D0A1A0A 0000000D 49484452 00000001", DisplayName = "a PNG cut short")]
    [DataRow("89504E470D0A1A0A 0000000D 49444154 00000001 00000001", DisplayName = "a PNG whose first chunk is not IHDR")]
    [DataRow("89504E470D0A1A0A 0000000D 49484452 80000000 00000001", DisplayName = "a PNG of a negative width")]
    [DataRow("0100 0900 0002", DisplayName = "a metafile of an unknown version")]
    [DataRow("3C737667", DisplayName = "an SVG")]
    [DataRow("4D5A9000", DisplayName = "a program")]
    public void AMalformedPicture_IsRefused(string hex) => Refused(Hex(hex));

    [TestMethod]
    public void NoPicture_IsRefused() => Refused(null);

    [TestMethod]
    public void APictureTooLarge_IsRefusedBeforeItsHeaderIsRead()
    {
        var bytes = new byte[PictureHeader.MaxBytes + 1];
        Bitmap2x2.CopyTo(bytes, 0);

        Refused(bytes);
    }

    [TestMethod]
    [DataRow(PictureHeader.MaxSide + 1, 1, DisplayName = "too wide")]
    [DataRow(1, PictureHeader.MaxSide + 1, DisplayName = "too tall")]
    [DataRow(4097, 4096, DisplayName = "too many pixels")]
    [DataRow(0, 1, DisplayName = "of no width")]
    [DataRow(int.MaxValue, 1, DisplayName = "of the widest width")]
    public void APictureOfASizePastTheBounds_IsRefused_WhateverItsFormat(int width, int height)
    {
        Refused(BitmapHeader(width, height));
        Refused(PngHeader(width, height));
        Refused(Icon(1, PngHeader(width, height)));
        if (width <= ushort.MaxValue && height <= ushort.MaxValue)
        {
            Refused(JpegHeader(width, height));
        }
    }

    [TestMethod]
    public void APictureAtTheBounds_IsAccepted()
    {
        Assert.AreEqual(new PictureInfo(PictureFormat.Png, 4096, 4096), Accepted(PngHeader(4096, 4096)));
        Assert.AreEqual(new PictureInfo(PictureFormat.Png, PictureHeader.MaxSide, 1024), Accepted(PngHeader(PictureHeader.MaxSide, 1024)));
    }

    [TestMethod]
    public void EveryPictureCutShortOrChanged_IsAcceptedWithinTheBounds_OrRefused_AndNeverThrows()
    {
        var pictures = new[]
        {
            Bitmap2x2, Gif1x1, PngHeader(3, 2), JpegHeader(640, 480), Icon(1, PngHeader(16, 16), Bitmap2x2[14..]), Icon(2, PngHeader(32, 32)),
        };
        var read = 0;

        foreach (var (picture, seed) in pictures.Select((p, i) => (p, i)))
        {
            foreach (var bytes in Malformed(picture, seed, 500))
            {
                if (PictureHeader.TryRead(bytes, out var info) && info.HasPixelSize)
                {
                    Assert.IsTrue(info.Width is > 0 and <= PictureHeader.MaxSide && info.Height is > 0 and <= PictureHeader.MaxSide);
                    Assert.IsLessThanOrEqualTo((long)PictureHeader.MaxPixels, (long)info.Width * info.Height);
                }

                read++;
            }
        }

        Assert.IsGreaterThan(3000, read);
    }
}
