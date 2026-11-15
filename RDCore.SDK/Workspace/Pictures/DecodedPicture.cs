using System.Collections.Immutable;

namespace RDCore.SDK.Workspace.Pictures;

/// <summary>
/// A picture's pixels, as <see cref="PictureDecoder"/> decodes them.
/// </summary>
/// <remarks>
/// <para>
/// Every picture has its pixels in <see cref="Bgra"/>. A picture of a format that numbers its colors (a GIF, a bitmap of 8 bits per pixel or fewer, a PNG
/// of indexed color) also keeps those numbers, in <see cref="Indexes"/>, and its color table, in <see cref="Palette"/>, so that it can be written with the
/// same table again. An icon's or a cursor's image keeps its AND mask, in <see cref="Mask"/>.
/// </para>
/// <para>
/// A pixel the file does not give, past the end of a body cut short, is 0: black and transparent in <see cref="Bgra"/>, index 0 in
/// <see cref="Indexes"/>, and no mask. <see cref="IsComplete"/> tells whether the body was cut short.
/// </para>
/// </remarks>
public sealed class DecodedPicture
{
    internal DecodedPicture(int width, int height, byte[] bgra, ImmutableArray<uint> palette = default, byte[]? indexes = null, int transparentIndex = -1,
        byte[]? mask = null, bool isComplete = true, bool hasAlphaChannel = false)
    {
        HasAlphaChannel = hasAlphaChannel;
        Width = width;
        Height = height;
        Bgra = bgra;
        Palette = palette.IsDefault ? [] : palette;
        Indexes = indexes is null ? (ReadOnlyMemory<byte>?)null : indexes;
        TransparentIndex = transparentIndex;
        Mask = mask is null ? (ReadOnlyMemory<byte>?)null : mask;
        IsComplete = isComplete;
    }

    /// <summary>
    /// The picture's width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// The picture's height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// The pixels, 4 bytes each in the order blue, green, red, alpha, with the alpha not premultiplied; row by row from the top, <see cref="Width"/> times 4
    /// bytes to a row.
    /// </summary>
    public ReadOnlyMemory<byte> Bgra { get; }

    /// <summary>
    /// The color table of a picture whose pixels number their colors, each entry as <c>0xAARRGGBB</c>; empty for any other picture, and for a GIF that has
    /// no table.
    /// </summary>
    public ImmutableArray<uint> Palette { get; }

    /// <summary>
    /// Each pixel's number in <see cref="Palette"/>, one byte each and row by row from the top; <see langword="null"/> for a picture that does not number its
    /// colors. A number past the end of the palette is drawn black.
    /// </summary>
    public ReadOnlyMemory<byte>? Indexes { get; }

    /// <summary>
    /// The number of a GIF's transparent color, or -1 for none.
    /// </summary>
    public int TransparentIndex { get; }

    /// <summary>
    /// An icon's or a cursor's AND mask, one byte for each pixel and row by row from the top: 1 where the mask is set, which shows what is under the image, 0
    /// elsewhere. <see langword="null"/> for a picture with no mask, a PNG image of an icon included.
    /// </summary>
    public ReadOnlyMemory<byte>? Mask { get; }

    /// <summary>
    /// Whether the file gave every pixel; <see langword="false"/> where its body was cut short or could not be read to its end.
    /// </summary>
    public bool IsComplete { get; }

    // a bitmap whose bit fields state an alpha field, a 32-bit icon image whose alpha is not all 0, or a GIF one of whose pixels is its transparent color:
    // its alpha is its own, which a bitmap of 24 bits or fewer, or an icon of 16 colors and a mask, would lose. Set by the bitmap and GIF decoders; a PNG is
    // always written with its alpha.
    internal bool HasAlphaChannel { get; }
}
