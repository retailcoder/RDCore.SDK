using System.Buffers.Binary;
using System.Text;

namespace RDCore.Tests.Forms;

/// <summary>
/// Writes the structures [MS-OFORMS] persists, byte by byte, for tests that state each byte they give the reader: a
/// structure's values are aligned from its own first byte, as the reader expects.
/// </summary>
internal sealed class MSFormsBytes
{
    private readonly List<byte> _bytes = [];
    private int _origin;

    public int Length => _bytes.Count;

    public MSFormsBytes U8(int value)
    {
        _bytes.Add((byte)value);
        return this;
    }

    public MSFormsBytes U16(int value) => Raw(BitConverter.GetBytes((ushort)value));

    public MSFormsBytes U32(uint value) => Raw(BitConverter.GetBytes(value));

    public MSFormsBytes I32(int value) => Raw(BitConverter.GetBytes(value));

    public MSFormsBytes Guid(Guid value) => Raw(value.ToByteArray());

    public MSFormsBytes Raw(params byte[] bytes)
    {
        _bytes.AddRange(bytes);
        return this;
    }

    // padding to a multiple of `alignment` from the current structure's first byte
    public MSFormsBytes Align(int alignment) => AlignFrom(_origin, alignment);

    public MSFormsBytes AlignFrom(int origin, int alignment)
    {
        while ((Length - origin) % alignment != 0)
        {
            _bytes.Add(0xEE); // padding is not read: make it visible
        }

        return this;
    }

    // a compressed fmString's CountOfBytesWithCompressionFlag
    public static uint Count(string value) => (uint)value.Length | 0x80000000;

    // a compressed fmString, padded to 4 bytes
    public MSFormsBytes String(string value) => Raw(Encoding.Latin1.GetBytes(value)).Align(4);

    // a structure: MinorVersion 0, MajorVersion, its size, its mask, its DataBlock padded to 4, then its ExtraDataBlock
    public MSFormsBytes Structure(int majorVersion, ulong mask, Action<MSFormsBytes> data, Action<MSFormsBytes>? extra = null, int maskSize = 4)
    {
        var saved = _origin;
        _origin = Length;
        U8(0).U8(majorVersion);
        var sizeAt = Length;
        U16(0);
        U32((uint)mask);
        if (maskSize == 8)
        {
            U32((uint)(mask >> 32));
        }

        data(this);
        Align(4);
        extra?.Invoke(this);
        var size = Length - sizeAt - 2;
        _bytes[sizeAt] = (byte)size;
        _bytes[sizeAt + 1] = (byte)(size >> 8);
        _origin = saved;
        return this;
    }

    // a TextProps (2.3.1) that stores nothing: every font property has its default
    public MSFormsBytes TextProps() => Structure(2, 0, _ => { });

    // a GuidAndPicture (2.4.8) holding `data`
    public MSFormsBytes Picture(byte[] data)
        => Guid(new Guid("0BE35204-8F91-11CE-9DE3-00AA004BB851")).U32(0x0000746C).U32((uint)data.Length).Raw(data);

    public byte[] ToArray() => [.. _bytes];

    /// <summary>
    /// A form stream (<c>f</c>): a <c>FormControl</c> that stores <paramref name="formMask"/>'s properties, an empty class
    /// table, and the sites, all at depth 0.
    /// </summary>
    public static byte[] FormStream(uint formMask, Action<MSFormsBytes> data, Action<MSFormsBytes>? extra, params byte[][] sites)
    {
        var bytes = new MSFormsBytes().Structure(4, formMask, data, extra);
        if ((formMask & (1u << 6)) == 0)
        {
            bytes.U16(0); // CountOfSiteClassInfo
        }

        bytes.U32((uint)sites.Length).U32((uint)sites.Sum(site => site.Length));
        var depthsStart = bytes.Length;
        foreach (var _ in sites)
        {
            bytes.U8(0).U8(1); // depth 0, one site of type 1
        }

        bytes.AlignFrom(depthsStart, 4);
        foreach (var site in sites)
        {
            bytes.Raw(site);
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// An empty form's form stream.
    /// </summary>
    public static byte[] FormStream(params byte[][] sites) => FormStream(0, _ => { }, null, sites);

    /// <summary>
    /// A site (2.2.10.12) with a name, an ID, a class cache index, and the size of its control's data in <c>o</c>
    /// (<c>null</c>: not streamed, which a parent control's site is).
    /// </summary>
    public static byte[] Site(string name, int id, int cacheIndex, int? objectStreamSize, int tabIndex = 0, uint? bitFlags = null)
    {
        // Name, ID, BitFlags (when given), ObjectStreamSize (when streamed), TabIndex, ClsidCacheIndex
        var mask = 1u | 1u << 2 | 1u << 6 | 1u << 7 | (objectStreamSize is null ? 0 : 1u << 5);
        var flags = bitFlags ?? (objectStreamSize is null ? 0x00040023u : (uint?)null);
        if (flags is not null)
        {
            mask |= 1u << 4;
        }

        return new MSFormsBytes().Structure(
            0,
            mask,
            data =>
            {
                data.U32(Count(name)).I32(id);
                if (flags is { } value)
                {
                    data.U32(value);
                }

                if (objectStreamSize is { } size)
                {
                    data.I32(size);
                }

                data.U16(tabIndex).U16(cacheIndex);
            },
            extra => extra.String(name)).ToArray();
    }
}

/// <summary>
/// Writes a version 3 compound file ([MS-CFB]) whose streams all live in regular sectors (its mini stream cutoff is 0),
/// for tests that give the reader a storage of their own.
/// </summary>
internal static class TestCompoundFile
{
    private const uint EndOfChain = 0xFFFFFFFE, FreeSector = 0xFFFFFFFF, FatSector = 0xFFFFFFFD, NoStream = 0xFFFFFFFF;

    /// <summary>
    /// A stream (<paramref name="Stream"/> not null) or a storage (<paramref name="Children"/> not null).
    /// </summary>
    public sealed record class Entry(string Name, byte[]? Stream = null, IReadOnlyList<Entry>? Children = null);

    public static Entry Stream(string name, byte[] data) => new(name, data);

    public static Entry Storage(string name, params Entry[] children) => new(name, Children: children);

    public static byte[] Write(params Entry[] root)
    {
        // the directory, root first; each storage's children chained by their right siblings
        var entries = new List<(Entry Entry, uint Child, uint Right)>();
        uint Add(Entry entry)
        {
            var index = entries.Count;
            entries.Add((entry, NoStream, NoStream));
            if (entry.Children is { Count: > 0 } children)
            {
                var indexes = children.Select(Add).ToList();
                for (var position = 0; position < indexes.Count - 1; position++)
                {
                    entries[(int)indexes[position]] = entries[(int)indexes[position]] with { Right = indexes[position + 1] };
                }

                entries[index] = entries[index] with { Child = indexes[0] };
            }

            return (uint)index;
        }

        Add(new Entry("Root Entry", Children: root));

        static int SectorsOf(int length) => (length + 511) / 512;
        var dataSectors = entries.Sum(entry => SectorsOf(entry.Entry.Stream?.Length ?? 0));
        var directorySectors = SectorsOf(entries.Count * 128);
        var fatSectors = 1;
        while (fatSectors * 128 < fatSectors + directorySectors + dataSectors)
        {
            fatSectors++;
        }

        var sectorCount = fatSectors + directorySectors + dataSectors;
        var file = new byte[512 * (1 + sectorCount)];
        var fat = Enumerable.Repeat(FreeSector, fatSectors * 128).ToArray();
        for (var sector = 0; sector < fatSectors; sector++)
        {
            fat[sector] = FatSector;
        }

        void Chain(int first, int count)
        {
            for (var sector = first; sector < first + count; sector++)
            {
                fat[sector] = sector == first + count - 1 ? EndOfChain : (uint)sector + 1;
            }
        }

        Chain(fatSectors, directorySectors);
        var next = fatSectors + directorySectors;
        var directory = file.AsSpan(512 * (1 + fatSectors));
        for (var index = 0; index < entries.Count; index++)
        {
            var (entry, child, right) = entries[index];
            var slot = directory.Slice(index * 128, 128);
            var name = Encoding.Unicode.GetBytes(entry.Name + "\0");
            name.CopyTo(slot);
            BinaryPrimitives.WriteUInt16LittleEndian(slot[64..], (ushort)name.Length);
            slot[66] = (byte)(index == 0 ? 5 : entry.Children is null ? 2 : 1);
            slot[67] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(slot[68..], NoStream);
            BinaryPrimitives.WriteUInt32LittleEndian(slot[72..], right);
            BinaryPrimitives.WriteUInt32LittleEndian(slot[76..], child);
            var data = entry.Stream ?? [];
            var start = data.Length == 0 ? EndOfChain : (uint)next;
            BinaryPrimitives.WriteUInt32LittleEndian(slot[116..], start);
            BinaryPrimitives.WriteUInt64LittleEndian(slot[120..], (ulong)data.Length);
            if (data.Length > 0)
            {
                data.CopyTo(file, 512 * (1 + next));
                Chain(next, SectorsOf(data.Length));
                next += SectorsOf(data.Length);
            }
        }

        for (var index = 0; index < fat.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(512 + index * 4), fat[index]);
        }

        // the header ([MS-CFB] 2.2): version 3, 512-byte sectors, no mini stream, no DIFAT sectors
        byte[] signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        signature.CopyTo(file, 0);
        var header = file.AsSpan(0, 512);
        BinaryPrimitives.WriteUInt16LittleEndian(header[24..], 0x3E);
        BinaryPrimitives.WriteUInt16LittleEndian(header[26..], 3);
        BinaryPrimitives.WriteUInt16LittleEndian(header[28..], 0xFFFE);
        BinaryPrimitives.WriteUInt16LittleEndian(header[30..], 9);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], 6);
        BinaryPrimitives.WriteUInt32LittleEndian(header[44..], (uint)fatSectors);
        BinaryPrimitives.WriteUInt32LittleEndian(header[48..], (uint)fatSectors);
        BinaryPrimitives.WriteUInt32LittleEndian(header[56..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header[60..], EndOfChain);
        BinaryPrimitives.WriteUInt32LittleEndian(header[68..], EndOfChain);
        for (var index = 0; index < 109; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header[(76 + index * 4)..], index < fatSectors ? (uint)index : FreeSector);
        }

        return file;
    }
}
