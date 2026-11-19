using System.Buffers.Binary;
using System.Collections.Immutable;

namespace RDCore.SDK.Workspace.Pictures;

// A GIF's first frame, LZW-decoded at its own size: the logical screen's size and the frame's place on it are not part of the picture.
internal static class GifDecoder
{
    private const int MaxCodes = 4096;

    // the screen descriptor after the 6-byte signature: its width and height, flags (a global table, and its size), a background color and an aspect; then
    // the global table; then blocks: extensions (0x21), an image (0x2C), the trailer (0x3B). The width and the height are the first frame's.
    public static DecodedPicture Decode(ReadOnlySpan<byte> b, int width, int height)
    {
        var indexes = new byte[width * height];
        var given = new bool[width * height];
        var at = 13;
        var global = b.Length > 10 && (b[10] & 0x80) != 0 ? Table(b, ref at, b[10]) : [];
        var transparent = -1;
        var palette = global;
        var complete = false;
        while (at < b.Length)
        {
            var block = b[at];
            if (block == 0x21 && at + 1 < b.Length)
            {
                // a graphic control extension: a block of 4 bytes whose first holds the transparency flag and whose last is the transparent color.
                if (b[at + 1] == 0xF9 && at + 7 <= b.Length && b[at + 2] == 4)
                {
                    transparent = (b[at + 3] & 1) != 0 ? b[at + 6] : -1;
                }

                at = SkipSubBlocks(b, at + 2);
                continue;
            }

            if (block != 0x2C || at + 10 > b.Length)
            {
                break;
            }

            // the image descriptor: its place on the screen, which is ignored, and its size, then flags (a local table and its size, interlacing).
            int across = U16(b, at + 5), down = U16(b, at + 7);
            var flags = b[at + 9];
            at += 10;
            if ((flags & 0x80) != 0)
            {
                palette = Table(b, ref at, flags);
            }

            if (at < b.Length)
            {
                var minimum = b[at];
                var data = SubBlocks(b, at + 1);
                complete = Lzw(data, minimum, across, down, (flags & 0x40) != 0, (x, y, index) =>
                {
                    if (x < width && y < height)
                    {
                        var pixel = y * width + x;
                        indexes[pixel] = index;
                        given[pixel] = true;
                    }
                });
            }

            break;
        }

        var bgra = new byte[width * height * 4];
        var clear = false;
        for (var i = 0; i < indexes.Length; i++)
        {
            if (given[i])
            {
                clear |= indexes[i] == transparent;
                PictureDecoder.Put(bgra, i, indexes[i] == transparent ? 0 : indexes[i] < palette.Length ? palette[indexes[i]] : 0xFF000000);
            }
        }

        // a GIF's alpha is its own where one of its pixels is its transparent color.
        return new DecodedPicture(width, height, bgra, palette, indexes, transparent, isComplete: complete, hasAlphaChannel: clear);
    }

    // a color table of 2 to the power of the flags' low 3 bits plus 1 entries, 3 bytes each; an entry past the end of the bytes is black.
    private static ImmutableArray<uint> Table(ReadOnlySpan<byte> b, ref int at, byte flags)
    {
        var count = 2 << (flags & 7);
        var table = ImmutableArray.CreateBuilder<uint>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = at + i * 3;
            table.Add(entry + 3 <= b.Length ? PictureDecoder.Argb(0xFF, b[entry], b[entry + 1], b[entry + 2]) : 0xFF000000);
        }

        at += count * 3;
        return table.MoveToImmutable();
    }

    // sub-blocks: a length, then that many bytes, until a length of 0. Where the bytes end first, so does the walk.
    private static int SkipSubBlocks(ReadOnlySpan<byte> b, int at)
    {
        while (at < b.Length && b[at] != 0)
        {
            at += 1 + b[at];
        }

        return at + 1;
    }

    private static byte[] SubBlocks(ReadOnlySpan<byte> b, int at)
    {
        using var data = new MemoryStream();
        while (at < b.Length && b[at] != 0)
        {
            data.Write(b.Slice(at + 1, Math.Min(b[at], b.Length - at - 1)));
            at += 1 + b[at];
        }

        return data.ToArray();
    }

    // the frame's pixels from its LZW codes, read from the low bit up: a clear code resets the table, and the end code ends the frame. Each pixel goes to the
    // frame's next place, row by row, or in the four passes of an interlaced frame. No more pixels are decoded than the frame has. Whether it was filled.
    private static bool Lzw(byte[] data, int minimum, int across, int down, bool interlaced, Action<int, int, byte> put)
    {
        var total = (long)across * down;
        if (total == 0)
        {
            return true;
        }

        if (minimum is < 2 or > 8)
        {
            return false;
        }

        var rows = new int[down];
        var n = 0;
        (int Start, int Step)[] passes = interlaced ? [(0, 8), (4, 8), (2, 4), (1, 2)] : [(0, 1)];
        foreach (var (start, step) in passes)
        {
            for (var y = start; y < down; y += step)
            {
                rows[n++] = y;
            }
        }

        int clear = 1 << minimum, end = clear + 1, next = clear + 2, size = minimum + 1, old = -1, first = 0;
        var prefix = new short[MaxCodes];
        var suffix = new byte[MaxCodes];
        var stack = new byte[MaxCodes + 1];
        for (var i = 0; i < clear; i++)
        {
            suffix[i] = (byte)i;
        }

        long written = 0, bit = 0;
        var bits = (long)data.Length * 8;
        while (written < total && bit + size <= bits)
        {
            var code = 0;
            for (var i = 0; i < size; i++, bit++)
            {
                code |= (data[bit >> 3] >> (int)(bit & 7) & 1) << i;
            }

            if (code == clear)
            {
                (next, size, old) = (clear + 2, minimum + 1, -1);
                continue;
            }

            if (code == end)
            {
                break;
            }

            var top = 0;
            var current = code;
            if (old == -1)
            {
                if (code >= clear)
                {
                    break;
                }

                stack[top++] = (byte)code;
                first = code;
            }
            else
            {
                if (code > next || code == next && next >= MaxCodes)
                {
                    break;
                }

                if (code == next)
                {
                    stack[top++] = (byte)first;
                    current = old;
                }

                while (current >= clear)
                {
                    stack[top++] = suffix[current];
                    current = prefix[current];
                }

                first = current;
                stack[top++] = (byte)first;
                if (next < MaxCodes)
                {
                    prefix[next] = (short)old;
                    suffix[next] = (byte)first;
                    next++;
                    if (next == 1 << size && size < 12)
                    {
                        size++;
                    }
                }
            }

            while (top > 0 && written < total)
            {
                put((int)(written % across), rows[written / across], stack[--top]);
                written++;
            }

            old = code;
        }

        return written == total;
    }

    private static ushort U16(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);
}
