// Derived from HexIDE (https://github.com/hexide-io/HexIDE), commit ae17f1db.
// Copyright (c) 2024 Bartosz Korczyński. Copyright (c) 2026 The HexIDE Authors. MIT License; see NOTICE.md.

using System.Buffers.Binary;
using System.Collections.Immutable;

namespace RDCore.SDK.Workspace.Pictures;

/// <summary>
/// The file format of a picture: what a <c>StdPicture</c> holds, or a form's <c>Picture</c> or <c>Icon</c>.
/// </summary>
public enum PictureFormat
{
    /// <summary>
    /// A Windows bitmap (<c>.bmp</c>).
    /// </summary>
    Bitmap,

    /// <summary>
    /// A Windows icon (<c>.ico</c>).
    /// </summary>
    Icon,

    /// <summary>
    /// A Windows cursor (<c>.cur</c>).
    /// </summary>
    Cursor,

    /// <summary>
    /// A GIF image.
    /// </summary>
    Gif,

    /// <summary>
    /// A JPEG image.
    /// </summary>
    Jpeg,

    /// <summary>
    /// A PNG image.
    /// </summary>
    Png,

    /// <summary>
    /// A Windows metafile (<c>.wmf</c>), placeable or not.
    /// </summary>
    Metafile,

    /// <summary>
    /// An enhanced metafile (<c>.emf</c>).
    /// </summary>
    EnhancedMetafile,
}

/// <summary>
/// What a picture's header says about it: its format and its size in pixels.
/// </summary>
/// <param name="Format">The picture's file format.</param>
/// <param name="Width">The picture's width in pixels, or 0 for a metafile, which has no size in pixels.</param>
/// <param name="Height">The picture's height in pixels, or 0 for a metafile.</param>
public readonly record struct PictureInfo(PictureFormat Format, int Width, int Height)
{
    /// <summary>
    /// Whether the picture has a size in pixels: every format but the metafiles, which are drawn by records and have none.
    /// </summary>
    public bool HasPixelSize => Format is not (PictureFormat.Metafile or PictureFormat.EnhancedMetafile);

    /// <summary>
    /// The picture's width in HIMETRIC units (0.01 mm), which is what a <c>StdPicture</c>'s <c>Width</c> reads.
    /// </summary>
    /// <remarks>
    /// A picture with a size in pixels is measured at 96 pixels per inch, rounded to the nearest unit, whatever resolution its file states. A placeable
    /// metafile's size is its bounding box at the units per inch its header states; an enhanced metafile's is its frame. A metafile that is not placeable
    /// states no size, and its size is 0.
    /// </remarks>
    public int HimetricWidth { get; init; }

    /// <summary>
    /// The picture's height in HIMETRIC units (0.01 mm), measured as <see cref="HimetricWidth"/> is.
    /// </summary>
    public int HimetricHeight { get; init; }

    /// <summary>
    /// A length in pixels, in HIMETRIC units (0.01 mm) at 96 pixels per inch, rounded to the nearest unit.
    /// </summary>
    /// <param name="pixels">The length in pixels.</param>
    /// <returns>The length in HIMETRIC units.</returns>
    public static int PixelsToHimetric(int pixels) => Scale(pixels, PictureHeader.HimetricPerInch, PictureHeader.PixelsPerInch);

    // a * b / c, rounded to the nearest, half away from zero.
    internal static int Scale(long a, long b, long c)
    {
        var product = a * b;
        return (int)((product + (product < 0 ? -c / 2 : c / 2)) / c);
    }
}

/// <summary>
/// One of the images of an icon or a cursor, as its directory entry and its own header state it.
/// </summary>
/// <param name="Width">The image's width in pixels, as its own header states it.</param>
/// <param name="Height">The image's height in pixels, without the mask a DIB image holds under it.</param>
/// <param name="BitCount">The image's bits per pixel: its DIB header's, or 32 for an image stored as a PNG.</param>
/// <param name="Offset">Where the image starts in the file.</param>
/// <param name="Length">How many bytes the image has.</param>
public readonly record struct IconImage(int Width, int Height, int BitCount, int Offset, int Length);

/// <summary>
/// Reads what a picture's header says about it before anything decodes it, and refuses a picture too large or too malformed to decode safely.
/// </summary>
/// <remarks>
/// <para>
/// A picture's bytes come from a file the user opened, so they may be anything. <see cref="TryRead"/> reads only the format's header, with every offset checked
/// against the bytes, and never throws. It refuses a picture:
/// </para>
/// <list type="bullet">
/// <item>of no bytes, or more than <see cref="MaxBytes"/>, before reading anything;</item>
/// <item>of a format it does not recognize, or whose header is cut short or holds an impossible value;</item>
/// <item>with a size in pixels (<see cref="PictureInfo.HasPixelSize"/>) wider or taller than <see cref="MaxSide"/>, or of more than <see cref="MaxPixels"/>.
/// An icon or a cursor is refused when any of its images is, a PNG image inside one included.</item>
/// </list>
/// <para>
/// A decoder given only the pictures <see cref="TryRead"/> accepts allocates at most <see cref="MaxPixels"/> pixels for one. A refused picture is what VBA
/// reports as run-time error 481, "Invalid picture".
/// </para>
/// </remarks>
public static class PictureHeader
{
    /// <summary>
    /// The most bytes a picture may have: 16 MiB.
    /// </summary>
    public const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>
    /// The most pixels a picture may have across or down: 16384.
    /// </summary>
    public const int MaxSide = 16384;

    /// <summary>
    /// The most pixels a picture may have in all: 16,777,216, which is 64 MiB at 4 bytes a pixel.
    /// </summary>
    public const int MaxPixels = 16 * 1024 * 1024;

    /// <summary>
    /// The HIMETRIC units in an inch: 2540, since a unit is 0.01 mm.
    /// </summary>
    public const int HimetricPerInch = 2540;

    /// <summary>
    /// The pixels per inch a picture with a size in pixels is measured at: 96.
    /// </summary>
    public const int PixelsPerInch = 96;

    // the bytes' length already bounds how many directory entries an icon can hold; this bounds the walk as well.
    private const int MaxIconImages = 1024;

    /// <summary>
    /// Reads a picture's header.
    /// </summary>
    /// <param name="bytes">The picture, as its file holds it.</param>
    /// <param name="info">What the header says when the picture is accepted; <see langword="default"/> when it is refused.</param>
    /// <returns>
    /// <see langword="true"/> when the picture is accepted; <see langword="false"/> for no bytes, too many, a format not recognized, a malformed header, or a
    /// size past <see cref="MaxSide"/> or <see cref="MaxPixels"/>.
    /// </returns>
    public static bool TryRead(ReadOnlySpan<byte> bytes, out PictureInfo info)
    {
        info = default;
        if (bytes.IsEmpty || bytes.Length > MaxBytes)
        {
            return false;
        }

        if (Read(bytes) is not { } found || found.HasPixelSize && !WithinBounds(found.Width, found.Height))
        {
            return false;
        }

        var (width, height) = HimetricSizeOf(found, bytes);
        info = found with { HimetricWidth = width, HimetricHeight = height };
        return true;
    }

    /// <summary>
    /// Reads the images of an icon or a cursor, in the order its directory lists them.
    /// </summary>
    /// <param name="bytes">The icon or the cursor, as its file holds it.</param>
    /// <param name="images">The images when <see cref="TryRead"/> accepts the picture and it is an icon or a cursor; empty otherwise.</param>
    /// <returns>
    /// <see langword="true"/> for an icon or a cursor that <see cref="TryRead"/> accepts; <see langword="false"/> for anything else.
    /// </returns>
    public static bool TryReadIconImages(ReadOnlySpan<byte> bytes, out ImmutableArray<IconImage> images)
    {
        images = [];
        if (!TryRead(bytes, out var info) || info.Format is not (PictureFormat.Icon or PictureFormat.Cursor) || IconImages(bytes) is not { } found)
        {
            return false;
        }

        images = found;
        return true;
    }

    private static (int Width, int Height) HimetricSizeOf(PictureInfo info, ReadOnlySpan<byte> b)
    {
        if (info.HasPixelSize)
        {
            return (PictureInfo.PixelsToHimetric(info.Width), PictureInfo.PixelsToHimetric(info.Height));
        }

        // a placeable metafile's header: its key, a handle, its bounding box (left, top, right, bottom) and its units per inch, as 16-bit numbers.
        if (info.Format is PictureFormat.Metafile)
        {
            if (b.Length < 16 || !b.StartsWith((ReadOnlySpan<byte>)[0xD7, 0xCD, 0xC6, 0x9A]) || U16(b, 14) is not (> 0 and var inch))
            {
                return (0, 0);
            }

            var across = Math.Abs(I16(b, 10) - I16(b, 6));
            var down = Math.Abs(I16(b, 12) - I16(b, 8));
            return (PictureInfo.Scale(across, HimetricPerInch, inch), PictureInfo.Scale(down, HimetricPerInch, inch));
        }

        // an enhanced metafile's header: its bounds in device units, then its frame (left, top, right, bottom) in HIMETRIC units.
        var left = BinaryPrimitives.ReadInt32LittleEndian(b[24..]);
        var top = BinaryPrimitives.ReadInt32LittleEndian(b[28..]);
        var right = BinaryPrimitives.ReadInt32LittleEndian(b[32..]);
        var bottom = BinaryPrimitives.ReadInt32LittleEndian(b[36..]);
        return ((int)Math.Min(int.MaxValue, Math.Abs((long)right - left)), (int)Math.Min(int.MaxValue, Math.Abs((long)bottom - top)));
    }

    private static bool WithinBounds(int width, int height) =>
        width is > 0 and <= MaxSide && height is > 0 and <= MaxSide && (long)width * height <= MaxPixels;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static PictureInfo? Read(ReadOnlySpan<byte> b)
    {
        if (b.StartsWith("BM"u8))
        {
            return Bitmap(b);
        }

        if (b.StartsWith((ReadOnlySpan<byte>)[0, 0, 1, 0]))
        {
            return Icon(b, PictureFormat.Icon);
        }

        if (b.StartsWith((ReadOnlySpan<byte>)[0, 0, 2, 0]))
        {
            return Icon(b, PictureFormat.Cursor);
        }

        if (b.StartsWith("GIF87a"u8) || b.StartsWith("GIF89a"u8))
        {
            return b.Length < 10 ? null : new PictureInfo(PictureFormat.Gif, U16(b, 6), U16(b, 8));
        }

        if (b.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8]))
        {
            return Jpeg(b);
        }

        if (b.StartsWith(PngSignature))
        {
            return Png(b);
        }

        // a placeable metafile's key, or a plain metafile's header: its type (1 or 2), its 9-word size, and its version.
        if (b.StartsWith((ReadOnlySpan<byte>)[0xD7, 0xCD, 0xC6, 0x9A])
            || b.Length >= 6 && (b[0] is 1 or 2) && b[1] == 0 && b[2] == 9 && b[3] == 0 && (U16(b, 4) is 0x100 or 0x300))
        {
            return new PictureInfo(PictureFormat.Metafile, 0, 0);
        }

        // an enhanced metafile: an EMR_HEADER record, its signature " EMF" at 40.
        if (b.Length >= 44 && b.StartsWith((ReadOnlySpan<byte>)[1, 0, 0, 0]) && b.Slice(40, 4).SequenceEqual(" EMF"u8))
        {
            return new PictureInfo(PictureFormat.EnhancedMetafile, 0, 0);
        }

        return null;
    }

    // a bitmap file header (14 bytes), then a DIB header.
    private static PictureInfo? Bitmap(ReadOnlySpan<byte> b) =>
        b.Length >= 14 && U32(b, 10) < (uint)b.Length && Dib(b[14..]) is { } size
            ? new PictureInfo(PictureFormat.Bitmap, size.Width, size.Height)
            : null;

    // a DIB header's size: a BITMAPCOREHEADER's 16-bit width and height, or a BITMAPINFOHEADER's (or later's) 32-bit ones, the height negative for a
    // top-down bitmap. An icon's DIB holds its image and its mask, so its height is twice the image's.
    private static (int Width, int Height)? Dib(ReadOnlySpan<byte> b, bool icon = false)
    {
        if (b.Length < 4)
        {
            return null;
        }

        var size = U32(b, 0);
        if (size == 12)
        {
            return b.Length < 12 ? null : (U16(b, 4), icon ? U16(b, 6) / 2 : U16(b, 6));
        }

        if (size < 40 || size > (uint)b.Length)
        {
            return null;
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(b[4..]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(b[8..]);
        if (width <= 0 || height == int.MinValue)
        {
            return null;
        }

        height = Math.Abs(height);
        return (width, icon ? height / 2 : height);
    }

    // a 6-byte header (reserved, type, count), then 16 bytes for each image: its width and height (0 for 256), and its size and offset in the file. Each
    // image is a PNG or a DIB. The size is the largest image's; every image must be within the bounds.
    private static PictureInfo? Icon(ReadOnlySpan<byte> b, PictureFormat format)
    {
        if (IconImages(b) is not { } images)
        {
            return null;
        }

        int width = 0, height = 0;
        foreach (var image in images)
        {
            if ((long)image.Width * image.Height > (long)width * height)
            {
                (width, height) = (image.Width, image.Height);
            }
        }

        return new PictureInfo(format, width, height);
    }

    private static ImmutableArray<IconImage>? IconImages(ReadOnlySpan<byte> b)
    {
        if (b.Length < 6)
        {
            return null;
        }

        var count = U16(b, 4);
        if (count == 0 || count > MaxIconImages || 6 + 16 * count > b.Length)
        {
            return null;
        }

        var images = ImmutableArray.CreateBuilder<IconImage>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = b.Slice(6 + 16 * i, 16);
            var length = U32(entry, 8);
            var offset = U32(entry, 12);
            if (length == 0 || offset > (uint)b.Length || length > (uint)b.Length - offset)
            {
                return null;
            }

            var image = b.Slice((int)offset, (int)length);
            var png = Png(image);
            (int Width, int Height)? size = png is { } stored ? (stored.Width, stored.Height) : Dib(image, icon: true);
            if (size is not { } found || !WithinBounds(found.Width, found.Height))
            {
                return null;
            }

            // a DIB's bits per pixel: a BITMAPCOREHEADER's at 10, a later header's at 14.
            var bits = png is not null ? 32 : U32(image, 0) == 12 ? U16(image, 10) : U16(image, 14);
            images.Add(new IconImage(found.Width, found.Height, bits, (int)offset, (int)length));
        }

        return images.MoveToImmutable();
    }

    // the signature, then the IHDR chunk: its length, "IHDR", and the width and height as big-endian 32-bit numbers.
    private static PictureInfo? Png(ReadOnlySpan<byte> b)
    {
        if (b.Length < 24 || !b.StartsWith(PngSignature) || !b.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(b[16..]);
        var height = BinaryPrimitives.ReadInt32BigEndian(b[20..]);
        return width > 0 && height > 0 ? new PictureInfo(PictureFormat.Png, width, height) : null;
    }

    // markers from the start of image: each one 0xFF and a code, with a big-endian length that counts itself, until a start of frame, whose header holds the
    // height and the width. Each step moves on by at least 2 bytes, so the walk ends.
    private static PictureInfo? Jpeg(ReadOnlySpan<byte> b)
    {
        var at = 2;
        while (at + 4 <= b.Length)
        {
            if (b[at] != 0xFF)
            {
                return null;
            }

            var code = b[at + 1];

            // a fill byte before a marker.
            if (code == 0xFF)
            {
                at++;
                continue;
            }

            // a marker with no length: a restart or a TEM. A start of scan or an end of image before a frame: no size.
            if (code is >= 0xD0 and <= 0xD7 or 0x01)
            {
                at += 2;
                continue;
            }

            if (code is 0xD9 or 0xDA)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(b[(at + 2)..]);
            if (length < 2 || at + 2 + length > b.Length)
            {
                return null;
            }

            // a start of frame: C0 to CF, but for C4 (Huffman tables), C8 (reserved) and CC (arithmetic coding).
            if (code is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                return length < 7
                    ? null
                    : new PictureInfo(PictureFormat.Jpeg, BinaryPrimitives.ReadUInt16BigEndian(b[(at + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(b[(at + 5)..]));
            }

            at += 2 + length;
        }

        return null;
    }

    private static ushort U16(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);

    private static short I16(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadInt16LittleEndian(b[at..]);

    private static uint U32(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
}
