using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;

namespace RDCore.SDK.Workspace.Pictures;

/// <summary>
/// Writes a <see cref="DecodedPicture"/> as a bitmap file, a PNG file or an icon file.
/// </summary>
/// <remarks>
/// <para>
/// A bitmap is written uncompressed: a 14-byte file header, a 40-byte <c>BITMAPINFOHEADER</c> (a 124-byte <c>BITMAPV5HEADER</c> at 32 bits per pixel)
/// that states no resolution and no count of colors used, the color table of an indexed bitmap, and the rows from the bottom up, each padded with zeros to
/// 4 bytes.
/// </para>
/// <para>
/// <see cref="WriteIcon(DecodedPicture)"/> writes an icon as one image of 4 bits per pixel in the 16 colors of <see cref="IconColors"/>, with its AND mask:
/// a 6-byte header, one 16-byte directory entry that states 16 colors and no planes or bits per pixel, then the image's DIB, twice its height for the
/// mask. <see cref="WriteIcon(DecodedPicture, int)"/> also writes the image at 32 bits per pixel, and <see cref="WritePngIcon"/> stores it as a PNG.
/// </para>
/// <para>
/// A PNG is written at 8 bits per channel with its alpha (color type 6), not interlaced: its signature, then an <c>IHDR</c>, one <c>IDAT</c> and an
/// <c>IEND</c> chunk, and no other. Each row is written unfiltered, and the rows are compressed with zlib.
/// </para>
/// </remarks>
public static class PictureWriter
{
    private const int FileHeader = 14;
    private const int InfoHeader = 40;
    private const int Directory = 6 + 16;
    private const int V5Header = 124;

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
    /// 32, to write <see cref="DecodedPicture.Bgra"/> with its alpha, not premultiplied, under a <c>BITMAPV5HEADER</c> whose bit fields state the alpha;
    /// 24, to write it with its alpha left out; or 1, 4 or 8, to write <see cref="DecodedPicture.Indexes"/> with a color table of 2, 16 or 256 entries:
    /// <see cref="DecodedPicture.Palette"/>, then the <see cref="StandardColors"/> it lacks, in their order, when the palette and they fill the table
    /// exactly; otherwise black entries fill it.
    /// </param>
    /// <returns>The bitmap file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitsPerPixel"/> is not 1, 4, 8, 24 or 32.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bitsPerPixel"/> is 1, 4 or 8, and the picture has no <see cref="DecodedPicture.Indexes"/>, or more colors than that depth numbers.
    /// </exception>
    /// <remarks>
    /// The <c>BITMAPV5HEADER</c> of a 32-bit bitmap states <c>BI_BITFIELDS</c>, the fields <c>0x00FF0000</c>, <c>0x0000FF00</c>, <c>0x000000FF</c> and
    /// <c>0xFF000000</c> for red, green, blue and alpha, and the sRGB color space. Every other bitmap has a 40-byte <c>BITMAPINFOHEADER</c>.
    /// </remarks>
    public static byte[] WriteBitmap(DecodedPicture picture, int bitsPerPixel)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (bitsPerPixel is not (1 or 4 or 8 or 24 or 32))
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerPixel), bitsPerPixel, "A bitmap is written at 1, 4, 8, 24 or 32 bits per pixel.");
        }

        var colors = bitsPerPixel >= 24 ? 0 : 1 << bitsPerPixel;
        if (colors > 0 && (picture.Indexes is null || picture.Palette.Length > colors))
        {
            throw new ArgumentException($"The picture has no color numbers, or more colors than {bitsPerPixel} bits per pixel number.", nameof(picture));
        }

        int width = picture.Width, height = picture.Height;
        var stride = Stride(width, bitsPerPixel);
        var header = bitsPerPixel == 32 ? V5Header : InfoHeader;
        var offset = FileHeader + header + colors * 4;
        var bytes = new byte[offset + stride * height];
        "BM"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), offset);
        Header(bytes.AsSpan(FileHeader), width, height, bitsPerPixel, stride * height);
        if (bitsPerPixel == 32)
        {
            AlphaFields(bytes.AsSpan(FileHeader, V5Header));
        }

        // a bitmap of 24 or 32 bits has no color table, whatever the picture's palette.
        var table = colors > 0 ? Table(picture.Palette, colors) : [];
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
                if (bitsPerPixel >= 24)
                {
                    bgra.Slice(pixel * 4, bitsPerPixel / 8).CopyTo(row[(x * bitsPerPixel / 8)..]);
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
    public static byte[] WriteIcon(DecodedPicture picture) => WriteIcon(picture, 4);

    /// <summary>
    /// Writes a picture as an icon file of one image: in the 16 colors of <see cref="IconColors"/>, as <see cref="WriteIcon(DecodedPicture)"/> writes it,
    /// or at 32 bits per pixel, each pixel's alpha kept.
    /// </summary>
    /// <param name="picture">The picture: an icon's or a cursor's image, or any other.</param>
    /// <param name="bitsPerPixel">4, for the 16 colors; or 32, to write <see cref="DecodedPicture.Bgra"/> with its alpha, not premultiplied.</param>
    /// <returns>The icon file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitsPerPixel"/> is not 4 or 32.</exception>
    /// <remarks>
    /// A 32-bit image's directory entry states no colors, 1 plane and 32 bits per pixel, and its DIB has no color table. Its AND mask is written as at 4 bits
    /// per pixel: <see cref="DecodedPicture.Mask"/>, or for a picture with none, set where a pixel's alpha is 0.
    /// </remarks>
    public static byte[] WriteIcon(DecodedPicture picture, int bitsPerPixel)
    {
        ArgumentNullException.ThrowIfNull(picture);
        if (bitsPerPixel is not (4 or 32))
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerPixel), bitsPerPixel, "An icon is written at 4 or 32 bits per pixel.");
        }

        var four = bitsPerPixel == 4;
        int width = picture.Width, height = picture.Height;
        int colorStride = Stride(width, bitsPerPixel), maskStride = Stride(width, 1);
        var table = four ? 16 * 4 : 0;
        var image = InfoHeader + table + (colorStride + maskStride) * height;
        var bytes = new byte[Directory + image];
        IconDirectory(bytes, width, height, four ? 16 : 0, four ? 0 : 1, four ? 0 : 32, image);
        var dib = bytes.AsSpan(Directory);
        Header(dib, width, height * 2, bitsPerPixel, (colorStride + maskStride) * height);
        for (var i = 0; i < table / 4; i++)
        {
            Entry(dib[(InfoHeader + i * 4)..], IconColors[i]);
        }

        var colors = InfoHeader + table;
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

                if (four)
                {
                    Pack(colorRow, x, 4, Nearest(bgra[pixel * 4 + 2], bgra[pixel * 4 + 1], bgra[pixel * 4]));
                }
                else
                {
                    bgra.Slice(pixel * 4, 4).CopyTo(colorRow[(x * 4)..]);
                }
            }
        }

        return bytes;
    }

    /// <summary>
    /// Writes a picture as a PNG file of 8 bits per channel that keeps each pixel's alpha.
    /// </summary>
    /// <param name="picture">The picture: <see cref="DecodedPicture.Bgra"/> is written, its alpha as it is, not premultiplied.</param>
    /// <returns>The PNG file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <see cref="DecodedPicture.Palette"/>, <see cref="DecodedPicture.Indexes"/> and <see cref="DecodedPicture.Mask"/> are not written: the pixels are
    /// written as <see cref="DecodedPicture.Bgra"/> holds them.
    /// </remarks>
    public static byte[] WritePng(DecodedPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        int width = picture.Width, height = picture.Height;
        var rows = new byte[height * (1 + width * 4)];
        var bgra = picture.Bgra.Span;
        for (var y = 0; y < height; y++)
        {
            var row = rows.AsSpan(y * (1 + width * 4) + 1, width * 4);
            for (var x = 0; x < width; x++)
            {
                var pixel = bgra.Slice((y * width + x) * 4, 4);
                (row[x * 4], row[x * 4 + 1], row[x * 4 + 2], row[x * 4 + 3]) = (pixel[2], pixel[1], pixel[0], pixel[3]);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(rows);
        }

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        (header[8], header[9]) = (8, 6);

        using var file = new MemoryStream();
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(file, "IHDR"u8, header);
        Chunk(file, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        Chunk(file, "IEND"u8, []);
        return file.ToArray();
    }

    /// <summary>
    /// Writes a picture as an icon file of one image stored as a PNG, written by <see cref="WritePng"/>, so that each pixel keeps its alpha.
    /// </summary>
    /// <param name="picture">The picture: an icon's or a cursor's image, or any other.</param>
    /// <returns>The icon file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="picture"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The directory entry states no colors, 1 plane and 32 bits per pixel, and a side of 256 pixels or more as 0. A PNG image has no AND mask:
    /// <see cref="DecodedPicture.Mask"/> is not written, and a pixel shows what is under it where its alpha says so.
    /// </remarks>
    public static byte[] WritePngIcon(DecodedPicture picture)
    {
        var png = WritePng(picture);
        var bytes = new byte[Directory + png.Length];
        IconDirectory(bytes, picture.Width, picture.Height, 0, 1, 32, png.Length);
        png.CopyTo(bytes, Directory);
        return bytes;
    }

    /// <summary>
    /// Writes an icon file or a cursor file that holds one of a file's images, as the file holds it.
    /// </summary>
    /// <param name="file">An icon file or a cursor file.</param>
    /// <param name="image">One of its images, as <see cref="PictureHeader.TryReadIconImages"/> reads them.</param>
    /// <returns>
    /// The bytes of a file of the same type, an icon or a cursor, whose one directory entry is the image's own, a cursor's hot spot included, and whose
    /// image is the image's bytes as they are: a PNG stays a PNG, and a DIB keeps its depth, its color table and its AND mask.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="file"/> is not an icon file or a cursor file, or <paramref name="image"/> is not one of its images.
    /// </exception>
    public static byte[] WriteIconImage(ReadOnlySpan<byte> file, IconImage image)
    {
        var index = PictureHeader.TryReadIconImages(file, out var images) ? images.IndexOf(image) : -1;
        if (index < 0)
        {
            throw new ArgumentException("The image is not one of the icon's or the cursor's images.", nameof(image));
        }

        var bytes = new byte[Directory + image.Length];
        file[..4].CopyTo(bytes);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), 1);
        file.Slice(6 + 16 * index, 16).CopyTo(bytes.AsSpan(6));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), image.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), Directory);
        file.Slice(image.Offset, image.Length).CopyTo(bytes.AsSpan(Directory));
        return bytes;
    }

    // an icon file's header (type 1, one image) and its directory entry: the image's sides (0 for 256 or more), its colors, planes and bits per pixel, its
    // size, and its offset, just past the entry.
    private static void IconDirectory(Span<byte> file, int width, int height, int colors, int planes, int bits, int size)
    {
        BinaryPrimitives.WriteInt16LittleEndian(file[2..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(file[4..], 1);
        file[6] = (byte)(width >= 256 ? 0 : width);
        file[7] = (byte)(height >= 256 ? 0 : height);
        file[8] = (byte)colors;
        BinaryPrimitives.WriteUInt16LittleEndian(file[10..], (ushort)planes);
        BinaryPrimitives.WriteUInt16LittleEndian(file[12..], (ushort)bits);
        BinaryPrimitives.WriteInt32LittleEndian(file[14..], size);
        BinaryPrimitives.WriteInt32LittleEndian(file[18..], Directory);
    }

    // a PNG chunk: its data's length, its type, its data, and the CRC-32 of its type and data.
    private static void Chunk(Stream file, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        file.Write(number);
        file.Write(type);
        file.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, ~Crc(Crc(0xFFFFFFFF, type), data));
        file.Write(number);
    }

    // the CRC-32 of ISO 3309 and ITU-T V.42 that PNG uses (reflected, polynomial 0xEDB88320), carried on from the value given.
    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc = CrcTable[(int)((crc ^ b) & 0xFF)] ^ crc >> 8;
        }

        return crc;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320 ^ c >> 1 : c >> 1;
        }

        return c;
    }).ToArray();

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

    // a BITMAPV5HEADER's size, BI_BITFIELDS, its red, green, blue and alpha fields, and LCS_sRGB, over a BITMAPINFOHEADER's fields.
    private static void AlphaFields(Span<byte> header)
    {
        BinaryPrimitives.WriteInt32LittleEndian(header, V5Header);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], 3);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], 0x00FF0000);
        BinaryPrimitives.WriteUInt32LittleEndian(header[44..], 0x0000FF00);
        BinaryPrimitives.WriteUInt32LittleEndian(header[48..], 0x000000FF);
        BinaryPrimitives.WriteUInt32LittleEndian(header[52..], 0xFF000000);
        BinaryPrimitives.WriteUInt32LittleEndian(header[56..], 0x73524742);
    }

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
