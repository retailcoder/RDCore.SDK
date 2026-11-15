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
/// <item><term>A compressed bitmap</term><description>Its decoded pixels, uncompressed: at its depth, with a full color table, for 8 bits per pixel or
/// fewer; at 24 bits otherwise.</description></item>
/// <item><term>A GIF</term><description>A bitmap of 8 bits per pixel: the color numbers of its first frame on its screen, and a table of 256 colors that is
/// its own padded with black.</description></item>
/// <item><term>A PNG</term><description>A bitmap of 24 bits per pixel, its alpha left out, as a 32-bit bitmap's is. RD-VBA addition: MS-VBA does not load a
/// PNG.</description></item>
/// <item><term>An icon, a cursor</term><description>An icon of one image, the one the picture holds (<see cref="StdPictureObject.Image"/>), written by
/// <see cref="PictureWriter.WriteIcon"/>: 4 bits per pixel in <see cref="PictureWriter.IconColors"/>, and its AND mask.</description></item>
/// </list>
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
            PictureFormat.Gif => PictureDecoder.TryDecode(bytes, out var gif) is PictureDecodeFailure.None ? PictureWriter.WriteBitmap(gif!, 8) : null,
            _ => PictureDecoder.TryDecode(bytes, out var other) is PictureDecodeFailure.None ? PictureWriter.WriteBitmap(other!, 24) : null,
        };
    }

    // the image the picture holds, or the file's first.
    private static byte[]? Icon(ReadOnlySpan<byte> bytes, IconImage? image)
    {
        DecodedPicture? decoded;
        var failure = image is { } held ? PictureDecoder.TryDecodeIconImage(bytes, held, out decoded) : PictureDecoder.TryDecode(bytes, out decoded);
        return failure is PictureDecodeFailure.None ? PictureWriter.WriteIcon(decoded!) : null;
    }

    // a compressed bitmap: decoded, then written at its depth where its pixels number their colors, and in 24 bits otherwise.
    private static byte[]? Decoded(ReadOnlySpan<byte> file)
    {
        if (PictureDecoder.TryDecode(file, out var picture) is not PictureDecodeFailure.None)
        {
            return null;
        }

        var bits = BinaryPrimitives.ReadUInt16LittleEndian(file[(FileHeader + 14)..]);
        return PictureWriter.WriteBitmap(picture!, picture!.Indexes is not null && bits is 1 or 4 or 8 ? bits : 24);
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
