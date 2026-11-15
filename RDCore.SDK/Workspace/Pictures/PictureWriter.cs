using System.Buffers.Binary;
using System.Collections.Immutable;

namespace RDCore.SDK.Workspace.Pictures;

/// <summary>
/// Writes a <see cref="DecodedPicture"/> as a bitmap file or an icon file.
/// </summary>
/// <remarks>
/// <para>
/// A bitmap is written uncompressed: a 14-byte file header, a 40-byte <c>BITMAPINFOHEADER</c> that states no resolution and no count of colors used, the
/// color table of an indexed bitmap, and the rows from the bottom up, each padded with zeros to 4 bytes.
/// </para>
/// <para>
/// An icon is written as one image of 4 bits per pixel in the 16 colors of <see cref="IconColors"/>, with its AND mask: a 6-byte header, one 16-byte
/// directory entry that states 16 colors and no planes or bits per pixel, then the image's DIB, twice its height for the mask.
/// </para>
/// </remarks>
public static class PictureWriter
{
    private const int FileHeader = 14;
    private const int InfoHeader = 40;

    /// <summary>
    /// The 16 colors an icon is written in, each as <c>0xAARRGGBB</c>, in the order of its color table: black, maroon, green, olive, navy, purple, teal,
    /// gray, silver, red, lime, yellow, blue, fuchsia, aqua and white.
    /// </summary>
    public static ImmutableArray<uint> IconColors { get; } =
    [
        0xFF000000, 0xFF800000, 0xFF008000, 0xFF808000, 0xFF000080, 0xFF800080, 0xFF008080, 0xFF808080,
        0xFFC0C0C0, 0xFFFF0000, 0xFF00FF00, 0xFFFFFF00, 0xFF0000FF, 0xFFFF00FF, 0xFF00FFFF, 0xFFFFFFFF,
    ];

    /// <summary>
    /// The 16 standard colors, each as <c>0xAARRGGBB</c>, in the order <see cref="WriteBitmap"/> fills a color table with them: black, maroon, green, olive,
    /// navy, purple, teal, silver, gray, red, lime, yellow, blue, fuchsia, aqua and white.
    /// </summary>
    public static ImmutableArray<uint> StandardColors { get; } =
    [
        0xFF000000, 0xFF800000, 0xFF008000, 0xFF808000, 0xFF000080, 0xFF800080, 0xFF008080, 0xFFC0C0C0,
        0xFF808080, 0xFFFF0000, 0xFF00FF00, 0xFFFFFF00, 0xFF0000FF, 0xFFFF00FF, 0xFF00FFFF, 0xFFFFFFFF,
    ];

    /// <summary>
    /// Writes a picture as a bitmap file.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="bitsPerPixel">
    /// 24, to write <see cref="DecodedPicture.Bgra"/> with its alpha left out; or 1, 4 or 8, to write <see cref="DecodedPicture.Indexes"/> with a color table
    /// of 2, 16 or 256 entries: <see cref="DecodedPicture.Palette"/>, then the <see cref="StandardColors"/> it lacks, in their order, when the palette and
    /// they fill the table exactly; otherwise black entries fill it.
    /// </param>
    /// <returns>The bitmap file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitsPerPixel"/> is not 1, 4, 8 or 24.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bitsPerPixel"/> is 1, 4 or 8, and the picture has no <see cref="DecodedPicture.Indexes"/>, or more colors than that depth numbers.
    /// </exception>
    public static byte[] WriteBitmap(DecodedPicture picture, int bitsPerPixel)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (bitsPerPixel is not (1 or 4 or 8 or 24))
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerPixel), bitsPerPixel, "A bitmap is written at 1, 4, 8 or 24 bits per pixel.");
        }

        var colors = bitsPerPixel == 24 ? 0 : 1 << bitsPerPixel;
        if (colors > 0 && (picture.Indexes is null || picture.Palette.Length > colors))
        {
            throw new ArgumentException($"The picture has no color numbers, or more colors than {bitsPerPixel} bits per pixel number.", nameof(picture));
        }

        int width = picture.Width, height = picture.Height;
        var stride = Stride(width, bitsPerPixel);
        var offset = FileHeader + InfoHeader + colors * 4;
        var bytes = new byte[offset + stride * height];
        "BM"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), offset);
        Header(bytes.AsSpan(FileHeader), width, height, bitsPerPixel, stride * height);
        var table = Table(picture.Palette, colors);
        for (var i = 0; i < table.Count; i++)
        {
            Entry(bytes.AsSpan(FileHeader + InfoHeader + i * 4), table[i]);
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
    /// Writes a picture as an icon file of one image, in the 16 colors of <see cref="IconColors"/>.
    /// </summary>
    /// <param name="picture">The picture: an icon's or a cursor's image, or any other.</param>
    /// <returns>The icon file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Each pixel is written as the nearest of the 16 colors, the first of them where two are as near, the pixels the mask is set for included. The mask is
    /// <see cref="DecodedPicture.Mask"/>, whatever the pixels' alpha, or for a picture with none, set where a pixel's alpha is 0. A directory entry states a
    /// side of 256 pixels or more as 0.
    /// </remarks>
    public static byte[] WriteIcon(DecodedPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        int width = picture.Width, height = picture.Height;
        int colorStride = Stride(width, 4), maskStride = Stride(width, 1);
        var image = InfoHeader + 16 * 4 + (colorStride + maskStride) * height;
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
        Header(dib, width, height * 2, 4, (colorStride + maskStride) * height);
        for (var i = 0; i < 16; i++)
        {
            Entry(dib[(InfoHeader + i * 4)..], IconColors[i]);
        }

        var colors = InfoHeader + 16 * 4;
        var mask = colors + colorStride * height;
        var bgra = picture.Bgra.Span;
        var given = picture.Mask is { } m ? m.Span : default;
        for (var y = 0; y < height; y++)
        {
            var colorRow = dib.Slice(colors + (height - 1 - y) * colorStride, colorStride);
            var maskRow = dib.Slice(mask + (height - 1 - y) * maskStride, maskStride);
            for (var x = 0; x < width; x++)
            {
                var pixel = y * width + x;
                if (given.IsEmpty ? bgra[pixel * 4 + 3] == 0 : given[pixel] != 0)
                {
                    Pack(maskRow, x, 1, 1);
                }

                Pack(colorRow, x, 4, Nearest(bgra[pixel * 4 + 2], bgra[pixel * 4 + 1], bgra[pixel * 4]));
            }
        }

        return bytes;
    }

    // a color table of the size given: the palette, then the standard colors it lacks where they fill the table exactly; the entries left are black.
    private static List<uint> Table(ImmutableArray<uint> palette, int size)
    {
        var table = new List<uint>(palette);
        if (table.Count < size)
        {
            var lacking = StandardColors.Where(standard => !palette.Any(color => (color & 0xFFFFFF) == (standard & 0xFFFFFF))).ToList();
            if (table.Count + lacking.Count == size)
            {
                table.AddRange(lacking);
            }
        }

        return table;
    }

    private static int Stride(int width, int bits) => (int)(((long)width * bits + 31) / 32 * 4);

    // a BITMAPINFOHEADER: its size, width, height, 1 plane, the depth, no compression, the size of the pixels, and no resolution or colors used.
    private static void Header(Span<byte> header, int width, int height, int bits, int imageSize)
    {
        BinaryPrimitives.WriteInt32LittleEndian(header, InfoHeader);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], width);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], height);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], (ushort)bits);
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], imageSize);
    }

    // a color table entry: blue, green, red and a reserved 0.
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
        for (var i = 0; i < IconColors.Length; i++)
        {
            var color = IconColors[i];
            long dr = red - (int)(color >> 16 & 0xFF), dg = green - (int)(color >> 8 & 0xFF), db = blue - (int)(color & 0xFF);
            var distance = dr * dr + dg * dg + db * db;
            if (distance < nearest)
            {
                (best, nearest) = (i, distance);
            }
        }

        return best;
    }
}
