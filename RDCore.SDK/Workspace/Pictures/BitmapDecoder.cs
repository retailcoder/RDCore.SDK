using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace RDCore.SDK.Workspace.Pictures;

// A bitmap file's pixels, or an icon's DIB image and its AND mask.
internal static class BitmapDecoder
{
    private const int CoreHeader = 12;
    private const int InfoHeader = 40;
    private const uint Rgb = 0, Rle8 = 1, Rle4 = 2, BitFields = 3, AlphaBitFields = 6;

    // a bitmap file: a 14-byte file header, whose offset at 10 is where the pixels start, then a DIB.
    public static DecodedPicture? File(ReadOnlySpan<byte> b, out PictureDecodeFailure failure) =>
        Dib(b, 14, (int)BinaryPrimitives.ReadUInt32LittleEndian(b[10..]), icon: false, out failure);

    // whether a bitmap file of uncompressed pixels, with no bit fields, ends before the last of the rows its header states.
    public static bool RowsCutShort(ReadOnlySpan<byte> b)
    {
        const int dib = 14;
        if (b.Length < dib + CoreHeader)
        {
            return false;
        }

        var size = U32(b, dib);
        var core = size == CoreHeader;
        if (!core && (size < InfoHeader || b.Length < dib + InfoHeader || U32(b, dib + 16) != Rgb))
        {
            return false;
        }

        long width = Math.Abs((long)(core ? U16(b, dib + 4) : BinaryPrimitives.ReadInt32LittleEndian(b[(dib + 4)..])));
        long height = Math.Abs((long)(core ? U16(b, dib + 6) : BinaryPrimitives.ReadInt32LittleEndian(b[(dib + 8)..])));
        var bits = U16(b, dib + (core ? 10 : 14));
        return U32(b, 10) + (width * bits + 31) / 32 * 4 * height > b.Length;
    }

    // a DIB at dib: its header, its bit fields and its color table, then its pixels at pixels, or right after the table where pixels is -1. An icon's DIB is
    // twice its image's height: the image, then its AND mask.
    public static DecodedPicture? Dib(ReadOnlySpan<byte> b, int dib, int pixels, bool icon, out PictureDecodeFailure failure)
    {
        failure = PictureDecodeFailure.Refused;
        if (dib + 4 > b.Length)
        {
            return null;
        }

        var size = U32(b, dib);
        var core = size == CoreHeader;
        if (!core && (size < InfoHeader || dib + (long)size > b.Length) || core && dib + CoreHeader > b.Length)
        {
            return null;
        }

        int width, height, bits;
        uint compression = Rgb, used = 0;
        bool bottomUp = true;
        if (core)
        {
            width = U16(b, dib + 4);
            height = U16(b, dib + 6);
            bits = U16(b, dib + 10);
        }
        else
        {
            width = BinaryPrimitives.ReadInt32LittleEndian(b[(dib + 4)..]);
            height = BinaryPrimitives.ReadInt32LittleEndian(b[(dib + 8)..]);
            bits = U16(b, dib + 14);
            compression = U32(b, dib + 16);
            used = U32(b, dib + 32);
            if (height < 0 && height != int.MinValue)
            {
                bottomUp = false;
                height = -height;
            }
        }

        if (icon)
        {
            height /= 2;
        }

        if (width <= 0 || height <= 0 || width > PictureHeader.MaxSide || height > PictureHeader.MaxSide || (long)width * height > PictureHeader.MaxPixels)
        {
            return null;
        }

        failure = PictureDecodeFailure.NotDecoded;
        var supported = (bits, compression) switch
        {
            (1 or 4 or 8 or 16 or 24 or 32, Rgb) => true,
            (8, Rle8) or (4, Rle4) => true,
            (16 or 32, BitFields or AlphaBitFields) => true,
            _ => false,
        };
        if (!supported)
        {
            return null;
        }

        // the bit fields: in the header from 40 where it is long enough, or else just after it.
        var after = dib + (int)size;
        uint red = 0x7C00, green = 0x3E0, blue = 0x1F, alpha = 0;
        if (bits == 32)
        {
            (red, green, blue, alpha) = (0xFF0000, 0xFF00, 0xFF, icon ? 0xFF000000 : 0);
        }

        if (compression is BitFields or AlphaBitFields)
        {
            var fields = size >= 52 ? dib + 40 : after;
            var count = compression == AlphaBitFields || size >= 56 ? 4 : 3;
            if (fields + 4 * count > b.Length)
            {
                failure = PictureDecodeFailure.Refused;
                return null;
            }

            (red, green, blue) = (U32(b, fields), U32(b, fields + 4), U32(b, fields + 8));
            alpha = count == 4 ? U32(b, fields + 12) : 0;
            if (size < 52)
            {
                after += 4 * count;
            }
        }

        // the color table, of the colors the header says it uses or else of every color the depth numbers: 3 bytes an entry after a core header, 4 after
        // another. An entry past the end of the bytes is black.
        var palette = ImmutableArray<uint>.Empty;
        if (bits <= 8)
        {
            var colors = used > 0 && used < 1u << bits ? (int)used : 1 << bits;
            var entry = core ? 3 : 4;
            var table = ImmutableArray.CreateBuilder<uint>(colors);
            for (var i = 0; i < colors; i++)
            {
                var at = after + i * entry;
                table.Add(at + 3 <= b.Length ? PictureDecoder.Argb(0xFF, b[at + 2], b[at + 1], b[at]) : 0xFF000000);
            }

            palette = table.MoveToImmutable();
            after += colors * entry;
        }

        var start = pixels < 0 ? after : pixels;
        var complete = true;
        var indexes = bits <= 8 ? new byte[width * height] : null;
        var bgra = new byte[width * height * 4];
        var stride = (int)(((long)width * bits + 31) / 32 * 4);
        var end = start;
        if (compression is Rle8 or Rle4)
        {
            complete = Rle(b, start, width, height, compression == Rle4, indexes!, bottomUp, out end);
        }
        else
        {
            for (var row = 0; row < height; row++)
            {
                var at = start + (long)row * stride;
                if (at + stride > b.Length)
                {
                    complete = false;
                    break;
                }

                var line = b.Slice((int)at, stride);
                var y = bottomUp ? height - 1 - row : row;
                for (var x = 0; x < width; x++)
                {
                    var pixel = y * width + x;
                    switch (bits)
                    {
                        case <= 8:
                            var bit = x * bits;
                            indexes![pixel] = (byte)(line[bit / 8] >> 8 - bits - bit % 8 & (1 << bits) - 1);
                            break;
                        case 16:
                            Fields(bgra, pixel, U16(line, x * 2), red, green, blue, alpha);
                            break;
                        case 24:
                            PictureDecoder.Put(bgra, pixel, PictureDecoder.Argb(0xFF, line[x * 3 + 2], line[x * 3 + 1], line[x * 3]));
                            break;
                        default:
                            Fields(bgra, pixel, U32(line, x * 4), red, green, blue, alpha);
                            break;
                    }
                }
            }

            end = (int)Math.Min(b.Length, start + (long)stride * height);
        }

        if (indexes is not null)
        {
            bgra = PictureDecoder.Colors(indexes, palette);
        }

        if (!icon)
        {
            // a bitmap's alpha is its own only where its bit fields state an alpha field.
            return new DecodedPicture(width, height, bgra, palette, indexes, isComplete: complete, hasAlphaChannel: alpha != 0);
        }

        // the AND mask under the image: 1 bit a pixel, its rows padded to 4 bytes, in the image's order. Where a 32-bit image's alpha is not all 0, it is the
        // image's transparency; otherwise the mask is.
        var mask = new byte[width * height];
        var maskStride = (width + 31) / 32 * 4;
        for (var row = 0; row < height; row++)
        {
            var at = end + (long)row * maskStride;
            if (at + maskStride > b.Length)
            {
                complete = false;
                break;
            }

            var y = bottomUp ? height - 1 - row : row;
            for (var x = 0; x < width; x++)
            {
                mask[y * width + x] = (byte)(b[(int)at + x / 8] >> 7 - x % 8 & 1);
            }
        }

        var hasAlpha = false;
        if (bits == 32)
        {
            for (var i = 3; i < bgra.Length && !hasAlpha; i += 4)
            {
                hasAlpha = bgra[i] != 0;
            }
        }

        if (!hasAlpha)
        {
            for (var i = 0; i < mask.Length; i++)
            {
                bgra[i * 4 + 3] = mask[i] == 0 ? (byte)0xFF : (byte)0;
            }
        }

        return new DecodedPicture(width, height, bgra, palette, indexes, mask: mask, isComplete: complete, hasAlphaChannel: hasAlpha);
    }

    // a pixel of 16 or 32 bits, through its bit fields, each scaled to 8 bits. With no alpha field, the pixel is opaque; a 32-bit icon image's fourth byte is
    // its alpha.
    private static void Fields(Span<byte> bgra, int pixel, uint value, uint red, uint green, uint blue, uint alpha)
    {
        var at = pixel * 4;
        bgra[at] = Field(value, blue);
        bgra[at + 1] = Field(value, green);
        bgra[at + 2] = Field(value, red);
        bgra[at + 3] = alpha == 0 ? (byte)0xFF : Field(value, alpha);
    }

    private static byte Field(uint value, uint mask)
    {
        if (mask == 0)
        {
            return 0;
        }

        var shift = BitOperations.TrailingZeroCount(mask);
        var max = mask >> shift;
        return (byte)((ulong)((value & mask) >> shift) * 255 / max);
    }

    // RLE8 or RLE4 pixels, from the bottom row up (or the top down, for a header that says so): a count and a value; or 0 and an escape: 0 ends the row, 1
    // ends the bitmap, 2 moves by the next two bytes, and n of 3 or more is n pixels as they are, padded to 2 bytes. A pixel past the bitmap's edge is not
    // drawn. Each step reads at least 2 bytes, so the walk ends. Whether the bitmap's end was reached.
    private static bool Rle(ReadOnlySpan<byte> b, int at, int width, int height, bool four, byte[] indexes, bool bottomUp, out int end)
    {
        int x = 0, row = 0;
        void Set(int value)
        {
            if (x < width && row < height)
            {
                indexes[(bottomUp ? height - 1 - row : row) * width + x] = (byte)value;
            }

            x++;
        }

        while (at + 2 <= b.Length)
        {
            var count = b[at];
            var value = b[at + 1];
            at += 2;
            if (count > 0)
            {
                for (var i = 0; i < count && x < width; i++)
                {
                    Set(four ? (i % 2 == 0 ? value >> 4 : value & 0xF) : value);
                }

                continue;
            }

            switch (value)
            {
                case 0:
                    (x, row) = (0, row + 1);
                    break;
                case 1:
                    end = at;
                    return true;
                case 2:
                    if (at + 2 > b.Length)
                    {
                        end = at;
                        return false;
                    }

                    (x, row) = (x + b[at], row + b[at + 1]);
                    at += 2;
                    break;
                default:
                    var length = four ? (value + 1) / 2 : value;
                    for (var i = 0; i < value && at + (four ? i / 2 : i) < b.Length; i++)
                    {
                        var data = b[at + (four ? i / 2 : i)];
                        Set(four ? (i % 2 == 0 ? data >> 4 : data & 0xF) : data);
                    }

                    at += length + length % 2;
                    break;
            }

            if (row >= height)
            {
                end = Math.Min(at, b.Length);
                return true;
            }
        }

        end = Math.Min(at, b.Length);
        return false;
    }

    private static ushort U16(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);

    private static uint U32(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
}
