using System.Collections.Immutable;
using RDCore.SDK.Workspace.Pictures;

namespace RDCore.SDK.Runtime.Stdole;

/// <summary>
/// What a <c>stdole.StdPicture</c> object holds: the picture's file and what its header says, or no picture at all.
/// </summary>
/// <remarks>
/// <para>
/// One object is one picture, by reference: a runtime binds it to the VBA object that <c>LoadPicture</c> or <c>New StdPicture</c> makes, and a form's
/// control that is given the picture holds the same object, so that a program that sets it on two controls and reads either sees one picture. Only
/// <see cref="Palette"/> changes after the object is made.
/// </para>
/// <para>
/// There is no graphics device behind a picture: <see cref="Handle"/> is a number that tells one picture from another, 0 for none, given by whoever makes
/// the object.
/// </para>
/// </remarks>
public sealed class StdPictureObject
{
    // the icon size LoadPicture takes when none is asked for: the system's.
    private const int IconSide = 32;

    private int _palette;

    /// <summary>
    /// Creates a picture from a file that holds one.
    /// </summary>
    /// <param name="format">The picture's format.</param>
    /// <param name="bytes">The picture's file, as it was read. The object never writes to it.</param>
    /// <param name="width">The picture's width in HIMETRIC units.</param>
    /// <param name="height">The picture's height in HIMETRIC units.</param>
    /// <param name="handle">The value <see cref="Handle"/> reads; any value but 0, which is the handle of no picture.</param>
    /// <param name="image">The image of an icon or a cursor that was loaded, among those its file holds; <see langword="null"/> for any other picture.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="handle"/> is 0, or <paramref name="width"/> or <paramref name="height"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="image"/> is given for a picture that is neither an icon nor a cursor.</exception>
    public StdPictureObject(PictureFormat format, ReadOnlyMemory<byte> bytes, int width, int height, int handle, IconImage? image = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(handle);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (image is not null && format is not (PictureFormat.Icon or PictureFormat.Cursor))
        {
            throw new ArgumentException($"A picture of the format {format} has no icon image.", nameof(image));
        }

        Format = format;
        Bytes = bytes;
        Width = width;
        Height = height;
        Handle = handle;
        Image = image;
    }

    private StdPictureObject(bool isNew)
    {
        IsNew = isNew;
    }

    /// <summary>
    /// Creates what <c>New StdPicture</c> makes: an object that holds no picture, and that <c>SavePicture</c> saves as a file of no bytes.
    /// </summary>
    /// <returns>A new object, of no picture, with <see cref="IsNew"/> set.</returns>
    public static StdPictureObject CreateNew() => new(isNew: true);

    /// <summary>
    /// Creates what <c>LoadPicture</c> makes of no file name: an object that holds no picture, and that <c>SavePicture</c> refuses to save.
    /// </summary>
    /// <returns>A new object, of no picture.</returns>
    public static StdPictureObject CreateEmpty() => new(isNew: false);

    /// <summary>
    /// Makes the picture a file holds, as <c>LoadPicture</c> does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A picture is what its header says it is (<see cref="PictureHeader.TryRead"/>). A PNG is not one <c>LoadPicture</c> reads, and neither is a metafile
    /// whose header states no size. An icon or a cursor is its image of the size asked for, of the most bits per pixel when it has several of that size,
    /// and otherwise its first image, whatever its size. Asking for 0 by 0 is asking for the system's icon size, 32 by 32.
    /// </para>
    /// <para>The failures are those MS-VBA raises for the same files (Excel 64-bit), with the error numbers <see cref="StdPictureLoadFailure"/> gives.</para>
    /// </remarks>
    /// <param name="bytes">The file's bytes. The picture keeps them as they are.</param>
    /// <param name="iconWidth">The width in pixels of the image to load from an icon or a cursor; ignored for any other picture.</param>
    /// <param name="iconHeight">The height in pixels of the image to load from an icon or a cursor; ignored for any other picture.</param>
    /// <param name="handle">Gives the picture's <see cref="Handle"/>; called once, and only for a file that is a picture. It must not return 0.</param>
    /// <param name="picture">The picture, when the file is one; <see langword="null"/> otherwise.</param>
    /// <returns><see cref="StdPictureLoadFailure.None"/> when the file is a picture; otherwise why it is not.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="handle"/> returns 0.</exception>
    public static StdPictureLoadFailure TryLoad(ReadOnlyMemory<byte> bytes, int iconWidth, int iconHeight, Func<int> handle, out StdPictureObject? picture)
    {
        ArgumentNullException.ThrowIfNull(handle);
        picture = null;
        var span = bytes.Span;
        if (!PictureHeader.TryRead(span, out var info))
        {
            return span.StartsWith("BM"u8) ? StdPictureLoadFailure.BitmapCutShort : StdPictureLoadFailure.InvalidPicture;
        }

        switch (info.Format)
        {
            case PictureFormat.Png:
                return StdPictureLoadFailure.InvalidPicture;
            case PictureFormat.Metafile when info.HimetricWidth == 0 && info.HimetricHeight == 0:
                return StdPictureLoadFailure.MetafileStatesNoSize;
            case PictureFormat.Icon or PictureFormat.Cursor:
                _ = PictureHeader.TryReadIconImages(span, out var images);
                var image = Chosen(images, iconWidth, iconHeight);
                picture = new StdPictureObject(info.Format, bytes, PictureInfo.PixelsToHimetric(image.Width), PictureInfo.PixelsToHimetric(image.Height), handle(), image);
                return StdPictureLoadFailure.None;
            default:
                picture = new StdPictureObject(info.Format, bytes, info.HimetricWidth, info.HimetricHeight, handle());
                return StdPictureLoadFailure.None;
        }
    }

    /// <summary>
    /// The picture's format; <see langword="null"/> for an object that holds no picture.
    /// </summary>
    public PictureFormat? Format { get; }

    /// <summary>
    /// Whether the object was made by <c>New StdPicture</c> (<see cref="CreateNew"/>) and holds no picture. <c>SavePicture</c> writes such an object as a
    /// file of no bytes, where it refuses the empty picture <c>LoadPicture</c> gives for no file name (<see cref="CreateEmpty"/>).
    /// </summary>
    public bool IsNew { get; }

    /// <summary>
    /// The picture's file, as it was read; empty for no picture.
    /// </summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>
    /// The image of an icon or a cursor that was loaded, among those its file holds; <see langword="null"/> for any other picture.
    /// </summary>
    public IconImage? Image { get; }

    /// <summary>
    /// The picture's width in HIMETRIC units, which <c>Width</c> reads; 0 for no picture.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// The picture's height in HIMETRIC units, which <c>Height</c> reads; 0 for no picture.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// The value <c>Handle</c>, the default member, reads: 0 for no picture.
    /// </summary>
    public int Handle { get; }

    /// <summary>
    /// The picture's <c>Type</c>: 0 for none, 1 for a bitmap (a GIF and a JPEG are loaded as one), 2 for a metafile, 3 for an icon or a cursor, 4 for an
    /// enhanced metafile.
    /// </summary>
    public short Type => Format switch
    {
        null => 0,
        PictureFormat.Metafile => 2,
        PictureFormat.Icon or PictureFormat.Cursor => 3,
        PictureFormat.EnhancedMetafile => 4,
        _ => 1,
    };

    /// <summary>
    /// Whether the picture has a palette that <c>hPal</c> reads and a program can assign: a bitmap, a GIF or a JPEG. MS-VBA fails <c>hPal</c> on any
    /// other picture, and on no picture.
    /// </summary>
    public bool HasPalette => Format is PictureFormat.Bitmap or PictureFormat.Gif or PictureFormat.Jpeg;

    /// <summary>
    /// The value <c>hPal</c> reads, which a program can assign: 0 until it does.
    /// </summary>
    /// <exception cref="InvalidOperationException">The picture has no palette (<see cref="HasPalette"/> is <see langword="false"/>).</exception>
    public int Palette
    {
        get => HasPalette ? _palette : throw NoPalette();
        set => _palette = HasPalette ? value : throw NoPalette();
    }

    private InvalidOperationException NoPalette() => new($"A picture of type {Type} has no palette.");

    // the image of the size asked for, of the most bits per pixel when there are several; with none of that size, the first of the file, whatever its
    // size: no size near the one asked for is preferred to it (observed in Excel).
    private static IconImage Chosen(ImmutableArray<IconImage> images, int width, int height)
    {
        if (width == 0 && height == 0)
        {
            (width, height) = (IconSide, IconSide);
        }

        return images.Where(image => image.Width == width && image.Height == height).OrderByDescending(image => image.BitCount).FirstOrDefault() is { Length: > 0 } sized
            ? sized
            : images[0];
    }
}

/// <summary>
/// Why <see cref="StdPictureObject.TryLoad"/> made no picture of a file: each value is the error number MS-VBA's <c>LoadPicture</c> raises for it.
/// </summary>
public enum StdPictureLoadFailure
{
    /// <summary>
    /// The file is a picture.
    /// </summary>
    None = 0,

    /// <summary>
    /// The file is not a picture that <c>LoadPicture</c> reads: no bytes, a format it does not recognize or refuses (a PNG), or a malformed header. Run-time
    /// error 481, "Invalid picture".
    /// </summary>
    InvalidPicture = 481,

    /// <summary>
    /// The file starts as a bitmap and is cut short or malformed: <c>E_UNEXPECTED</c>, -2147418113.
    /// </summary>
    BitmapCutShort = unchecked((int)0x8000FFFF),

    /// <summary>
    /// The file is a metafile whose header states no size, one with no placeable header: <c>E_FAIL</c>, -2147467259.
    /// </summary>
    MetafileStatesNoSize = unchecked((int)0x80004005),
}
