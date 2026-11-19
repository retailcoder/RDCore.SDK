namespace RDCore.SDK.Forms;

/// <summary>
/// A font a control stores: a <c>TextProps</c> ([MS-OFORMS] 2.3.1) or a <c>StdFont</c> ([MS-OFORMS] 2.4.12).
/// </summary>
/// <param name="Name">The font's face name.</param>
/// <param name="Size">The font's height, in points.</param>
/// <param name="Bold">Whether the font is bold.</param>
/// <param name="Italic">Whether the font is italic.</param>
/// <param name="Underline">Whether the font is underlined.</param>
/// <param name="Strikeout">Whether the font is struck out.</param>
/// <param name="Weight">The font's weight, from <c>0</c> to <c>1000</c> (<c>400</c> normal, <c>700</c> bold).</param>
/// <param name="Charset">The font's character set (<c>0</c> ANSI, <c>1</c> the system's default).</param>
public sealed record class MSFormsFont(
    string Name,
    double Size,
    bool Bold,
    bool Italic,
    bool Underline,
    bool Strikeout,
    int Weight,
    int Charset);

/// <summary>
/// The formats of the picture data a control stores.
/// </summary>
public enum MSFormsPictureFormat
{
    /// <summary>
    /// Data that starts with none of the signatures below.
    /// </summary>
    Unknown,

    /// <summary>
    /// A bitmap file, which starts with its <c>BITMAPFILEHEADER</c> (<c>BM</c>).
    /// </summary>
    Bitmap,

    /// <summary>
    /// A device-independent bitmap with no file header: it starts with its <c>BITMAPINFOHEADER</c>.
    /// </summary>
    DeviceIndependentBitmap,

    /// <summary>
    /// A GIF file.
    /// </summary>
    Gif,

    /// <summary>
    /// A JPEG file.
    /// </summary>
    Jpeg,

    /// <summary>
    /// A PNG file.
    /// </summary>
    Png,

    /// <summary>
    /// A Windows metafile, placeable or not ([MS-WMF]).
    /// </summary>
    Metafile,

    /// <summary>
    /// An enhanced metafile ([MS-EMF]).
    /// </summary>
    EnhancedMetafile,

    /// <summary>
    /// An icon file.
    /// </summary>
    Icon,

    /// <summary>
    /// A cursor file.
    /// </summary>
    Cursor,
}

/// <summary>
/// A picture a control stores (<c>Picture</c>, <c>MouseIcon</c>): the data of a <c>StdPicture</c>
/// ([MS-OFORMS] 2.4.13), which is the content of a picture file.
/// </summary>
/// <param name="Data">The picture's data, as stored.</param>
public sealed record class MSFormsPicture(ReadOnlyMemory<byte> Data)
{
    /// <summary>
    /// The format of <see cref="Data"/>, from the signature it starts with.
    /// </summary>
    public MSFormsPictureFormat Format => FormatOf(Data.Span);

    private static MSFormsPictureFormat FormatOf(ReadOnlySpan<byte> data) => data switch
    {
        [(byte)'B', (byte)'M', ..] => MSFormsPictureFormat.Bitmap,
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..] => MSFormsPictureFormat.Gif,
        [0xFF, 0xD8, 0xFF, ..] => MSFormsPictureFormat.Jpeg,
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => MSFormsPictureFormat.Png,
        [0xD7, 0xCD, 0xC6, 0x9A, ..] or [1 or 2, 0, 9, 0, ..] => MSFormsPictureFormat.Metafile,
        [1, 0, 0, 0, ..] when data.Length >= 44 && data[40..44].SequenceEqual(" EMF"u8) => MSFormsPictureFormat.EnhancedMetafile,
        [0, 0, 1, 0, ..] => MSFormsPictureFormat.Icon,
        [0, 0, 2, 0, ..] => MSFormsPictureFormat.Cursor,
        [12 or 40 or 52 or 56 or 108 or 124, 0, 0, 0, ..] => MSFormsPictureFormat.DeviceIndependentBitmap,
        _ => MSFormsPictureFormat.Unknown,
    };
}

/// <summary>
/// A tab of a <c>TabStrip</c>, or of the tab row of a <c>MultiPage</c>: its entries in the arrays of a
/// <c>TabStripControl</c> ([MS-OFORMS] 2.2.9) and its <c>TabStripTabFlag</c> ([MS-OFORMS] 2.2.9.7).
/// </summary>
/// <param name="Caption">The tab's caption.</param>
/// <param name="Name">The tab's name.</param>
/// <param name="ControlTipText">The tab's tooltip.</param>
/// <param name="Tag">The tab's <c>Tag</c>.</param>
/// <param name="Accelerator">The tab's accelerator key; <c>'\0'</c> when it has none.</param>
/// <param name="Visible">Whether the tab is visible.</param>
/// <param name="Enabled">Whether the tab is enabled.</param>
public sealed record class MSFormsTab(
    string Caption,
    string Name,
    string ControlTipText,
    string Tag,
    char Accelerator,
    bool Visible,
    bool Enabled);
