using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;

namespace RDCore.SDK.Workspace.Pictures;

// A PNG's pixels: its IDAT chunks inflated a row at a time, each row unfiltered and its samples taken to 8 bits.
internal static class PngDecoder
{
    private const int Gray = 0, Truecolor = 2, Indexed = 3, GrayAlpha = 4, TruecolorAlpha = 6;

    // the seven passes of Adam7 interlacing: where each starts across and down, and its steps.
    private static readonly (int X, int Y, int StepX, int StepY)[] Adam7 =
        [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)];

    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool IsPng(ReadOnlySpan<byte> b) => b.StartsWith(Signature);

    // the IHDR's bit depth, color type, compression, filter and interlace methods follow its width and height; a depth or a method no PNG has is refused.
    public static DecodedPicture? Decode(ReadOnlySpan<byte> b, int width, int height, out PictureDecodeFailure failure)
    {
        failure = PictureDecodeFailure.Refused;
        if (b.Length < 29)
        {
            return null;
        }

        int depth = b[24], type = b[25];
        var interlaced = b[28] == 1;
        var valid = (type, depth) switch
        {
            (Gray, 1 or 2 or 4 or 8 or 16) => true,
            (Indexed, 1 or 2 or 4 or 8) => true,
            (Truecolor or GrayAlpha or TruecolorAlpha, 8 or 16) => true,
            _ => false,
        };
        if (!valid || b[26] != 0 || b[27] != 0 || b[28] > 1)
        {
            return null;
        }

        // the chunks after the signature: a big-endian length, a type, the data and a checksum. A chunk cut short gives what it has, and ends the walk.
        using var data = new MemoryStream();
        ReadOnlySpan<byte> plte = default, trns = default;
        var at = 8;
        while (at + 8 <= b.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(b[at..]);
            var kind = b.Slice(at + 4, 4);
            var start = at + 8;
            var available = (int)Math.Min(length, (uint)(b.Length - start));
            var chunk = b.Slice(start, available);
            if (kind.SequenceEqual("IDAT"u8))
            {
                data.Write(chunk);
            }
            else if (kind.SequenceEqual("PLTE"u8))
            {
                plte = chunk;
            }
            else if (kind.SequenceEqual("tRNS"u8))
            {
                trns = chunk;
            }
            else if (kind.SequenceEqual("IEND"u8))
            {
                break;
            }

            if (available < length)
            {
                break;
            }

            at = start + available + 4;
        }

        var palette = ImmutableArray<uint>.Empty;
        if (type == Indexed)
        {
            var entries = ImmutableArray.CreateBuilder<uint>(Math.Min(256, plte.Length / 3));
            for (var i = 0; i < entries.Capacity; i++)
            {
                entries.Add(PictureDecoder.Argb(i < trns.Length ? trns[i] : (byte)0xFF, plte[i * 3], plte[i * 3 + 1], plte[i * 3 + 2]));
            }

            palette = entries.MoveToImmutable();
        }

        // tRNS of a gray or a truecolor picture: the one color, in samples of the picture's depth, that is transparent.
        int? clearGray = type == Gray && trns.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(trns) : null;
        (int, int, int)? clearColor = type == Truecolor && trns.Length >= 6
            ? (BinaryPrimitives.ReadUInt16BigEndian(trns), BinaryPrimitives.ReadUInt16BigEndian(trns[2..]), BinaryPrimitives.ReadUInt16BigEndian(trns[4..]))
            : null;

        var channels = type switch { Truecolor => 3, GrayAlpha => 2, TruecolorAlpha => 4, _ => 1 };
        var bitsPerPixel = channels * depth;
        var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        var bgra = new byte[width * height * 4];
        var indexes = type == Indexed ? new byte[width * height] : null;
        var complete = true;

        data.Position = 0;
        using var inflated = new ZLibStream(data, CompressionMode.Decompress);
        (int X, int Y, int StepX, int StepY)[] passes = interlaced ? Adam7 : [(0, 0, 1, 1)];
        foreach (var (startX, startY, stepX, stepY) in passes)
        {
            var across = (width - startX + stepX - 1) / stepX;
            var down = (height - startY + stepY - 1) / stepY;
            if (across <= 0 || down <= 0)
            {
                continue;
            }

            var rowBytes = (int)(((long)across * bitsPerPixel + 7) / 8);
            var previous = new byte[rowBytes];
            var row = new byte[rowBytes + 1];
            for (var r = 0; r < down && complete; r++)
            {
                if (Read(inflated, row) < row.Length || !Unfilter(row[0], row.AsSpan(1), previous, bytesPerPixel))
                {
                    complete = false;
                    break;
                }

                var y = startY + r * stepY;
                for (var i = 0; i < across; i++)
                {
                    var pixel = y * width + startX + i * stepX;
                    var line = row.AsSpan(1);
                    switch (type)
                    {
                        case Indexed:
                            var index = (byte)Sample(line, i, depth);
                            indexes![pixel] = index;
                            PictureDecoder.Put(bgra, pixel, index < palette.Length ? palette[index] : 0xFF000000);
                            break;
                        case Gray:
                            var gray = Sample(line, i, depth);
                            var g = Eight(gray, depth);
                            PictureDecoder.Put(bgra, pixel, PictureDecoder.Argb(gray == clearGray ? (byte)0 : (byte)0xFF, g, g, g));
                            break;
                        case GrayAlpha:
                            var ga = Eight(Sample(line, i * 2, depth), depth);
                            PictureDecoder.Put(bgra, pixel, PictureDecoder.Argb(Eight(Sample(line, i * 2 + 1, depth), depth), ga, ga, ga));
                            break;
                        default:
                            int red = Sample(line, i * channels, depth), green = Sample(line, i * channels + 1, depth),
                                blue = Sample(line, i * channels + 2, depth);
                            var opacity = type == TruecolorAlpha ? Eight(Sample(line, i * channels + 3, depth), depth)
                                : (red, green, blue) == clearColor ? (byte)0 : (byte)0xFF;
                            PictureDecoder.Put(bgra, pixel, PictureDecoder.Argb(opacity, Eight(red, depth), Eight(green, depth), Eight(blue, depth)));
                            break;
                    }
                }

                row.AsSpan(1).CopyTo(previous);
            }

            if (!complete)
            {
                break;
            }
        }

        failure = PictureDecodeFailure.None;
        return new DecodedPicture(width, height, bgra, palette, indexes, isComplete: complete);
    }

    // as many bytes as the stream gives up to the buffer's length; inflated data that is not valid ends it.
    private static int Read(Stream stream, byte[] buffer)
    {
        try
        {
            return stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        }
        catch (InvalidDataException)
        {
            return 0;
        }
    }

    // a row's filter undone, from the row above; false for a filter type no PNG has.
    private static bool Unfilter(byte filter, Span<byte> row, ReadOnlySpan<byte> above, int step)
    {
        for (var i = 0; i < row.Length; i++)
        {
            int left = i >= step ? row[i - step] : 0, up = above[i], upLeft = i >= step ? above[i - step] : 0;
            row[i] += filter switch
            {
                0 => 0,
                1 => (byte)left,
                2 => (byte)up,
                3 => (byte)((left + up) / 2),
                4 => (byte)Paeth(left, up, upLeft),
                _ => 0,
            };
        }

        return filter <= 4;
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    // the nth sample of a row, at the picture's depth: packed from the high bits for a depth under 8, big-endian for 16.
    private static int Sample(ReadOnlySpan<byte> row, int n, int depth) => depth switch
    {
        8 => row[n],
        16 => BinaryPrimitives.ReadUInt16BigEndian(row[(n * 2)..]),
        _ => row[n * depth / 8] >> 8 - depth - n * depth % 8 & (1 << depth) - 1,
    };

    // a sample taken to 8 bits: a depth under 8 scaled up, and 16 bits' high byte.
    private static byte Eight(int sample, int depth) => depth switch
    {
        8 => (byte)sample,
        16 => (byte)(sample >> 8),
        _ => (byte)(sample * 255 / ((1 << depth) - 1)),
    };
}
