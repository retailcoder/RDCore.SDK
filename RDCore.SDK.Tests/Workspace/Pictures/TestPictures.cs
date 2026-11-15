using System.Buffers.Binary;

namespace RDCore.Tests.Workspace.Pictures;

// Pictures made byte by byte, for the tests that read and decode them.
internal static class TestPictures
{
    // A 2x2 24-bit bitmap, bottom-up: blue and white on top, red and green below.
    public static byte[] Bitmap2x2 { get; } = Hex(
        "424D 46000000 00000000 36000000",
        "28000000 02000000 02000000 0100 1800 00000000 10000000 130B0000 130B0000 00000000 00000000",
        "0000FF 00FF00 0000",
        "FF0000 FFFFFF 0000");

    // The smallest GIF: one transparent pixel.
    public static byte[] Gif1x1 { get; } = Hex("474946383961 0100 0100 80 00 00 FFFFFF 000000 21F904 01 0000 00 00 2C 0000 0000 0100 0100 00 02 0244 0100 3B");

    public static byte[] Hex(params string[] parts) => Convert.FromHexString(string.Concat(parts).Replace(" ", ""));

    // A bitmap's headers only, declaring a size; the inspection reads no further.
    public static byte[] BitmapHeader(int width, int height)
    {
        var bytes = new byte[64];
        "BM"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), height);
        return bytes;
    }

    // A PNG's signature and IHDR chunk only, declaring a size.
    public static byte[] PngHeader(int width, int height)
    {
        var bytes = Hex("89504E470D0A1A0A 0000000D 49484452 00000000 00000000 0806000000 00000000");
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    // A JPEG's start of image, an APP0 segment, and a baseline start of frame declaring a size.
    public static byte[] JpegHeader(int width, int height)
    {
        var bytes = Hex("FFD8 FFE0 0010 4A46494600 0101 00 0001 0001 0000", "FFC0 0011 08 0000 0000 03 012200 021101 031101");
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(25), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(27), (ushort)width);
        return bytes;
    }

    // An icon (type 1) or a cursor (type 2) holding the images given, each a PNG or a DIB.
    public static byte[] Icon(short type, params byte[][] images)
    {
        var header = 6 + 16 * images.Length;
        var bytes = new byte[header + images.Sum(i => i.Length)];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), type);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), (short)images.Length);
        var offset = header;
        for (var i = 0; i < images.Length; i++)
        {
            var entry = bytes.AsSpan(6 + 16 * i);
            BinaryPrimitives.WriteInt32LittleEndian(entry[8..], images[i].Length);
            BinaryPrimitives.WriteInt32LittleEndian(entry[12..], offset);
            images[i].CopyTo(bytes, offset);
            offset += images[i].Length;
        }
        return bytes;
    }

    // Rows deflated as a PNG's IDAT holds them, compressed or in stored blocks.
    public static byte[] Deflated(byte[] rows, bool stored = false)
    {
        using var compressed = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(compressed,
            stored ? System.IO.Compression.CompressionLevel.NoCompression : System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            z.Write(rows);
        return compressed.ToArray();
    }

    // A PNG of the given IHDR, its rows (each with its filter byte first) deflated into one IDAT, or the IDAT data given, then the chunks given and IEND.
    // The checksums are 0: no reader checks them.
    public static byte[] Png(int width, int height, byte type, byte depth, byte[] rows, bool interlaced = false, byte[]? idat = null,
        params (string Type, byte[] Data)[] chunks)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        (ihdr[8], ihdr[9], ihdr[12]) = (depth, type, (byte)(interlaced ? 1 : 0));
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        void Chunk(string kind, byte[] data)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            png.Write(length);
            png.Write(System.Text.Encoding.ASCII.GetBytes(kind));
            png.Write(data);
            png.Write(new byte[4]);
        }
        Chunk("IHDR", ihdr);
        foreach (var (kind, data) in chunks)
            Chunk(kind, data);
        Chunk("IDAT", idat ?? Deflated(rows));
        Chunk("IEND", []);
        return png.ToArray();
    }

    // A GIF89a of the given screen, global table and one frame at the given place: a graphic control extension where a color is transparent, then the
    // frame's pixels LZW-encoded.
    public static byte[] Gif(int width, int height, uint[] table, byte[] pixels, int frameWidth, int frameHeight, int left = 0, int top = 0,
        int transparent = -1, bool interlaced = false)
    {
        var bits = 1;
        while (1 << bits < table.Length)
            bits++;
        using var gif = new MemoryStream();
        gif.Write("GIF89a"u8);
        void U16(int value) { gif.WriteByte((byte)value); gif.WriteByte((byte)(value >> 8)); }
        U16(width);
        U16(height);
        gif.WriteByte((byte)(0x80 | bits - 1));
        gif.WriteByte(0);
        gif.WriteByte(0);
        for (var i = 0; i < 1 << bits; i++)
        {
            var color = i < table.Length ? table[i] : 0;
            gif.Write([(byte)(color >> 16), (byte)(color >> 8), (byte)color]);
        }
        if (transparent >= 0)
            gif.Write([0x21, 0xF9, 4, 1, 0, 0, (byte)transparent, 0]);
        gif.WriteByte(0x2C);
        U16(left);
        U16(top);
        U16(frameWidth);
        U16(frameHeight);
        gif.WriteByte((byte)(interlaced ? 0x40 : 0));
        var minimum = Math.Max(2, bits);
        gif.WriteByte((byte)minimum);
        var data = Lzw(pixels, minimum);
        for (var at = 0; at < data.Length; at += 255)
        {
            var length = Math.Min(255, data.Length - at);
            gif.WriteByte((byte)length);
            gif.Write(data, at, length);
        }
        gif.WriteByte(0);
        gif.WriteByte(0x3B);
        return gif.ToArray();
    }

    // GIF's LZW: a clear code first, codes of growing size from the low bit up, a clear code where the table fills, and the end code.
    public static byte[] Lzw(byte[] pixels, int minimum)
    {
        int clear = 1 << minimum, end = clear + 1, next = clear + 2, size = minimum + 1;
        var table = new Dictionary<(int, int), int>();
        var output = new List<byte>();
        int buffer = 0, held = 0;
        void Write(int code)
        {
            buffer |= code << held;
            held += size;
            while (held >= 8)
            {
                output.Add((byte)buffer);
                buffer >>= 8;
                held -= 8;
            }
        }
        Write(clear);
        int prefix = pixels[0];
        for (var i = 1; i < pixels.Length; i++)
        {
            if (table.TryGetValue((prefix, pixels[i]), out var code))
            {
                prefix = code;
                continue;
            }
            Write(prefix);
            if (next < 4096)
            {
                table[(prefix, pixels[i])] = next++;
                if (next > 1 << size && size < 12)
                    size++;
            }
            else
            {
                Write(clear);
                (table, next, size) = (new(), clear + 2, minimum + 1);
            }
            prefix = pixels[i];
        }
        Write(prefix);
        Write(end);
        if (held > 0)
            output.Add((byte)buffer);
        return [.. output];
    }

    // A DIB as an icon holds an image: a BITMAPINFOHEADER of twice the height, the color table, the rows from the bottom up, then the AND mask's.
    public static byte[] IconDib(int width, int height, int bits, uint[] table, byte[] colorRows, byte[] maskRows)
    {
        var bytes = new byte[40 + table.Length * 4 + colorRows.Length + maskRows.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), height * 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), (short)bits);
        if (table.Length > 0)
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), table.Length);
        for (var i = 0; i < table.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40 + i * 4), table[i] & 0xFFFFFF);
        colorRows.CopyTo(bytes, 40 + table.Length * 4);
        maskRows.CopyTo(bytes, 40 + table.Length * 4 + colorRows.Length);
        return bytes;
    }

    // A bitmap file: the file header, a BITMAPINFOHEADER of the given depth and compression, the bit fields or the color table given, then the pixels.
    public static byte[] Bitmap(int width, int height, int bits, int compression, uint[] table, byte[] pixels, int used = 0)
    {
        var offset = 14 + 40 + table.Length * 4;
        var bytes = new byte[offset + pixels.Length];
        "BM"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), offset);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), (short)bits);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(30), compression);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(46), used);
        for (var i = 0; i < table.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(54 + i * 4), table[i]);
        pixels.CopyTo(bytes, offset);
        return bytes;
    }

    // The picture cut short at every length, then the picture with a few of its bytes changed, the same ones on every run.
    public static IEnumerable<byte[]> Malformed(byte[] picture, int seed, int count)
    {
        for (var length = 0; length < picture.Length; length++)
            yield return picture[..length];
        var random = new Random(seed);
        for (var i = 0; i < count; i++)
        {
            var bytes = (byte[])picture.Clone();
            for (var change = random.Next(1, 5); change > 0; change--)
                bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
            yield return bytes;
        }
    }
}
