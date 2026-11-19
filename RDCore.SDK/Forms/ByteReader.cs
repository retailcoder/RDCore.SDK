using System.Buffers.Binary;
using System.Text;

namespace RDCore.SDK.Forms;

/// <summary>
/// A forward-only reader over a stream's bytes, whose every read is bounds-checked: running out of bytes is a
/// <see cref="MSFormsFormatProblem.Truncated"/> stream.
/// </summary>
/// <param name="data">The bytes to read, from the first.</param>
internal ref struct ByteReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;

    public int Position { get; private set; }

    public readonly int Length => _data.Length;

    public byte ReadByte() => Take(1)[0];

    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

    public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));

    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

    public Guid ReadGuid() => new(Take(16));

    public ReadOnlySpan<byte> ReadBytes(int count) => Take(count);

    // an fmString ([MS-OFORMS] 2.4.14.4) of the size its CountOfBytesWithCompressionFlag (2.4.14.2) gives: compressed,
    // every character is one byte (its high-order byte, zero, removed); uncompressed, UTF-16. Its padding is the caller's.
    public string ReadString(uint countAndCompression)
    {
        var bytes = Take((int)(countAndCompression & 0x7FFFFFFF));
        return (countAndCompression & 0x80000000) != 0 ? Encoding.Latin1.GetString(bytes) : Encoding.Unicode.GetString(bytes);
    }

    public void Skip(int count) => Take(count);

    public void Seek(int position)
    {
        if (position < Position || position > _data.Length)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.Truncated);
        }

        Position = position;
    }

    // a value is aligned to its own size from the first byte of the structure that holds it ([MS-OFORMS] 2.2.1.2 and alike)
    public void AlignFrom(int origin, int alignment)
        => Skip((alignment - (Position - origin) % alignment) % alignment);

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || count > _data.Length - Position)
        {
            throw new MSFormsFormatException(MSFormsFormatProblem.Truncated);
        }

        var result = _data.Slice(Position, count);
        Position += count;
        return result;
    }
}
