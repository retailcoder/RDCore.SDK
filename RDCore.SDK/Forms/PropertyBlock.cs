namespace RDCore.SDK.Forms;

/// <summary>
/// How a property of a mask-driven structure is persisted: its DataBlock slot, its ExtraDataBlock value, its StreamData,
/// or the mask bit alone ([MS-OFORMS] 2.2.1.1 and the masks of every structure alike).
/// </summary>
internal enum SlotType
{
    // DataBlock values: an integer of 1, 2 or 4 bytes, aligned to its size
    UInt8,
    UInt16,
    Int16,
    UInt32,
    Int32,

    // a 4-byte bit field kept as a uint: VariousPropertyBits, FontEffects, BooleanProperties, BitFlags
    Bits,

    // OLE_COLOR (2.4.9), a uint
    Color,

    // a Unicode character (2 bytes)
    Char,

    // an int32 in HIMETRIC, as a double in points
    Length,

    // a 4-byte fmPicturePosition, as the object model's fmPicturePosition value
    PicturePosition,

    // a 2-byte TRUE (-1) or FALSE (0), as a bool
    Boolean16,

    // the mask bit alone: set means not the file format's default
    Flag,

    // a CountOfBytesWithCompressionFlag in the DataBlock, the fmString in the ExtraDataBlock
    String,

    // a byte count in the DataBlock, an array of ArrayString in the ExtraDataBlock (2.2.9.4)
    Strings,

    // an fmSize or fmPosition in the ExtraDataBlock (two int32 in HIMETRIC), as two doubles in points
    Size,

    // a 0xFFFF marker in the DataBlock, a GuidAndPicture (2.4.8) or GuidAndFont (2.4.7) in the StreamData
    Picture,
    Font,
}

/// <summary>
/// A property of a mask-driven structure: its mask bit, how it is persisted, the Microsoft Forms property it is (and the
/// second one of a size or a position), the file format's default, and the kinds of control it applies to.
/// </summary>
/// <remarks>
/// A name that starts with an underscore is the structure's own business (a count, a raw bit field); it is not one of the
/// control's properties.
/// </remarks>
/// <param name="Bit">The property's bit in the structure's mask.</param>
/// <param name="Type">How the property is persisted.</param>
/// <param name="Name">The property's name.</param>
/// <param name="Default">The file format's default, when the structure does not store the property.</param>
/// <param name="Name2">The name of a size's or a position's second value; <c>null</c> for another property.</param>
/// <param name="AppliesTo">The kinds of control the property applies to; <c>null</c> when it applies to every kind.</param>
internal sealed record class Slot(
    int Bit,
    SlotType Type,
    string Name,
    object? Default = null,
    string? Name2 = null,
    IReadOnlySet<MSFormsControlKind>? AppliesTo = null)
{
    public bool IsInternal => Name.StartsWith('_');

    public bool AppliesToKind(MSFormsControlKind kind) => AppliesTo is null || AppliesTo.Contains(kind);
}

/// <summary>
/// The values a mask-driven structure stores: its mask, and each stored property's value by name.
/// </summary>
/// <param name="mask">The structure's mask.</param>
internal sealed class Block(ulong mask)
{
    public ulong Mask { get; } = mask;

    public Dictionary<string, object> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    // where the structure starts in its stream, and where each stored string's count and characters are: what a writer
    // that changes a string needs
    public int Start { get; init; }

    public Dictionary<string, (int CountAt, int TextAt)> StringsAt { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Has(int bit) => (Mask & (1UL << bit)) != 0;

    public T Get<T>(string name, T fallback) => Values.TryGetValue(name, out var value) && value is T typed ? typed : fallback;
}

/// <summary>
/// Reads the structures [MS-OFORMS] persists with a property mask: a version, a size, the mask, then a DataBlock, an
/// ExtraDataBlock and StreamData, each holding the stored properties in the order of their mask bits.
/// </summary>
internal static class PropertyBlock
{
    private static readonly Guid StdFont = new("0BE35203-8F91-11CE-9DE3-00AA004BB851");
    private static readonly Guid TextPropsFont = new("AFC20920-DA4E-11CE-B943-00AA006887B4");

    // the HIMETRIC to point ratio: 2540 HIMETRIC are an inch, which is 72 points
    public static double Points(int himetric) => himetric * 72.0 / 2540.0;

    /// <summary>
    /// Reads the structure at the reader's position: <c>MinorVersion</c> and <c>MajorVersion</c> (or a 2-byte version),
    /// a 2-byte size of what follows it up to its StreamData, a mask of <paramref name="maskSize"/> bytes, its blocks, and
    /// its StreamData.
    /// </summary>
    /// <param name="reader">The reader, at the structure's first byte; past its StreamData once read.</param>
    /// <param name="slots">The structure's slots.</param>
    /// <param name="maskSize">The size of the structure's mask, in bytes: <c>4</c> or <c>8</c>.</param>
    /// <param name="majorVersion">The major version the structure must have, its minor version being <c>0</c>; <c>null</c>
    /// when its version is not checked.</param>
    public static Block Read(ref ByteReader reader, IReadOnlyList<Slot> slots, int maskSize, byte? majorVersion)
    {
        var start = reader.Position;
        var minorVersion = reader.ReadByte();
        var major = reader.ReadByte();
        if (majorVersion is { } expected && (minorVersion != 0 || major != expected))
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.UnsupportedVersion);
        }

        var size = reader.ReadUInt16();
        var end = reader.Position + size;
        var mask = maskSize == 8 ? reader.ReadUInt32() | ((ulong)reader.ReadUInt32() << 32) : reader.ReadUInt32();
        var block = new Block(mask) { Start = start };
        var stored = slots.Where(slot => block.Has(slot.Bit)).ToList();

        // DataBlock: the values of 4 bytes or less, then padding to a multiple of 4
        var counts = new Dictionary<int, uint>();
        var countsAt = new Dictionary<int, int>();
        foreach (var slot in stored)
        {
            switch (slot.Type)
            {
                case SlotType.UInt8:
                    Set(block, slot, (int)reader.ReadByte());
                    break;
                case SlotType.UInt16:
                    reader.AlignFrom(start, 2);
                    Set(block, slot, (int)reader.ReadUInt16());
                    break;
                case SlotType.Int16:
                    reader.AlignFrom(start, 2);
                    Set(block, slot, (int)reader.ReadInt16());
                    break;
                case SlotType.Boolean16:
                    reader.AlignFrom(start, 2);
                    Set(block, slot, reader.ReadInt16() != 0);
                    break;
                case SlotType.Char:
                    reader.AlignFrom(start, 2);
                    Set(block, slot, (char)reader.ReadUInt16());
                    break;
                case SlotType.UInt32:
                    reader.AlignFrom(start, 4);
                    Set(block, slot, unchecked((int)reader.ReadUInt32()));
                    break;
                case SlotType.Int32:
                    reader.AlignFrom(start, 4);
                    Set(block, slot, reader.ReadInt32());
                    break;
                case SlotType.Bits or SlotType.Color:
                    reader.AlignFrom(start, 4);
                    Set(block, slot, reader.ReadUInt32());
                    break;
                case SlotType.Length:
                    reader.AlignFrom(start, 4);
                    Set(block, slot, Points(reader.ReadInt32()));
                    break;
                case SlotType.PicturePosition:
                    reader.AlignFrom(start, 4);
                    Set(block, slot, PicturePositionOf(reader.ReadUInt32()));
                    break;
                case SlotType.String or SlotType.Strings:
                    reader.AlignFrom(start, 4);
                    countsAt[slot.Bit] = reader.Position;
                    counts[slot.Bit] = reader.ReadUInt32();
                    break;
                case SlotType.Picture or SlotType.Font:
                    reader.AlignFrom(start, 2);
                    reader.ReadUInt16(); // the 0xFFFF marker: the value is in the StreamData
                    break;
                case SlotType.Flag:
                    Set(block, slot, !(bool)slot.Default!);
                    break;
            }
        }

        reader.AlignFrom(start, 4);

        // ExtraDataBlock: the larger values, each padded to a multiple of 4
        foreach (var slot in stored)
        {
            switch (slot.Type)
            {
                case SlotType.Size:
                    var width = reader.ReadInt32();
                    var height = reader.ReadInt32();
                    Set(block, slot, Points(width), Points(height));
                    break;
                case SlotType.String:
                    block.StringsAt[slot.Name] = (countsAt[slot.Bit], reader.Position);
                    Set(block, slot, reader.ReadString(counts[slot.Bit]));
                    reader.AlignFrom(start, 4);
                    break;
                case SlotType.Strings:
                    Set(block, slot, ReadArrayStrings(ref reader, start, counts[slot.Bit]));
                    break;
            }
        }

        // the structure's size says where its StreamData starts; a size smaller than what it holds is damage
        reader.Seek(end);

        foreach (var slot in stored)
        {
            switch (slot.Type)
            {
                case SlotType.Picture:
                    Set(block, slot, ReadGuidAndPicture(ref reader));
                    break;
                case SlotType.Font:
                    Set(block, slot, ReadGuidAndFont(ref reader));
                    break;
            }
        }

        return block;
    }

    /// <summary>
    /// The file format's default of each slot that has one and applies to <paramref name="kind"/>, as the values
    /// <see cref="Read"/> gives.
    /// </summary>
    /// <param name="slots">The structure's slots.</param>
    /// <param name="kind">The control's kind.</param>
    public static Dictionary<string, object> Defaults(IReadOnlyList<Slot> slots, MSFormsControlKind kind)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in slots.Where(slot => slot.Default is not null && slot.AppliesToKind(kind)))
        {
            switch (slot.Type, slot.Default)
            {
                case (SlotType.Size, (int width, int height)):
                    result[slot.Name] = Points(width);
                    result[slot.Name2!] = Points(height);
                    break;
                case (SlotType.Length, int himetric):
                    result[slot.Name] = Points(himetric);
                    break;
                case (SlotType.PicturePosition, uint position):
                    result[slot.Name] = PicturePositionOf(position);
                    break;
                default:
                    result[slot.Name] = slot.Default!;
                    break;
            }
        }

        return result;
    }

    // TextProps ([MS-OFORMS] 2.3.1): the font of a CommandButton, Label, MorphData or TabStrip, and its paragraph alignment
    private static readonly Slot[] TextPropsSlots =
    [
        new(0, SlotType.String, "_FontName", "MS Sans Serif"),
        new(1, SlotType.Bits, "_FontEffects", 0u),
        new(2, SlotType.UInt32, "_FontHeight", 160),
        new(4, SlotType.UInt8, "_FontCharSet", 1),
        new(5, SlotType.UInt8, "_FontPitchAndFamily", 0),
        new(6, SlotType.UInt8, "_ParagraphAlign", 1),
        new(7, SlotType.UInt16, "_FontWeight", 400),
    ];

    /// <summary>
    /// Reads a <c>TextProps</c> ([MS-OFORMS] 2.3.1): the font it describes, and its <c>ParagraphAlign</c> as the object
    /// model's <c>TextAlign</c> (<c>null</c> when it is not stored).
    /// </summary>
    /// <param name="reader">The reader, at the <c>TextProps</c>' first byte; past it once read.</param>
    public static (MSFormsFont Font, int? TextAlign) ReadTextProps(ref ByteReader reader)
    {
        var block = Read(ref reader, TextPropsSlots, maskSize: 4, majorVersion: 2);
        var defaults = Defaults(TextPropsSlots, MSFormsControlKind.Other);
        T Value<T>(string name) => block.Values.TryGetValue(name, out var value) ? (T)value : (T)defaults[name];

        var effects = Value<uint>("_FontEffects");
        var font = new MSFormsFont(
            Value<string>("_FontName"),
            Value<int>("_FontHeight") / 20.0, // twips
            (effects & 1) != 0,
            (effects & 2) != 0,
            (effects & 4) != 0,
            (effects & 8) != 0,
            Value<int>("_FontWeight"),
            Value<int>("_FontCharSet"));
        int? textAlign = block.Has(6) ? TextAlignOf(Value<int>("_ParagraphAlign")) : null;
        return (font, textAlign);
    }

    /// <summary>
    /// The object model's <c>TextAlign</c> (<c>fmTextAlign</c>: 1 left, 2 center, 3 right) of a <c>TextProps</c>'
    /// <c>ParagraphAlign</c> (1 left, 2 right, 3 center).
    /// </summary>
    /// <param name="paragraphAlign">The <c>ParagraphAlign</c>.</param>
    public static int TextAlignOf(int paragraphAlign) => paragraphAlign switch
    {
        2 => 3,
        3 => 2,
        _ => paragraphAlign,
    };

    // GuidAndFont ([MS-OFORMS] 2.4.7): a GUID, then a StdFont (2.4.12) or a TextProps (2.3.1), by that GUID
    private static MSFormsFont ReadGuidAndFont(ref ByteReader reader)
    {
        var classId = reader.ReadGuid();
        if (classId == TextPropsFont)
        {
            return ReadTextProps(ref reader).Font;
        }

        if (classId != StdFont)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.UnsupportedVersion);
        }

        reader.ReadByte(); // Version
        var charset = reader.ReadUInt16();
        var flags = reader.ReadByte();
        var weight = reader.ReadUInt16();
        var height = reader.ReadUInt32();
        var name = System.Text.Encoding.Latin1.GetString(reader.ReadBytes(reader.ReadByte()));

        // bFlags: bit 0 bold, bit 1 italic, bit 2 underline, bit 3 strikethrough; the weight carries boldness too
        return new MSFormsFont(name, height / 10000.0, (flags & 1) != 0 || weight >= 700, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0, weight, charset);
    }

    // GuidAndPicture ([MS-OFORMS] 2.4.8): a GUID, then a StdPicture (2.4.13): a preamble, a size, and that many bytes
    private static MSFormsPicture ReadGuidAndPicture(ref ByteReader reader)
    {
        reader.ReadGuid();
        reader.ReadUInt32(); // Preamble, 0x0000746C
        var size = reader.ReadUInt32();
        return new MSFormsPicture(reader.ReadBytes((int)Math.Min(size, int.MaxValue)).ToArray());
    }

    // an array of ArrayString ([MS-OFORMS] 2.2.9.4, 2.4.14.3) of `size` bytes: each a CountOfCharsWithCompressionFlag,
    // the characters, then padding to a multiple of 4
    private static string[] ReadArrayStrings(ref ByteReader reader, int origin, uint size)
    {
        var end = reader.Position + (int)Math.Min(size, int.MaxValue);
        var result = new List<string>();
        while (reader.Position < end)
        {
            var countAndCompression = reader.ReadUInt32();
            var count = countAndCompression & 0x7FFFFFFF;
            var compressed = (countAndCompression & 0x80000000) != 0;
            result.Add(reader.ReadString(compressed ? countAndCompression : Math.Min(count * 2, 0x7FFFFFFF)));
            reader.AlignFrom(origin, 4);
        }

        reader.Seek(end);
        return [.. result];
    }

    // fmPicturePosition: the picture's anchor point in its low word and the caption's in its high
    // word, each one of a 3 × 3 grid read row by row; as the object model's fmPicturePosition. A value outside the table
    // is kept as stored.
    private static int PicturePositionOf(uint stored) => stored switch
    {
        0x00020000 => 0, // LeftTop
        0x00050003 => 1, // LeftCenter
        0x00080006 => 2, // LeftBottom
        0x00000002 => 3, // RightTop
        0x00030005 => 4, // RightCenter
        0x00060008 => 5, // RightBottom
        0x00060000 => 6, // AboveLeft
        0x00070001 => 7, // AboveCenter
        0x00080002 => 8, // AboveRight
        0x00000006 => 9, // BelowLeft
        0x00010007 => 10, // BelowCenter
        0x00020008 => 11, // BelowRight
        0x00040004 => 12, // Center
        _ => unchecked((int)stored),
    };

    private static void Set(Block block, Slot slot, object value) => block.Values[slot.Name] = value;

    private static void Set(Block block, Slot slot, object first, object second)
    {
        block.Values[slot.Name] = first;
        block.Values[slot.Name2!] = second;
    }
}
