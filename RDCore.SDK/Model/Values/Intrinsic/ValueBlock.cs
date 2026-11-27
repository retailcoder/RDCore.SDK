using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Runtime.Abstract.Execution;
using System.Buffers.Binary;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// The storage of one record of a <see cref="VBUserDefinedType"/>, or of the elements of an array of a fixed-width intrinsic type or of a user-defined type:
/// the bytes of its fields or elements of fixed-width intrinsic types, the reference slots of a record's other fields, and whether each field or element is
/// known.
/// </summary>
/// <remarks>
/// <para>
/// A record's block is laid out as its <see cref="RecordShape"/> says. A <see cref="VBUserDefinedTypeValue"/> is a view of a block, at the offsets of the
/// record it is: the record itself, or a record it nests.
/// </para>
/// <para>
/// An array's block holds its elements side by side in the order <see cref="VBArrayValue.ElementAt"/> reads them. An element of a fixed-width intrinsic type
/// is the width of its encoding (<see cref="VBArrayValue.ElementEncoding"/>), and its place is its flat index. An element that is a record is laid out as its
/// shape says, at its flat index times the shape's <see cref="RecordShape.ByteSize"/>, <see cref="RecordShape.SlotCount"/> and
/// <see cref="RecordShape.PlaceCount"/>, and is a view of the block there.
/// </para>
/// <para>
/// A field's or an element's cell, the binding a <c>ByRef</c> parameter given it is bound to (<strong>MS-VBAL §5.3.1.11</strong>), and the view of a nested
/// record are made on first need and kept for the block's life, so that every parameter given one field or element shares one cell.
/// </para>
/// </remarks>
public sealed class ValueBlock
{
    private readonly int _placeCount;
    private object?[]? _made;
    private bool[]? _forgotten;

    /// <summary>
    /// A block for a record of <paramref name="shape"/>, its bytes zero and its slots empty.
    /// </summary>
    /// <param name="shape">The shape of the record.</param>
    public ValueBlock(RecordShape shape)
        : this(shape, shape.ByteSize == 0 ? [] : new byte[shape.ByteSize], shape.SlotCount == 0 ? [] : new VBTypedValue[shape.SlotCount], shape.PlaceCount)
    {
    }

    // the block of the elements of an array, each one place of width bytes, all zero.
    internal ValueBlock(int length, int width)
        : this(null, length * width == 0 ? [] : new byte[length * width], [], length)
    {
    }

    // the block of the elements of an array of records of shape, laid end to end: their bytes zero and their slots empty.
    internal ValueBlock(RecordShape shape, int length)
        : this(null, length * shape.ByteSize == 0 ? [] : new byte[length * shape.ByteSize],
            length * shape.SlotCount == 0 ? [] : new VBTypedValue[length * shape.SlotCount], length * shape.PlaceCount)
    {
    }

    private ValueBlock(RecordShape? shape, byte[] bytes, VBTypedValue[] slots, int placeCount)
    {
        Shape = shape;
        Bytes = bytes;
        Slots = slots;
        _placeCount = placeCount;
    }

    /// <summary>
    /// The shape of the record the block holds; <see langword="null"/> for the block of an array's elements.
    /// </summary>
    public RecordShape? Shape { get; }

    /// <summary>
    /// The bytes of the record's bytes fields, or of the array's elements, little-endian, each at its offset.
    /// </summary>
    /// <remarks>
    /// A caller that writes these bytes other than through a field's or an element's cell calls <see cref="Written"/> afterwards, so that each cell reads
    /// what was written.
    /// </remarks>
    public byte[] Bytes { get; }

    /// <summary>
    /// The values of the reference fields of the record, or of the array's records, each in the slot its shape gives it; none in the block of an array of a
    /// fixed-width intrinsic type.
    /// </summary>
    public VBTypedValue[] Slots { get; }

    /// <summary>
    /// How many times the bytes were written other than through a field's or an element's cell.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Says the bytes were written other than through a field's or an element's cell: each cell reads its field or element again.
    /// </summary>
    public void Written() => Version++;

    /// <summary>
    /// Whether the field or element at <paramref name="place"/> is known: a bytes field or an element is, until it is told to forget it.
    /// </summary>
    /// <param name="place">The field's place in the block, or the element's flat index.</param>
    /// <returns><see langword="false"/> when the value is not known.</returns>
    public bool IsKnown(int place) => _forgotten is null || !_forgotten[place];

    internal void Know(int place, bool isKnown)
    {
        if (!isKnown)
        {
            (_forgotten ??= new bool[_placeCount])[place] = true;
        }
        else if (_forgotten is not null)
        {
            _forgotten[place] = false;
        }
    }

    // what is made of the fields, on first need: a block whose fields are not read one by one makes none of it.
    internal object? MadeAt(int place) => _made?[place];

    internal void Keep(int place, object made) => (_made ??= new object?[_placeCount])[place] = made;

    // a block of an array's elements that holds what this one holds, and knows what it knows; nothing made of it.
    internal ValueBlock Copy()
    {
        var copy = new ValueBlock(Shape, [.. Bytes], Slots.Length == 0 ? [] : [.. Slots], _placeCount);
        copy._forgotten = _forgotten is null ? null : [.. _forgotten];
        return copy;
    }

    // the first places of an array's block, and their bytes, let what those of another hold: each cell reads its element again.
    internal void LetFrom(ValueBlock source, int places, int width)
    {
        source.Bytes.AsSpan(0, places * width).CopyTo(Bytes);
        for (var place = 0; place < places; place++)
        {
            Know(place, source.IsKnown(place));
        }

        Written();
    }

    // the first places of an array's block zero, and known: each cell reads its default again.
    internal void Clear(int places, int width)
    {
        Array.Clear(Bytes, 0, places * width);
        if (_forgotten is not null)
        {
            Array.Clear(_forgotten, 0, places);
        }

        Written();
    }
}

/// <summary>
/// The cell of a bytes field of a record, or of an element of an array of a fixed-width type: what it holds is the bytes of its
/// <see cref="ValueBlock"/> at its offset, which reading the cell decodes and writing it encodes.
/// </summary>
public sealed class BlockBytesCell : IKnowledgeBinding
{
    private IRuntimeValue? _read;
    private int _readVersion;

    internal BlockBytesCell(ValueBlock block, int offset, int place, RecordFieldEncoding encoding)
    {
        Block = block;
        Offset = offset;
        Place = place;
        Encoding = encoding;
    }

    /// <summary>
    /// The block the field's or the element's bytes are in.
    /// </summary>
    public ValueBlock Block { get; }

    /// <summary>
    /// The offset of the field's or the element's bytes in the block.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// The field's place in the block, or the element's flat index.
    /// </summary>
    public int Place { get; }

    /// <summary>
    /// How the value is encoded.
    /// </summary>
    public RecordFieldEncoding Encoding { get; }

    /// <inheritdoc/>
    public IRuntimeValue Value
    {
        get
        {
            if (_read is null || _readVersion != Block.Version)
            {
                (_read, _readVersion) = (Decode(Block.Bytes.AsSpan(Offset), Encoding), Block.Version);
            }

            return _read;
        }
    }

    /// <inheritdoc/>
    public bool IsKnown => Block.IsKnown(Place);

    /// <inheritdoc/>
    public BindingCapabilities BindingCapabilities => BindingCapabilities.GetValue | BindingCapabilities.SetValue;

    /// <inheritdoc/>
    public IRuntimeValue GetValue(IRuntimeSymbolResolver resolver) => Value;

    /// <inheritdoc/>
    public void SetValue(IRuntimeSymbolResolver resolver, IRuntimeValue value) => Store(value, isKnown: true);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException"><paramref name="value"/> is not a runtime value of the field's type.</exception>
    public void Store(IRuntimeValue value, bool isKnown)
    {
        Encode(value, Block.Bytes.AsSpan(Offset), Encoding);
        (_read, _readVersion) = (value, Block.Version);
        Block.Know(Place, isKnown);
    }

    /// <inheritdoc/>
    public void Forget() => Block.Know(Place, isKnown: false);

    // the field's bytes were written other than through this cell: it decodes them on its next read.
    internal void Reread() => _read = null;

    // the payload of a field's bytes: an integer, a Boolean's -1 or 0 or a Currency's ten-thousandths as a long; a Single or a Double as a double.
    internal static void Read(ReadOnlySpan<byte> bytes, RecordFieldEncoding encoding, out long integral, out double floating)
    {
        (integral, floating) = encoding switch
        {
            RecordFieldEncoding.Byte => ((long)bytes[0], 0d),
            RecordFieldEncoding.Int16 or RecordFieldEncoding.Boolean => ((long)BinaryPrimitives.ReadInt16LittleEndian(bytes), 0d),
            RecordFieldEncoding.Int32 => ((long)BinaryPrimitives.ReadInt32LittleEndian(bytes), 0d),
            RecordFieldEncoding.Int64 or RecordFieldEncoding.Currency => (BinaryPrimitives.ReadInt64LittleEndian(bytes), 0d),
            RecordFieldEncoding.Single => (0L, (double)BinaryPrimitives.ReadSingleLittleEndian(bytes)),
            RecordFieldEncoding.Double => (0L, BinaryPrimitives.ReadDoubleLittleEndian(bytes)),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
    }

    // a payload written to a field's bytes, as Read reads it.
    internal static void Write(Span<byte> bytes, RecordFieldEncoding encoding, long integral, double floating)
    {
        switch (encoding)
        {
            case RecordFieldEncoding.Byte:
                bytes[0] = checked((byte)integral);
                break;
            case RecordFieldEncoding.Int16 or RecordFieldEncoding.Boolean:
                BinaryPrimitives.WriteInt16LittleEndian(bytes, checked((short)integral));
                break;
            case RecordFieldEncoding.Int32:
                BinaryPrimitives.WriteInt32LittleEndian(bytes, checked((int)integral));
                break;
            case RecordFieldEncoding.Int64 or RecordFieldEncoding.Currency:
                BinaryPrimitives.WriteInt64LittleEndian(bytes, integral);
                break;
            case RecordFieldEncoding.Single:
                BinaryPrimitives.WriteSingleLittleEndian(bytes, (float)floating);
                break;
            case RecordFieldEncoding.Double:
                BinaryPrimitives.WriteDoubleLittleEndian(bytes, floating);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(encoding));
        }
    }

    /// <inheritdoc/>
    public IRuntimeValue Invoke(IRuntimeSymbolResolver resolver, IRuntimeValue[] args) => throw new NotSupportedException();

    /// <summary>
    /// The runtime value that <paramref name="bytes"/> encode as <paramref name="encoding"/>.
    /// </summary>
    /// <param name="bytes">The field's bytes, from its offset.</param>
    /// <param name="encoding">How the field's value is encoded.</param>
    /// <returns>The field's value.</returns>
    public static IRuntimeValue Decode(ReadOnlySpan<byte> bytes, RecordFieldEncoding encoding) => encoding switch
    {
        RecordFieldEncoding.Byte => new VBRuntimeValue<byte>(bytes[0]),
        RecordFieldEncoding.Int16 => new VBRuntimeValue<short>(BinaryPrimitives.ReadInt16LittleEndian(bytes)),
        RecordFieldEncoding.Boolean => new VBRuntimeBooleanValue(BinaryPrimitives.ReadInt16LittleEndian(bytes)),
        RecordFieldEncoding.Int32 => new VBRuntimeValue<int>(BinaryPrimitives.ReadInt32LittleEndian(bytes)),
        RecordFieldEncoding.Int64 => new VBRuntimeValue<long>(BinaryPrimitives.ReadInt64LittleEndian(bytes)),
        RecordFieldEncoding.Single => new VBRuntimeValue<float>(BinaryPrimitives.ReadSingleLittleEndian(bytes)),
        RecordFieldEncoding.Double => new VBRuntimeValue<double>(BinaryPrimitives.ReadDoubleLittleEndian(bytes)),
        RecordFieldEncoding.Currency => new VBRuntimeValue<VBRuntimeCurrencyValue>(new VBRuntimeCurrencyValue(BinaryPrimitives.ReadInt64LittleEndian(bytes))),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
    };

    /// <summary>
    /// Writes <paramref name="value"/> to <paramref name="bytes"/>, encoded as <paramref name="encoding"/>.
    /// </summary>
    /// <param name="value">A runtime value of the field's type.</param>
    /// <param name="bytes">The field's bytes, from its offset.</param>
    /// <param name="encoding">How the field's value is encoded.</param>
    /// <exception cref="InvalidOperationException"><paramref name="value"/> is not a runtime value of the field's type.</exception>
    public static void Encode(IRuntimeValue value, Span<byte> bytes, RecordFieldEncoding encoding)
    {
        switch (encoding, value)
        {
            case (RecordFieldEncoding.Byte, VBRuntimeValue<byte> number):
                bytes[0] = number.Value;
                break;
            case (RecordFieldEncoding.Int16, VBRuntimeValue<short> number):
                BinaryPrimitives.WriteInt16LittleEndian(bytes, number.Value);
                break;
            case (RecordFieldEncoding.Boolean, VBRuntimeBooleanValue truth):
                BinaryPrimitives.WriteInt16LittleEndian(bytes, truth.StoredValue);
                break;
            case (RecordFieldEncoding.Int32, VBRuntimeValue<int> number):
                BinaryPrimitives.WriteInt32LittleEndian(bytes, number.Value);
                break;
            case (RecordFieldEncoding.Int64, VBRuntimeValue<long> number):
                BinaryPrimitives.WriteInt64LittleEndian(bytes, number.Value);
                break;
            case (RecordFieldEncoding.Single, VBRuntimeValue<float> number):
                BinaryPrimitives.WriteSingleLittleEndian(bytes, number.Value);
                break;
            case (RecordFieldEncoding.Double, VBRuntimeValue<double> number):
                BinaryPrimitives.WriteDoubleLittleEndian(bytes, number.Value);
                break;
            case (RecordFieldEncoding.Currency, VBRuntimeValue<VBRuntimeCurrencyValue> amount):
                BinaryPrimitives.WriteInt64LittleEndian(bytes, amount.Value.StoredValue);
                break;
            default:
                throw new InvalidOperationException($"a {value.GetType().Name} is no {encoding} value of a record field.");
        }
    }
}
