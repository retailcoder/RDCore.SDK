using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Runtime;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Where each field of a record of a <see cref="VBUserDefinedType"/> is held in its <see cref="ValueBlock"/>: a field of a fixed-width intrinsic type as
/// bytes of the block, a nested record inline, as its own shape lays it out, and any other field in a reference slot of the block.
/// </summary>
/// <remarks>
/// <para>
/// The bytes of a block are laid out by the shape in declaration order, a bytes field at an offset aligned to its own width and a nested record at a
/// multiple of 8. They are not the record's memory image (<see cref="VBUserDefinedTypeLayout"/>), which depends on the pointer width and on the lengths
/// of fixed-size array fields: a shape is one per type, so that an offset into the bytes is a constant for the type.
/// </para>
/// <para>
/// A field is held as bytes when its type is <c>Byte</c>, <c>Integer</c>, <c>Boolean</c>, <c>Long</c>, <c>LongLong</c>, <c>LongPtr</c>, <c>Single</c>,
/// <c>Double</c>, <c>Currency</c>, <c>Date</c> or an <c>Enum</c>. A <c>String</c>, fixed-length or not, an object, a <c>Variant</c>, an array of either
/// kind and a <c>Decimal</c> are held in a reference slot, as the typed value a field of the type holds.
/// </para>
/// </remarks>
public sealed class RecordShape
{
    private static readonly ConditionalWeakTable<VBUserDefinedType, RecordShape> Shapes = new();

    // the bytes field at each place, the fields of the records it nests included, as its offset in the record's bytes and its encoding: None at a place that
    // is not a bytes field.
    private readonly (int Offset, RecordFieldEncoding Encoding)[] _bytesAt;

    private RecordShape(VBUserDefinedType type, ImmutableArray<RecordShapeField> fields, int byteSize, int slotCount, int placeCount)
    {
        Type = type;
        Fields = fields;
        ByteSize = byteSize;
        SlotCount = slotCount;
        PlaceCount = placeCount;
        _bytesAt = new (int, RecordFieldEncoding)[placeCount];
        foreach (var field in fields)
        {
            if (field.Kind == RecordFieldKind.Bytes)
            {
                _bytesAt[field.Place] = (field.Offset, field.Encoding);
            }
            else if (field.Nested is { } nested)
            {
                for (var place = 0; place < nested.PlaceCount; place++)
                {
                    if (nested._bytesAt[place] is { Encoding: not RecordFieldEncoding.None } inner)
                    {
                        _bytesAt[field.NestedPlace + place] = (field.Offset + inner.Offset, inner.Encoding);
                    }
                }
            }
        }
    }

    /// <summary>
    /// The shape of the records of <paramref name="type"/>, made once per type and kept as long as the type is.
    /// </summary>
    /// <param name="type">The user-defined type.</param>
    /// <returns>The type's shape.</returns>
    public static RecordShape Of(VBUserDefinedType type) => Shapes.GetValue(type, static type => Make(type, [type]));

    /// <summary>
    /// The type whose records the shape lays out.
    /// </summary>
    public VBUserDefinedType Type { get; }

    /// <summary>
    /// Where each field of the type is held, in declaration order.
    /// </summary>
    public ImmutableArray<RecordShapeField> Fields { get; }

    /// <summary>
    /// How many bytes a record of the type holds, the bytes of the records it nests included.
    /// </summary>
    public int ByteSize { get; }

    /// <summary>
    /// How many reference slots a record of the type holds, the slots of the records it nests included.
    /// </summary>
    public int SlotCount { get; }

    /// <summary>
    /// How many places a record of the type has for what is made of its fields on first need: one per field, and the places of the records it nests.
    /// </summary>
    public int PlaceCount { get; }

    /// <summary>
    /// The place of the field that <paramref name="path"/> names, relative to the record's first place: a field of the record itself by its position in
    /// declaration order, a field of a record it nests by the nested record's position, then the field's position in that record, and so on.
    /// </summary>
    /// <remarks>
    /// A field of the record itself is at the place of its position. A place does not depend on the pointer width, so a place found when code is compiled
    /// holds when it runs. The offset of a field's bytes can depend on it, when a <c>LongPtr</c> field comes before the field.
    /// </remarks>
    /// <param name="path">The position of the field at each level, the outermost first.</param>
    /// <returns>
    /// The field's place, which <see cref="VBUserDefinedTypeValue.TryReadBytesField"/> and <see cref="VBUserDefinedTypeValue.TryWriteBytesField"/> take;
    /// -1 when the path is empty, a position is out of range, or a field before the last is not a nested record.
    /// </returns>
    public int PlaceOf(ReadOnlySpan<int> path)
    {
        var (shape, first) = (this, 0);
        for (var level = 0; level < path.Length; level++)
        {
            if (path[level] < 0 || path[level] >= shape.Fields.Length)
            {
                return -1;
            }

            var field = shape.Fields[path[level]];
            if (level == path.Length - 1)
            {
                return first + field.Place;
            }

            if (field.Nested is not { } nested)
            {
                return -1;
            }

            (shape, first) = (nested, first + field.NestedPlace);
        }

        return -1;
    }

    // the offset of the bytes field at place, when it is one encoded as encoding.
    internal bool IsBytesAt(int place, RecordFieldEncoding encoding, out int offset)
    {
        offset = place >= 0 && place < _bytesAt.Length && encoding != RecordFieldEncoding.None && _bytesAt[place].Encoding == encoding
            ? _bytesAt[place].Offset
            : -1;
        return offset >= 0;
    }

    private static RecordShape Make(VBUserDefinedType type, HashSet<VBUserDefinedType> nesting)
    {
        var declared = type.DeclaredFields;
        var fields = ImmutableArray.CreateBuilder<RecordShapeField>(declared.Length);
        var (bytes, slots, places) = (0, 0, declared.Length);
        foreach (var field in declared)
        {
            var place = fields.Count;
            if (field.ResolvedType is VBUserDefinedType nestedType && nesting.Add(nestedType))
            {
                var nested = Make(nestedType, nesting);
                _ = nesting.Remove(nestedType);
                bytes = nested.ByteSize == 0 ? bytes : Aligned(bytes, 8);
                fields.Add(new RecordShapeField(field, RecordFieldKind.Nested, bytes, slots, place, places, RecordFieldEncoding.None, nested));
                (bytes, slots, places) = (bytes + nested.ByteSize, slots + nested.SlotCount, places + nested.PlaceCount);
            }
            else if (EncodingOf(field) is var encoding and not RecordFieldEncoding.None)
            {
                var width = WidthOf(encoding);
                bytes = Aligned(bytes, width);
                fields.Add(new RecordShapeField(field, RecordFieldKind.Bytes, bytes, -1, place, -1, encoding, null));
                bytes += width;
            }
            else
            {
                fields.Add(new RecordShapeField(field, RecordFieldKind.Reference, -1, slots, place, -1, RecordFieldEncoding.None, null));
                slots++;
            }
        }

        return new RecordShape(type, fields.MoveToImmutable(), bytes, slots, places);
    }

    private static RecordFieldEncoding EncodingOf(VBUserDefinedTypeFieldSymbol field) => EncodingOf(field.ResolvedType);

    // a field, or an element of an array, is held as bytes when its type's default value is a number, a Boolean or a Date, whose runtime value is one the
    // bytes encode.
    internal static RecordFieldEncoding EncodingOf(VBType? type) => type?.DefaultValue switch
    {
        (VBByteValue or VBIntegerValue or VBBooleanValue or VBLongValue or VBLongLongValue or VBLongPtrValue or VBSingleValue or VBDoubleValue
            or VBCurrencyValue or VBDateValue) and var value => EncodingOf(value.RuntimeValue),
        _ => RecordFieldEncoding.None,
    };

    private static RecordFieldEncoding EncodingOf(IRuntimeValue value) => value switch
    {
        VBRuntimeValue<byte> => RecordFieldEncoding.Byte,
        VBRuntimeValue<short> => RecordFieldEncoding.Int16,
        VBRuntimeBooleanValue => RecordFieldEncoding.Boolean,
        VBRuntimeValue<int> => RecordFieldEncoding.Int32,
        VBRuntimeValue<long> => RecordFieldEncoding.Int64,
        VBRuntimeValue<float> => RecordFieldEncoding.Single,
        VBRuntimeValue<double> => RecordFieldEncoding.Double,
        VBRuntimeValue<VBRuntimeCurrencyValue> => RecordFieldEncoding.Currency,
        _ => RecordFieldEncoding.None,
    };

    /// <summary>
    /// How many bytes a field of <paramref name="encoding"/> takes.
    /// </summary>
    /// <param name="encoding">The encoding of a bytes field.</param>
    /// <returns>The field's width in bytes; 0 for <see cref="RecordFieldEncoding.None"/>.</returns>
    public static int WidthOf(RecordFieldEncoding encoding) => encoding switch
    {
        RecordFieldEncoding.Byte => 1,
        RecordFieldEncoding.Int16 or RecordFieldEncoding.Boolean => 2,
        RecordFieldEncoding.Int32 or RecordFieldEncoding.Single => 4,
        RecordFieldEncoding.Int64 or RecordFieldEncoding.Double or RecordFieldEncoding.Currency => 8,
        _ => 0,
    };

    private static int Aligned(int offset, int alignment) => alignment <= 1 ? offset : (offset + alignment - 1) / alignment * alignment;
}

/// <summary>
/// Where one field of a record is held in its <see cref="ValueBlock"/>.
/// </summary>
/// <param name="Symbol">The field.</param>
/// <param name="Kind">Whether the field is bytes, a nested record or a reference slot.</param>
/// <param name="Offset">The field's offset in the bytes of the record: a bytes field's, or a nested record's first byte; -1 for a reference slot.</param>
/// <param name="Slot">The field's reference slot, or a nested record's first slot; -1 for a bytes field.</param>
/// <param name="Place">The field's place among what is made of the record's fields on first need, relative to the record's first place.</param>
/// <param name="NestedPlace">A nested record's first place, relative to the record's first place; -1 for any other field.</param>
/// <param name="Encoding">How a bytes field's value is encoded; <see cref="RecordFieldEncoding.None"/> for any other field.</param>
/// <param name="Nested">A nested record's shape; <see langword="null"/> for any other field.</param>
public readonly record struct RecordShapeField(
    VBUserDefinedTypeFieldSymbol Symbol, RecordFieldKind Kind, int Offset, int Slot, int Place, int NestedPlace, RecordFieldEncoding Encoding,
    RecordShape? Nested);

/// <summary>
/// Where a field of a record is held.
/// </summary>
public enum RecordFieldKind
{
    /// <summary>
    /// The field's value is bytes of the record, at the field's offset.
    /// </summary>
    Bytes,

    /// <summary>
    /// The field is a record, inline: its bytes and its slots are the record's own, from the field's offset and slot.
    /// </summary>
    Nested,

    /// <summary>
    /// The field's value is a typed value in a reference slot of the record.
    /// </summary>
    Reference,
}

/// <summary>
/// How the value of a bytes field is encoded, little-endian: the runtime value of a field of its type.
/// </summary>
public enum RecordFieldEncoding
{
    /// <summary>
    /// The field is not a bytes field.
    /// </summary>
    None,

    /// <summary>
    /// A <c>Byte</c>, one byte.
    /// </summary>
    Byte,

    /// <summary>
    /// An <c>Integer</c>, two bytes.
    /// </summary>
    Int16,

    /// <summary>
    /// A <c>Boolean</c>, two bytes, 0 for <c>False</c> and -1 for <c>True</c>.
    /// </summary>
    Boolean,

    /// <summary>
    /// A <c>Long</c>, a 32-bit <c>LongPtr</c> or an <c>Enum</c>, four bytes.
    /// </summary>
    Int32,

    /// <summary>
    /// A <c>LongLong</c> or a 64-bit <c>LongPtr</c>, eight bytes.
    /// </summary>
    Int64,

    /// <summary>
    /// A <c>Single</c>, four bytes.
    /// </summary>
    Single,

    /// <summary>
    /// A <c>Double</c> or a <c>Date</c> (its serial value), eight bytes.
    /// </summary>
    Double,

    /// <summary>
    /// A <c>Currency</c>, eight bytes: its value times 10,000.
    /// </summary>
    Currency,
}
