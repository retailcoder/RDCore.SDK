using RDCore.SDK.Workspace.Pictures;
using System.Buffers.Binary;

namespace RDCore.SDK.Runtime.Stdole;

/// <summary>
/// The file <c>SavePicture</c> writes for a <c>stdole.StdPicture</c> (<see cref="StdPictureObject"/>), in the layouts MS-VBA writes (Excel 64-bit).
/// </summary>
/// <remarks>
/// <list type="table">
/// <listheader><term>Picture</term><description>File written</description></listheader>
/// <item><term>A metafile, an enhanced metafile</term><description>The file it was loaded from, as it was read.</description></item>
/// <item><term>An uncompressed bitmap</term><description>Its pixels again, at its depth (24 bits for 32): a <c>BITMAPINFOHEADER</c> that states no
/// resolution, the colors of its table, and its rows from the bottom up, with the bits past a row's last pixel cleared.</description></item>
/// <item><term>A compressed bitmap</term><description>Its decoded pixels, uncompressed: for 8 bits per pixel or fewer, at the least of 1, 4 and 8 bits per
/// pixel that numbers its color table, with that table filled as <see cref="PictureWriter.WriteBitmap"/> fills it; at 24 bits otherwise. A pixel the
/// compression skips is color number 0.</description></item>
/// <item><term>A bitmap whose bit fields state an alpha field</term><description>Where a pixel's alpha is not 255, a bitmap of 32 bits per pixel that keeps
/// each pixel's alpha, not premultiplied, under a <c>BITMAPV5HEADER</c> whose bit fields state the alpha: RD-VBA addition, where MS-VBA writes it at 24
/// bits, its alpha left out. Where every pixel is opaque, it is written as a compressed bitmap is.</description></item>
/// <item><term>A GIF</term><description>A bitmap of its first frame's size and color numbers, at the least of 1, 4 and 8 bits per pixel that numbers its
/// color table, with that table filled as <see cref="PictureWriter.WriteBitmap"/> fills it. A GIF one of whose pixels is its transparent color is written
/// as a bitmap of 32 bits per pixel instead, as a bitmap with an alpha field is, those pixels clear. RD-VBA addition: MS-VBA writes the transparent color as
/// its own number, and the file loads opaque.</description></item>
/// <item><term>A PNG</term><description>A PNG of 8 bits per channel that keeps each pixel's alpha, written by <see cref="PictureWriter.WritePng"/>, whatever
/// the depth and color type it was loaded at. RD-VBA addition: MS-VBA does not load a PNG.</description></item>
/// <item><term>An icon, a cursor</term><description>An icon of one image, the one the picture holds (<see cref="StdPictureObject.Image"/>), written by
/// <see cref="PictureWriter.WriteIcon(DecodedPicture)"/>: 4 bits per pixel in <see cref="PictureWriter.IconColors"/>, and its AND mask. A cursor is written
/// as an icon. An image with an alpha that its mask does not give keeps that alpha, an RD-VBA addition where MS-VBA writes the 16 colors and the mask:
/// an image stored as a PNG is written by <see cref="PictureWriter.WritePngIcon"/>, as a PNG; a 32-bit image with a pixel whose alpha is neither 0 where the mask is set nor
/// 255 where it is clear, by <see cref="PictureWriter.WriteIcon(DecodedPicture, int)"/>, at 32 bits per pixel with its own AND mask. Any other image has
/// its transparency in its mask (a 32-bit image whose alpha is all 0 included), and is written in the 16 colors.</description></item>
/// <item><term>A JPEG</term><description>A bitmap of 24 bits per pixel. A JPEG of a kind that is not decoded
/// (<see cref="PictureDecodeFailure.NotDecoded"/>) is not written: <see cref="BytesOf"/> returns <see langword="null"/>.</description></item>
/// </list>
/// <para>
/// The file keeps the alpha <see cref="PictureDecoder"/> reads from the picture: decoded, it has the alpha the picture decodes to, pixel by pixel. A
/// picture that decodes opaque (a 32-bit bitmap of <c>BI_RGB</c>, whose fourth byte is reserved, among others), and an icon image whose alpha is its
/// mask's, have no more alpha to keep than MS-VBA's file holds, and are written as MS-VBA writes them.
/// </para>
/// <para>
/// A picture's pixels are decoded by <see cref="PictureDecoder"/>, so a body cut short is written with the pixels it lacks as 0.
/// </para>
/// </remarks>
public static class SavedPicture
{
    private const int FileHeader = 14;
    private const int InfoHeader = 40;
    private const int CoreHeader = 12;

    /// <summary>
    /// The file <c>SavePicture</c> writes for a picture.
    /// </summary>
    /// <param name="picture">The picture to save.</param>
    /// <returns>
    /// The file's bytes: no bytes for an object made by <c>New StdPicture</c> (<see cref="StdPictureObject.IsNew"/>). <see langword="null"/> for an object
    /// that holds no picture otherwise, which <c>SavePicture</c> refuses, and for a picture whose pixels are not decoded
    /// (<see cref="PictureDecodeFailure"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    public static byte[]? BytesOf(StdPictureObject picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var bytes = picture.Bytes.Span;
        return picture.Format switch
        {
            null => picture.IsNew ? [] : null,
            PictureFormat.Metafile or PictureFormat.EnhancedMetafile => picture.Bytes.ToArray(),
            PictureFormat.Bitmap => Bitmap(bytes) ?? Decoded(bytes),
            PictureFormat.Icon or PictureFormat.Cursor => Icon(bytes, picture.Image),
            PictureFormat.Gif => PictureDecoder.TryDecode(bytes, out var gif) is PictureDecodeFailure.None
                ? PictureWriter.WriteBitmap(gif!, KeepsAlpha(gif!) ? 32 : Depth(gif!))
                : null,
            PictureFormat.Png => PictureDecoder.TryDecode(bytes, out var png) is PictureDecodeFailure.None ? PictureWriter.WritePng(png!) : null,
            _ => PictureDecoder.TryDecode(bytes, out var other) is PictureDecodeFailure.None ? PictureWriter.WriteBitmap(other!, 24) : null,
        };
    }

    /// <summary>
    /// The file <c>SavePicture</c> writes for a picture, or why it writes none.
    /// </summary>
    /// <param name="picture">The picture to save.</param>
    /// <param name="bytes">
    /// The file's bytes, as <see cref="BytesOf"/> gives them, when the result is <see cref="SavedPictureFailure.None"/>; <see langword="null"/> otherwise.
    /// </param>
    /// <returns>
    /// <see cref="SavedPictureFailure.None"/> when there is a file to write, including the file of no bytes for an object made by <c>New StdPicture</c>;
    /// <see cref="SavedPictureFailure.NoPicture"/> for an object that holds no picture otherwise; <see cref="SavedPictureFailure.NotDecoded"/> for a
    /// picture whose pixels are not decoded.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    public static SavedPictureFailure TryBytesOf(StdPictureObject picture, out byte[]? bytes)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (picture is { Format: null, IsNew: false })
        {
            bytes = null;
            return SavedPictureFailure.NoPicture;
        }

        bytes = BytesOf(picture);
        return bytes is null ? SavedPictureFailure.NotDecoded : SavedPictureFailure.None;
    }

    // the image the picture holds, or the file's first: as a PNG where it is stored as one, at 32 bits where its alpha is more than its mask gives, in the 16
    // icon colors otherwise.
    private static byte[]? Icon(ReadOnlySpan<byte> bytes, IconImage? image)
    {
        DecodedPicture? decoded;
        var failure = image is { } held ? PictureDecoder.TryDecodeIconImage(bytes, held, out decoded) : PictureDecoder.TryDecode(bytes, out decoded);
        if (failure is not PictureDecodeFailure.None)
        {
            return null;
        }

        var stored = image ?? (PictureHeader.TryReadIconImages(bytes, out var images) ? images[0] : (IconImage?)null);
        if (stored is { } read && PngDecoder.IsPng(bytes.Slice(read.Offset, read.Length)))
        {
            return PictureWriter.WritePngIcon(decoded!);
        }

        return PictureWriter.WriteIcon(decoded!, AlphaBeyondMask(decoded!) ? 32 : 4);
    }

    // a compressed bitmap: decoded, then written at 32 bits where its alpha is its own and not all opaque, at the least depth its colors fit where its pixels
    // number them, and in 24 bits otherwise.
    private static byte[]? Decoded(ReadOnlySpan<byte> file) =>
        PictureDecoder.TryDecode(file, out var picture) is PictureDecodeFailure.None
            ? PictureWriter.WriteBitmap(picture!, KeepsAlpha(picture!) ? 32 : picture!.Indexes is null ? 24 : Depth(picture!))
            : null;

    // a picture whose alpha is its own (an alpha field, a GIF's transparent color), with a pixel that is not opaque: what a bitmap of 24 bits or fewer would
    // lose. The pixels a body cut short lacks make no picture's alpha its own.
    private static bool KeepsAlpha(DecodedPicture picture)
    {
        if (!picture.HasAlphaChannel)
        {
            return false;
        }

        var bgra = picture.Bgra.Span;
        for (var i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] != 0xFF)
            {
                return true;
            }
        }

        return false;
    }

    // an icon image with a pixel whose alpha is not its mask's (0 where the mask is set, 255 where it is clear): what 16 colors and a mask would lose.
    private static bool AlphaBeyondMask(DecodedPicture image)
    {
        var bgra = image.Bgra.Span;
        var mask = image.Mask is { } m ? m.Span : default;
        for (var i = 0; i < image.Width * image.Height; i++)
        {
            if (bgra[i * 4 + 3] != (!mask.IsEmpty && mask[i] != 0 ? 0 : 0xFF))
            {
                return true;
            }
        }

        return false;
    }

    // the least of 1, 4 and 8 bits per pixel that numbers the picture's color table, and every number its pixels hold.
    private static int Depth(DecodedPicture picture)
    {
        var colors = picture.Palette.Length;
        foreach (var number in picture.Indexes!.Value.Span)
        {
            colors = Math.Max(colors, number + 1);
        }

        return colors switch
        {
            <= 2 => 1,
            <= 16 => 4,
            _ => 8,
        };
    }

    // a bitmap file of uncompressed pixels, whatever its header: a BITMAPINFOHEADER of its size and depth (24 bits for 32), its colors, and its rows from
    // the bottom up, with the bits past a row's last pixel cleared. Null for compressed pixels, or a file shorter than its header says.
    private static byte[]? Bitmap(ReadOnlySpan<byte> file)
    {
        if (file.Length < FileHeader + CoreHeader)
        {
            return null;
        }

        var dib = file[FileHeader..];
        var size = BinaryPrimitives.ReadInt32LittleEndian(dib);
        var core = size == CoreHeader;
        if (!core && (size < InfoHeader || file.Length < FileHeader + InfoHeader || BinaryPrimitives.ReadInt32LittleEndian(dib[16..]) != 0))
        {
            return null;
        }

        var width = core ? BinaryPrimitives.ReadUInt16LittleEndian(dib[4..]) : BinaryPrimitives.ReadInt32LittleEndian(dib[4..]);
        var stated = core ? BinaryPrimitives.ReadUInt16LittleEndian(dib[6..]) : BinaryPrimitives.ReadInt32LittleEndian(dib[8..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(dib[(core ? 10 : 14)..]);
        var used = core ? 0 : BinaryPrimitives.ReadInt32LittleEndian(dib[32..]);
        if (width <= 0 || stated == 0 || bits is not (1 or 4 or 8 or 16 or 24 or 32))
        {
            return null;
        }

        var height = Math.Abs(stated);
        var colors = bits <= 8 ? (used > 0 && used < 1 << bits ? used : 1 << bits) : 0;
        var entry = core ? 3 : 4;
        var stride = Stride(width, bits);
        var pixels = (long)BinaryPrimitives.ReadUInt32LittleEndian(file[10..]);
        var table = FileHeader + size;
        if (table + (long)colors * entry > file.Length || pixels + (long)stride * height > file.Length)
        {
            return null;
        }

        var savedBits = bits == 32 ? 24 : bits;
        var savedStride = Stride(width, savedBits);
        var offset = FileHeader + InfoHeader + colors * 4;
        var saved = new byte[offset + savedStride * height];
        "BM"u8.CopyTo(saved);
        BinaryPrimitives.WriteInt32LittleEndian(saved.AsSpan(2), saved.Length);
        BinaryPrimitives.WriteInt32LittleEndian(saved.AsSpan(10), offset);

        var header = saved.AsSpan(FileHeader, InfoHeader);
        BinaryPrimitives.WriteInt32LittleEndian(header, InfoHeader);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], height);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], (ushort)savedBits);
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], savedStride * height);
        BinaryPrimitives.WriteInt32LittleEndian(header[32..], used);

        for (var i = 0; i < colors; i++)
        {
            file.Slice(table + i * entry, 3).CopyTo(saved.AsSpan(FileHeader + InfoHeader + i * 4));
        }

        var row = (int)(((long)width * savedBits + 7) / 8);
        var spare = (int)((long)width * savedBits % 8);
        for (var y = 0; y < height; y++)
        {
            // a top-down bitmap states a negative height: its first row is the top one.
            var source = file.Slice((int)(pixels + (long)(stated < 0 ? height - 1 - y : y) * stride), stride);
            var target = saved.AsSpan(offset + y * savedStride, row);
            if (bits == 32)
            {
                for (var x = 0; x < width; x++)
                {
                    source.Slice(x * 4, 3).CopyTo(target[(x * 3)..]);
                }
            }
            else
            {
                source[..row].CopyTo(target);
            }

            if (spare != 0)
            {
                target[^1] &= (byte)(0xFF << (8 - spare));
            }
        }

        return saved;
    }

    private static int Stride(int width, int bits) => (int)(((long)width * bits + 31) / 32 * 4);
}

/// <summary>
/// Why <see cref="SavedPicture.TryBytesOf"/> gave no file for a picture.
/// </summary>
public enum SavedPictureFailure
{
    /// <summary>
    /// There is a file to write.
    /// </summary>
    None,

    /// <summary>
    /// The object holds no picture and was not made by <c>New StdPicture</c>: the empty picture <c>LoadPicture</c> gives for no file name. MS-VBA's
    /// <c>SavePicture</c> raises run-time error 380, "Invalid property value", for it.
    /// </summary>
    NoPicture,

    /// <summary>
    /// The picture's pixels are not decoded (<see cref="PictureDecodeFailure"/>), so no file is written for it.
    /// </summary>
    NotDecoded,
}
