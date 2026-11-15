using System.Collections.Immutable;

namespace RDCore.SDK.Workspace.Pictures;

/// <summary>
/// Why <see cref="PictureDecoder"/> gave no pixels for a picture.
/// </summary>
public enum PictureDecodeFailure
{
    /// <summary>
    /// The picture was decoded.
    /// </summary>
    None,

    /// <summary>
    /// <see cref="PictureHeader.TryRead"/> refuses the picture, or its header holds a value no decoder takes, or the icon image named is not one of the file's:
    /// what VBA reports as run-time error 481, "Invalid picture".
    /// </summary>
    Refused,

    /// <summary>
    /// The picture is accepted, and is of a format or a kind the decoder does not decode: a metafile, an enhanced metafile, a bitmap of a depth or a
    /// compression it does not take, such as 2 bits per pixel or pixels that are JPEG or PNG data, or a JPEG that is lossless, hierarchical or
    /// arithmetic-coded, of 12-bit samples, or of 2 or 4 components.
    /// </summary>
    NotDecoded,
}

/// <summary>
/// Decodes a picture's pixels: a bitmap, a PNG, a GIF, a JPEG, or an image of an icon or a cursor.
/// </summary>
/// <remarks>
/// <para>
/// A picture is decoded only once <see cref="PictureHeader.TryRead"/> accepts it, so its pixels are never more than <see cref="PictureHeader.MaxPixels"/>,
/// and the decoder reads no more of a compressed body than those pixels need: a body that would grow past them is cut off there. Every offset is checked
/// against the bytes, and nothing is thrown for any bytes.
/// </para>
/// <para>
/// A body cut short, or one that cannot be read to its end, is decoded as far as it goes, and the pixels it does not give are 0
/// (<see cref="DecodedPicture.IsComplete"/>).
/// </para>
/// <list type="bullet">
/// <item>A bitmap: 1, 4, 8, 16, 24 or 32 bits per pixel; uncompressed, RLE8 or RLE4, or with bit fields. Its color table is kept for 8 bits per pixel or
/// fewer. A bitmap file's fourth byte of a 32-bit pixel is not alpha, so the picture is opaque, unless bit fields give an alpha mask.</item>
/// <item>A PNG: every color type and bit depth, interlaced or not, with its <c>tRNS</c> transparency. Its checksums are not checked. A sample of 16 bits
/// keeps its high byte.</item>
/// <item>A GIF: its first frame, on its logical screen, with its local color table or else its global one, and the transparent color of the frame's graphic
/// control extension. An animation's later frames are not decoded.</item>
/// <item>A JPEG (ITU-T T.81): Huffman-coded, baseline, extended or progressive, of 8-bit samples, gray or YCbCr (RGB where an Adobe <c>APP14</c> segment
/// says so), with restart intervals. Its chroma is upsampled as the IJG decoder does by default: by its triangle filter at half the width, or half the
/// width and height; by repeating each sample at any other ratio. The picture is opaque. A file of more than 256 scans is decoded to its 256th.</item>
/// <item>An icon's or a cursor's image: a PNG, or a DIB and its AND mask. A DIB image is clear where its 32-bit alpha says so, or, where it has no alpha or
/// every pixel's is 0, where its mask is set.</item>
/// </list>
/// </remarks>
public static class PictureDecoder
{
    /// <summary>
    /// Decodes a bitmap, a PNG, a GIF or a JPEG, or the first image of an icon or a cursor.
    /// </summary>
    /// <param name="bytes">The picture, as its file holds it.</param>
    /// <param name="picture">The pixels where the picture is decoded; <see langword="null"/> otherwise.</param>
    /// <returns><see cref="PictureDecodeFailure.None"/> where the picture is decoded; otherwise why it is not.</returns>
    public static PictureDecodeFailure TryDecode(ReadOnlySpan<byte> bytes, out DecodedPicture? picture)
    {
        picture = null;
        if (!PictureHeader.TryRead(bytes, out var info))
        {
            return PictureDecodeFailure.Refused;
        }

        switch (info.Format)
        {
            case PictureFormat.Icon or PictureFormat.Cursor:
                return PictureHeader.TryReadIconImages(bytes, out var images)
                    ? TryDecodeIconImage(bytes, images[0], out picture)
                    : PictureDecodeFailure.Refused;
            case PictureFormat.Bitmap:
                return Done(BitmapDecoder.File(bytes, out var failure), failure, out picture);
            case PictureFormat.Png:
                return Done(PngDecoder.Decode(bytes, info.Width, info.Height, out failure), failure, out picture);
            case PictureFormat.Gif:
                return Done(GifDecoder.Decode(bytes, info.Width, info.Height), PictureDecodeFailure.None, out picture);
            case PictureFormat.Jpeg:
                return Done(JpegDecoder.Decode(bytes, info.Width, info.Height, out failure), failure, out picture);
            default:
                return PictureDecodeFailure.NotDecoded;
        }
    }

    /// <summary>
    /// Decodes one of the images of an icon or a cursor, such as the one a <c>StdPicture</c> holds.
    /// </summary>
    /// <param name="bytes">The icon or the cursor, as its file holds it.</param>
    /// <param name="image">The image: one of those <see cref="PictureHeader.TryReadIconImages"/> reads from <paramref name="bytes"/>.</param>
    /// <param name="picture">The image's pixels, and its mask where it is a DIB, where it is decoded; <see langword="null"/> otherwise.</param>
    /// <returns>
    /// <see cref="PictureDecodeFailure.None"/> where the image is decoded; <see cref="PictureDecodeFailure.Refused"/> where the picture is not an icon or a
    /// cursor that <see cref="PictureHeader.TryReadIconImages"/> accepts, or <paramref name="image"/> is not one of its images; otherwise why it is not.
    /// </returns>
    public static PictureDecodeFailure TryDecodeIconImage(ReadOnlySpan<byte> bytes, IconImage image, out DecodedPicture? picture)
    {
        picture = null;
        if (!PictureHeader.TryReadIconImages(bytes, out var images) || !images.Contains(image))
        {
            return PictureDecodeFailure.Refused;
        }

        var stored = bytes.Slice(image.Offset, image.Length);
        if (PngDecoder.IsPng(stored))
        {
            return Done(PngDecoder.Decode(stored, image.Width, image.Height, out var pngFailure), pngFailure, out picture);
        }

        return Done(BitmapDecoder.Dib(stored, 0, -1, icon: true, out var failure), failure, out picture);
    }

    private static PictureDecodeFailure Done(DecodedPicture? decoded, PictureDecodeFailure failure, out DecodedPicture? picture)
    {
        picture = decoded;
        return decoded is null ? failure is PictureDecodeFailure.None ? PictureDecodeFailure.Refused : failure : PictureDecodeFailure.None;
    }

    // a color as a palette entry holds it.
    internal static uint Argb(byte alpha, byte red, byte green, byte blue) => (uint)alpha << 24 | (uint)red << 16 | (uint)green << 8 | blue;

    // a palette entry written as 4 bytes of BGRA.
    internal static void Put(Span<byte> bgra, int pixel, uint argb)
    {
        var at = pixel * 4;
        bgra[at] = (byte)argb;
        bgra[at + 1] = (byte)(argb >> 8);
        bgra[at + 2] = (byte)(argb >> 16);
        bgra[at + 3] = (byte)(argb >> 24);
    }

    // the colors of indexed pixels: the palette's entries, and an opaque black for a number past its end.
    internal static byte[] Colors(ReadOnlySpan<byte> indexes, ImmutableArray<uint> palette)
    {
        var bgra = new byte[indexes.Length * 4];
        for (var i = 0; i < indexes.Length; i++)
        {
            Put(bgra, i, indexes[i] < palette.Length ? palette[indexes[i]] : 0xFF000000);
        }

        return bgra;
    }
}
