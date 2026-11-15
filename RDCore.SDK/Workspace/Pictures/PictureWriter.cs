using System.Buffers.Binary;
using System.Collections.Immutable;

namespace RDCore.SDK.Workspace.Pictures;

/// <summary>
/// Writes a <see cref="DecodedPicture"/> as a bitmap file or an icon file.
/// </summary>
/// <remarks>
/// <para>
/// A bitmap is written uncompressed: a 14-byte file header, a 40-byte <c>BITMAPINFOHEADER</c> that states no resolution and no count of colours used, the
/// colour table of an indexed bitmap, and the rows from the bottom up, each padded with zeros to 4 bytes.
/// </para>
/// <para>
/// An icon is written as one image of 4 bits per pixel in the 16 colours of <see cref="IconColours"/>, with its AND mask: a 6-byte header, one 16-byte
/// directory entry that states 16 colours and no planes or bits per pixel, then the image's DIB, twice its height for the mask.
/// </para>
/// </remarks>
public static class PictureWriter
{
    private const int FileHeader = 14;
    private const int InfoHeader = 40;

    /// <summary>
    /// The 16 colours an icon is written in, each as <c>0xAARRGGBB</c>, in the order of its colour table: black, maroon, green, olive, navy, purple, teal,
    /// silver, grey, red, lime, yellow, blue, fuchsia, aqua and white.
    /// </summary>
    public static ImmutableArray<uint> IconColours { get; } =
    [
        0xFF000000, 0xFF800000, 0xFF008000, 0xFF808000, 0xFF000080, 0xFF800080, 0xFF008080, 0xFFC0C0C0,
        0xFF808080, 0xFFFF0000, 0xFF00FF00, 0xFFFFFF00, 0xFF0000FF, 0xFFFF00FF, 0xFF00FFFF, 0xFFFFFFFF,
    ];

    /// <summary>
    /// Writes a picture as a bitmap file.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="bitsPerPixel">
    /// 24, to write <see cref="DecodedPicture.Bgra"/> with its alpha left out; or 1, 4 or 8, to write <see cref="DecodedPicture.Indexes"/> with a colour table
    /// of 2, 16 or 256 entries: <see cref="DecodedPicture.Palette"/>, then black entries to fill the table.
    /// </param>
    /// <returns>The bitmap file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitsPerPixel"/> is not 1, 4, 8 or 24.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bitsPerPixel"/> is 1, 4 or 8, and the picture has no <see cref="DecodedPicture.Indexes"/>, or more colours than that depth numbers.
    /// </exception>
    public static byte[] WriteBitmap(DecodedPicture picture, int bitsPerPixel)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (bitsPerPixel is not (1 or 4 or 8 or 24))
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerPixel), bitsPerPixel, "A bitmap is written at 1, 4, 8 or 24 bits per pixel.");
        }

        var colours = bitsPerPixel == 24 ? 0 : 1 << bitsPerPixel;
        if (colours > 0 && (picture.Indexes is null || picture.Palette.Length > colours))
        {
            throw new ArgumentException($"The picture has no colour numbers, or more colours than {bitsPerPixel} bits per pixel number.", nameof(picture));
        }

        int width = picture.Width, height = picture.Height;
        var stride = Stride(width, bitsPerPixel);
        var offset = FileHeader + InfoHeader + colours * 4;
        var bytes = new byte[offset + stride * height];
        "BM"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), offset);
        Header(bytes.AsSpan(FileHeader), width, height, bitsPerPixel, stride * height);
        for (var i = 0; i < picture.Palette.Length; i++)
        {
            Entry(bytes.AsSpan(FileHeader + InfoHeader + i * 4), picture.Palette[i]);
        }

        var bgra = picture.Bgra.Span;
        var indexes = picture.Indexes is { } numbers ? numbers.Span : default;
        for (var y = 0; y < height; y++)
        {
            var row = bytes.AsSpan(offset + (height - 1 - y) * stride, stride);
            for (var x = 0; x < width; x++)
            {
                var pixel = y * width + x;
                if (bitsPerPixel == 24)
                {
                    bgra.Slice(pixel * 4, 3).CopyTo(row[(x * 3)..]);
                }
                else
                {
                    Pack(row, x, bitsPerPixel, indexes[pixel]);
                }
            }
        }

        return bytes;
    }

    /// <summary>
    /// Writes a picture as an icon file of one image, in the 16 colours of <see cref="IconColours"/>.
    /// </summary>
    /// <param name="picture">The picture: an icon's or a cursor's image, or any other.</param>
    /// <returns>The icon file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Each pixel is written as the nearest of the 16 colours, the first of them where two are as near. The mask is <see cref="DecodedPicture.Mask"/>, or for
    /// a picture with none, set where a pixel's alpha is 0; a pixel the mask is set for is written black. A directory entry states a side of 256 pixels or more
    /// as 0.
    /// </remarks>
    public static byte[] WriteIcon(DecodedPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        int width = picture.Width, height = picture.Height;
        int colourStride = Stride(width, 4), maskStride = Stride(width, 1);
        var image = InfoHeader + 16 * 4 + (colourStride + maskStride) * height;
        const int Directory = 6 + 16;
        var bytes = new byte[Directory + image];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), 1);
        bytes[6] = (byte)(width >= 256 ? 0 : width);
        bytes[7] = (byte)(height >= 256 ? 0 : height);
        bytes[8] = 16;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), image);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), Directory);
        var dib = bytes.AsSpan(Directory);
        Header(dib, width, height * 2, 4, (colourStride + maskStride) * height);
        for (var i = 0; i < 16; i++)
        {
            Entry(dib[(InfoHeader + i * 4)..], IconColours[i]);
        }

        var colours = InfoHeader + 16 * 4;
        var mask = colours + colourStride * height;
        var bgra = picture.Bgra.Span;
        var given = picture.Mask is { } m ? m.Span : default;
        for (var y = 0; y < height; y++)
        {
            var colourRow = dib.Slice(colours + (height - 1 - y) * colourStride, colourStride);
            var maskRow = dib.Slice(mask + (height - 1 - y) * maskStride, maskStride);
            for (var x = 0; x < width; x++)
            {
                var pixel = y * width + x;
                var masked = given.IsEmpty ? bgra[pixel * 4 + 3] == 0 : given[pixel] != 0;
                if (masked)
                {
                    Pack(maskRow, x, 1, 1);
                }
                else
                {
                    Pack(colourRow, x, 4, Nearest(bgra[pixel * 4 + 2], bgra[pixel * 4 + 1], bgra[pixel * 4]));
                }
            }
        }

        return bytes;
    }

    private static int Stride(int width, int bits) => (int)(((long)width * bits + 31) / 32 * 4);

    // a BITMAPINFOHEADER: its size, width, height, 1 plane, the depth, no compression, the size of the pixels, and no resolution or colours used.
    private static void Header(Span<byte> header, int width, int height, int bits, int imageSize)
    {
        BinaryPrimitives.WriteInt32LittleEndian(header, InfoHeader);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], height);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], (ushort)bits);
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], imageSize);
    }

    // a colour table entry: blue, green, red and a reserved 0.
    private static void Entry(Span<byte> entry, uint argb)
    {
        entry[0] = (byte)argb;
        entry[1] = (byte)(argb >> 8);
        entry[2] = (byte)(argb >> 16);
    }

    // a pixel's number packed into its row from the high bits, at 1, 4 or 8 bits; a number too large for the depth keeps its low bits.
    private static void Pack(Span<byte> row, int x, int bits, int value)
    {
        var bit = x * bits;
        var shift = 8 - bits - bit % 8;
        row[bit / 8] |= (byte)((value & (1 << bits) - 1) << shift);
    }

    private static int Nearest(byte red, byte green, byte blue)
    {
        var best = 0;
        var nearest = long.MaxValue;
        for (var i = 0; i < IconColours.Length; i++)
        {
            var colour = IconColours[i];
            long dr = red - (int)(colour >> 16 & 0xFF), dg = green - (int)(colour >> 8 & 0xFF), db = blue - (int)(colour & 0xFF);
            var distance = dr * dr + dg * dg + db * db;
            if (distance < nearest)
            {
                (best, nearest) = (i, distance);
            }
        }

        return best;
    }
}
