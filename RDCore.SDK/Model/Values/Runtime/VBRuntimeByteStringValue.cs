using System.Runtime.InteropServices;
namespace RDCore.SDK.Model.Values.Runtime;

/// <summary>
/// The runtime value of a <c>String</c> whose data is an odd number of bytes: the whole characters it holds, and the byte
/// left over after them.
/// </summary>
/// <remarks>
/// The binary format a <c>String</c> is stored in is "the implementation-defined binary format used to store <c>String</c>
/// data" (<strong>MS-VBAL §5.5.1.2.6</strong>), and a <c>Byte()</c> of any length is "interpreted as if it represents" it.
/// RDCore's format is UTF-16LE code units - which a managed <see cref="string"/> already is - and, when the data is an odd
/// number of bytes, the one byte left over after the last whole code unit. A <see cref="string"/> cannot hold half a code
/// unit, so that byte is held beside it.
/// <para>
/// 👉 Only an odd string is one of these: an even one is the <see cref="VBRuntimeValue{T}"/> of a <see cref="string"/> it
/// always was. <see cref="BoxedValue"/> is the whole characters, so a reader that only knows the managed value sees the
/// characters of an odd string - never nothing, and never half a character.
/// </para>
/// <para>
/// The byte image assumes a little-endian platform that allows misaligned access, as the session's memory already does.
/// </para>
/// </remarks>
/// <param name="Text">The whole characters: every code unit the data makes up.</param>
/// <param name="TrailingByte">The byte left over after them.</param>
public readonly record struct VBRuntimeByteStringValue(string Text, byte TrailingByte) : IRuntimeValue<string>
{
    /// <inheritdoc/>
    public object BoxedValue => Text;
    /// <inheritdoc/>
    public string StoredValue => Text;

    /// <summary>
    /// The runtime value of the <c>String</c> whose data is <paramref name="bytes"/>: an ordinary one for an even number of
    /// bytes, and one of these for an odd number.
    /// </summary>
    /// <remarks>
    /// Every byte is kept, and every pair of bytes is a code unit - a lone surrogate included. <c>Encoding.Unicode</c> is not
    /// this format: it reads <c>00 D8</c> as <c>U+FFFD</c> and drops an odd byte.
    /// </remarks>
    /// <param name="bytes">The data of the string, two bytes per code unit, little-endian.</param>
    public static IRuntimeValue<string> FromBytes(ReadOnlySpan<byte> bytes)
    {
        var text = new string(MemoryMarshal.Cast<byte, char>(bytes[..(bytes.Length & ~1)]));
        return bytes.Length % 2 == 0
            ? new VBRuntimeValue<string>(text)
            : new VBRuntimeByteStringValue(text, bytes[^1]);
    }

    /// <summary>
    /// The data of the <c>String</c> <paramref name="value"/> holds: two bytes per code unit, little-endian, then the trailing
    /// byte when there is one.
    /// </summary>
    /// <remarks>
    /// <c>vbNullString</c> has no data at all, and neither has a value that holds no <c>String</c>.
    /// </remarks>
    /// <param name="value">The runtime value of a <c>String</c>.</param>
    public static byte[] BytesOf(IRuntimeValue value) => value switch
    {
        VBRuntimeByteStringValue odd => [.. MemoryMarshal.AsBytes(odd.Text.AsSpan()), odd.TrailingByte],
        { BoxedValue: string text } => MemoryMarshal.AsBytes(text.AsSpan()).ToArray(),
        _ => [],
    };

    /// <summary>
    /// The runtime value of the <c>String</c> whose data is <paramref name="count"/> bytes of the data of <paramref name="value"/> from
    /// <paramref name="index"/>: what <see cref="FromBytes"/> makes of that part of <see cref="BytesOf"/>, read where the value holds its data.
    /// </summary>
    /// <remarks>
    /// A part of whole characters from the start of a character is a substring of the characters. Any other part is made from its own bytes, read where
    /// the value holds them; one that ends in the trailing byte, from a copy of its own bytes. None of the rest of the data is copied.
    /// </remarks>
    /// <param name="value">The runtime value of a <c>String</c>.</param>
    /// <param name="index">The 0-based position of the first byte.</param>
    /// <param name="count">How many bytes to take.</param>
    /// <returns>The runtime value of the <c>String</c> of those bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Some of the bytes are not in the data.</exception>
    public static IRuntimeValue<string> SliceOf(IRuntimeValue value, int index, int count)
    {
        var (text, trailing) = value switch
        {
            VBRuntimeByteStringValue odd => (odd.Text, (byte?)odd.TrailingByte),
            { BoxedValue: string whole } => (whole, null),
            _ => (string.Empty, null),
        };

        var characters = MemoryMarshal.AsBytes(text.AsSpan());
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, characters.Length + (trailing is null ? 0 : 1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, characters.Length + (trailing is null ? 0 : 1) - index);

        if (count == 0)
        {
            return new VBRuntimeValue<string>(string.Empty);
        }

        if (index + count <= characters.Length)
        {
            return index % 2 == 0 && count % 2 == 0
                ? new VBRuntimeValue<string>(text.Substring(index / 2, count / 2))
                : FromBytes(characters.Slice(index, count));
        }

        Span<byte> bytes = count <= 256 ? stackalloc byte[count] : new byte[count];
        characters[index..].CopyTo(bytes);
        bytes[^1] = trailing!.Value;
        return FromBytes(bytes);
    }

    /// <inheritdoc/>
    public override string ToString() => Text;
}
