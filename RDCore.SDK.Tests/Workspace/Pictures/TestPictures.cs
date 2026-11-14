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
