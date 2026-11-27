using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Runtime.Abstract.Execution;
using RDCore.SDK.Runtime.Shared;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents a value of a <see cref="VBUserDefinedType"/>. Like <see cref="VBObjectValue"/> the
/// value is a <see cref="MemoryAddress"/>: a UDT has location identity and is never passed by value.
/// </summary>
/// <remarks>
/// A record is a view of a <see cref="ValueBlock"/>, which holds its fields as its <see cref="RecordShape"/> lays them out: a field of a fixed-width
/// intrinsic type as bytes, a nested record inline, and any other field in a reference slot. A nested record is a view of the same block at the nested
/// record's offsets, and an element of an array of records is a view of the array's block at the element's offsets. Declaration order is the order
/// <strong>MS-VBAL §5.4.5.11</strong> writes a record in, and the order <see cref="VBUserDefinedTypeLayout"/> assigns offsets in, and a field's index in
/// this type's members is its position in declaration order.
/// </remarks>
public record class VBUserDefinedTypeValue : VBTypedValue,
    IVBTypedValue<VBUserDefinedTypeValue, MemoryAddress>
{
    private readonly ValueBlock _block;
    private readonly RecordShape _shape;
    private readonly int _offset;
    private readonly int _slot;
    private readonly int _place;

    /// <summary>
    /// A record of <paramref name="typeInfo"/> bound to <paramref name="handle"/>, each field its type's default value.
    /// </summary>
    /// <param name="handle">The binding of the record.</param>
    /// <param name="typeInfo">The type of the record.</param>
    public VBUserDefinedTypeValue(IBindingHandle handle, VBUserDefinedType typeInfo)
        : this(handle, typeInfo, fieldDefaults: null) { }

    /// <summary>
    /// A record of <paramref name="typeInfo"/> whose fields each start as <paramref name="fieldDefaults"/> says a variable declared as the field
    /// is starts.
    /// </summary>
    /// <remarks>
    /// A field is declared as a variable is (<strong>MS-VBAL §5.2.3.3</strong>), and a fixed-size array field is as big as its declaration's
    /// bounds, constant expressions that the field's symbol carries and that only the runtime reduces: the type's own default gives such a field
    /// no dimensions. A field held as bytes starts as zero bytes: 0, <c>False</c>, or the date whose serial is 0, the default of each type such a field
    /// has. This is the record a declared variable of the type starts as.
    /// </remarks>
    /// <param name="typeInfo">The type of the record.</param>
    /// <param name="fieldDefaults">What each field, a typed symbol, starts as.</param>
    public VBUserDefinedTypeValue(VBUserDefinedType typeInfo, IVariableDefaults fieldDefaults)
        : this(new ValueBindingHandle(new VBRuntimeReference(MemoryAddress.Zero)), typeInfo, fieldDefaults) { }

    // without defaults, each field starts as its type's default.
    private VBUserDefinedTypeValue(IBindingHandle handle, VBUserDefinedType typeInfo, IVariableDefaults? fieldDefaults)
        : this(handle, new ValueBlock(RecordShape.Of(typeInfo)))
    {
        Start(fieldDefaults);
    }

    /// <summary>
    /// A record bound to <paramref name="reference"/>, each field its type's default value.
    /// </summary>
    /// <param name="reference">The address of the record.</param>
    /// <param name="typeInfo">The type of the record.</param>
    public VBUserDefinedTypeValue(MemoryAddress reference, VBUserDefinedType typeInfo)
        : this(new ValueBindingHandle(new VBRuntimeReference(reference)), typeInfo) { }

    /// <summary>
    /// A record bound to no address, each field its type's default value.
    /// </summary>
    /// <param name="typeInfo">The type of the record.</param>
    public VBUserDefinedTypeValue(VBUserDefinedType typeInfo)
        : this(MemoryAddress.Zero, typeInfo) { }

    /// <summary>
    /// The record <paramref name="block"/> holds, bound to <paramref name="handle"/>: a view of the block, as it is.
    /// </summary>
    /// <param name="handle">The binding of the record.</param>
    /// <param name="block">The block of the record.</param>
    /// <exception cref="ArgumentException"><paramref name="block"/> holds the elements of an array, whose <see cref="ValueBlock.Shape"/> is
    /// <see langword="null"/>.</exception>
    public VBUserDefinedTypeValue(IBindingHandle handle, ValueBlock block)
        : this(handle, block, block.Shape ?? throw new ArgumentException("the block holds the elements of an array.", nameof(block)),
            offset: 0, slot: 0, place: 0) { }

    // a view of block at the offsets of a record of shape: the record itself, or a record it nests.
    private VBUserDefinedTypeValue(IBindingHandle handle, ValueBlock block, RecordShape shape, int offset, int slot, int place)
        : base(shape.Type)
    {
        Handle = handle;
        Fields = shape.Type.DeclaredFields;
        (_block, _shape, _offset, _slot, _place) = (block, shape, offset, slot, place);
    }

    // the element at index of an array's block of records of shape, each field as start holds it, or its type's default without one: start is a record of
    // shape that each element of the array starts as, whose bytes are zero and known, as a record's are when it starts.
    internal static VBUserDefinedTypeValue ElementOf(ValueBlock block, RecordShape shape, int index, VBUserDefinedTypeValue? start)
    {
        var element = new VBUserDefinedTypeValue(new ValueBindingHandle(new VBRuntimeReference(MemoryAddress.Zero)), block, shape, index * shape.ByteSize,
            index * shape.SlotCount, index * shape.PlaceCount);
        if (shape.SlotCount == 0)
        {
            return element;
        }

        if (start is null)
        {
            element.Start(defaults: null);
        }
        else
        {
            element.CopyKnowledgeAndSlots(shape, start, element._slot, element._place, start._slot, start._place);
        }

        return element;
    }

    // the element at index of an array's block of records of source's shape, a copy of source as DataCopy makes one: the element of the array's copy.
    internal static VBUserDefinedTypeValue ElementCopyOf(ValueBlock block, int index, VBUserDefinedTypeValue source)
    {
        var shape = source._shape;
        var copy = new VBUserDefinedTypeValue(InvalidBindingHandle.Default, block, shape, index * shape.ByteSize, index * shape.SlotCount,
            index * shape.PlaceCount);
        copy.CopyFrom(source);
        return copy;
    }

    // a `with` copy is another view of the same record, at the same offsets: a write to a field through one is a write through the other. The copy an
    // assignment makes, which shares nothing, is DataCopy.
    protected VBUserDefinedTypeValue(VBUserDefinedTypeValue original) : base(original)
    {
        Fields = original.Fields;
        (_block, _shape, _offset, _slot, _place) = (original._block, original._shape, original._offset, original._slot, original._place);
    }

    /// <summary>
    /// The block that holds this record's fields.
    /// </summary>
    public ValueBlock Block => _block;

    /// <summary>
    /// The shape of this record, which says where each of its fields is in <see cref="Block"/>, relative to <see cref="ByteOffset"/>,
    /// <see cref="SlotOffset"/> and <see cref="PlaceOffset"/>.
    /// </summary>
    public RecordShape Shape => _shape;

    /// <summary>
    /// Where this record's bytes start in <see cref="Block"/>: 0 for a record that is not nested, the nested record's offset for a record nested in another.
    /// </summary>
    public int ByteOffset => _offset;

    /// <summary>
    /// Where this record's reference slots start in <see cref="Block"/>.
    /// </summary>
    public int SlotOffset => _slot;

    /// <summary>
    /// Where this record's places start in <see cref="Block"/>.
    /// </summary>
    public int PlaceOffset => _place;

    /// <summary>
    /// A copy of this record that shares no cell with it: the copy an assignment makes of a user-defined type (<see cref="DataValues"/>).
    /// </summary>
    /// <remarks>
    /// The copy is a block of its own: the bytes of this record copied, and each reference field a copy in a cell of its own, deep through an array and
    /// what a <c>Variant</c> holds. The copy is bound to no storage: it is whichever variable it is assigned to that gives it an address.
    /// </remarks>
    public VBUserDefinedTypeValue DataCopy()
    {
        var copy = new VBUserDefinedTypeValue(InvalidBindingHandle.Default, new ValueBlock(_shape));
        copy.CopyFrom(this);
        return copy;
    }

    private static VBTypedValue CopyOf(VBTypedValue field) => field switch
    {
        VBArrayValue => DataValues.Copy(field),
        // a Variant's value is bound to a cell of its own (VBVariantValue's handle), which a copy must not share whatever it holds.
        VBVariantValue variant when new VBVariantValue(DataValues.Copy(variant.TypedValue)) is var held
            => variant.IsIndeterminate ? held.AsIndeterminate() : held,
        _ => field with { Handle = DataValues.CopyCell(field.Handle) },
    };

    /// <summary>
    /// This type's fields, in declaration order: the same symbols <see cref="VBUserDefinedType.Fields"/>
    /// reports, held here so that a value can name its own fields without resolving its type again.
    /// </summary>
    public ImmutableArray<VBUserDefinedTypeFieldSymbol> Fields { get; init; }

    /// <summary>
    /// The address of the record.
    /// </summary>
    public MemoryAddress Value => ((VBRuntimeReference)RuntimeValue).Value;

    /// <summary>
    /// The in-memory size of this UDT, MS-VBA's own padding included: what <c>LenB</c> reports
    /// (<strong>MS-VBAL §6.1.2.11</strong>).
    /// </summary>
    /// <remarks>
    /// Not the size a <c>Put</c> statement writes, which is the unpadded concatenation of the fields and is
    /// what <c>Len</c> reports of a UDT instead: "<c>Len</c> returns the size as it will be written to the
    /// file". <see cref="VBUserDefinedTypeLayout"/> holds the padding rule and the reasoning behind it.
    /// </remarks>
    public override int Size => Layout.Size;

    /// <summary>
    /// Where each of this value's fields sits in memory.
    /// </summary>
    /// <remarks>
    /// Laid out from the record itself: a fixed-size array field is as wide as its elements, which its value knows and its type does not.
    /// </remarks>
    public VBUserDefinedTypeLayout Layout => VBUserDefinedTypeLayout.Of(this);

    /// <summary>
    /// The value of the field named <paramref name="name"/>, or <c>null</c> when there is no such field.
    /// </summary>
    /// <param name="name">The field name, compared case-insensitively as VBA compares identifiers.</param>
    public VBTypedValue? this[string name] => IndexOf(name) is >= 0 and var index ? ReadField(Stored(index)) : null;

    // a field of a UDT that is not known is not known either, whatever it was assigned: what the UDT holds is as unreliable as the UDT.
    private VBTypedValue ReadField(VBTypedValue field) => IsIndeterminate ? field.AsIndeterminate() : field;

    /// <summary>
    /// The value of the field at <paramref name="index"/> in declaration order, or <c>null</c> when there is
    /// no field there.
    /// </summary>
    /// <remarks>
    /// Declaration order is what a record is written and read in, so a caller walking the fields to
    /// serialize them wants this rather than the name-keyed indexer.
    /// </remarks>
    /// <param name="index">The field's position in declaration order.</param>
    public VBTypedValue? FieldAt(int index) => IsField(index) ? ReadField(Stored(index)) : null;

    /// <summary>
    /// The number of fields the record has.
    /// </summary>
    public int FieldCount => _shape.Fields.Length;

    /// <summary>
    /// The field at <paramref name="index"/> as the record stores it: bound to its own cell, which a write to the field writes to, whether or not
    /// the record is known; <c>null</c> when there is no field there.
    /// </summary>
    /// <remarks>
    /// <see cref="FieldAt"/> reads a field of a record that is not known as a value that is not known either, which is another binding. A caller
    /// that needs the cell itself (what the object a field holds is held by) reads it here. A field held as bytes is bound to its
    /// <see cref="BlockBytesCell"/>, and a nested record is a view of this record's block; both are made on first need and kept.
    /// </remarks>
    /// <param name="index">The field's position in declaration order.</param>
    public VBTypedValue? StoredFieldAt(int index) => IsField(index) ? Stored(index) : null;

    /// <summary>
    /// The values of this record's reference fields, those of the records it nests included, each bound to its own cell as
    /// <see cref="StoredFieldAt"/> reads it, in the order of their slots in <see cref="Block"/>.
    /// </summary>
    /// <remarks>
    /// A field held as bytes holds a number, a date or a <c>Boolean</c>, never an object or a string. A caller that looks for what the record holds by
    /// reference reads these, and makes no cell of a field held as bytes.
    /// </remarks>
    public ReadOnlySpan<VBTypedValue> ReferenceFields => _block.Slots.AsSpan(_slot, _shape.SlotCount);

    /// <summary>
    /// Reads the field at <paramref name="place"/>, held as bytes encoded as <paramref name="encoding"/>, as its payload: an integer, a <c>Boolean</c> as its
    /// -1 or 0 and a <c>Currency</c> as its ten-thousandths in <paramref name="integral"/>; a <c>Single</c>, a <c>Double</c> or the serial of a <c>Date</c>
    /// in <paramref name="floating"/>, the other being 0.
    /// </summary>
    /// <remarks>
    /// The field's bytes are read where the block holds them: no cell is made of the field, no view is made of a record it is in, and no value is boxed.
    /// Whether the field is known is not read (<see cref="ValueBlock.IsKnown"/>).
    /// </remarks>
    /// <param name="place">
    /// The field's place in the record (<see cref="RecordShape.PlaceOf"/>): its position in declaration order for a field of the record itself.
    /// </param>
    /// <param name="encoding">How the field's value is encoded, which the caller expects.</param>
    /// <param name="integral">The payload of an encoding of an integer, a <c>Boolean</c> or a <c>Currency</c>.</param>
    /// <param name="floating">The payload of an encoding of a <c>Single</c> or a <c>Double</c>.</param>
    /// <returns><see langword="false"/> when there is no field there, or it is not held as bytes encoded as <paramref name="encoding"/>.</returns>
    public bool TryReadBytesField(int place, RecordFieldEncoding encoding, out long integral, out double floating)
    {
        if (!_shape.IsBytesAt(place, encoding, out var offset))
        {
            (integral, floating) = (0, 0);
            return false;
        }

        BlockBytesCell.Read(_block.Bytes.AsSpan(_offset + offset), encoding, out integral, out floating);
        return true;
    }

    /// <summary>
    /// Let-assigns a payload to the field at <paramref name="place"/>, held as bytes encoded as <paramref name="encoding"/>, as
    /// <see cref="TryReadBytesField"/> reads one: the field is known, and its cell, if one was made, reads what was written.
    /// </summary>
    /// <remarks>
    /// The payload is a value of the field's type: what a let-coercion to that type makes (<strong>MS-VBAL §5.5.1.2</strong>). No cell is made of the
    /// field, no view is made of a record it is in, and no value is boxed.
    /// </remarks>
    /// <param name="place">
    /// The field's place in the record (<see cref="RecordShape.PlaceOf"/>): its position in declaration order for a field of the record itself.
    /// </param>
    /// <param name="encoding">How the field's value is encoded, which the caller expects.</param>
    /// <param name="integral">The payload of an encoding of an integer, a <c>Boolean</c> or a <c>Currency</c>.</param>
    /// <param name="floating">The payload of an encoding of a <c>Single</c> or a <c>Double</c>.</param>
    /// <returns><see langword="false"/> when there is no field there, or it is not held as bytes encoded as <paramref name="encoding"/>: nothing is
    /// written.</returns>
    /// <exception cref="OverflowException"><paramref name="integral"/> is out of the range of <paramref name="encoding"/>.</exception>
    public bool TryWriteBytesField(int place, RecordFieldEncoding encoding, long integral, double floating)
    {
        if (!_shape.IsBytesAt(place, encoding, out var offset))
        {
            return false;
        }

        BlockBytesCell.Write(_block.Bytes.AsSpan(_offset + offset), encoding, integral, floating);
        var at = _place + place;
        if (_block.MadeAt(at) is VBTypedValue { Handle: BlockBytesCell cell })
        {
            cell.Reread();
        }

        _block.Know(at, isKnown: true);
        return true;
    }

    private bool IsField(int index) => index >= 0 && index < _shape.Fields.Length;

    private VBTypedValue Stored(int index)
    {
        var field = _shape.Fields[index];
        switch (field.Kind)
        {
            case RecordFieldKind.Reference:
                return _block.Slots[_slot + field.Slot];
            case RecordFieldKind.Bytes when _block.MadeAt(_place + field.Place) is VBTypedValue made:
                return made;
            case RecordFieldKind.Bytes:
                var cell = new BlockBytesCell(_block, _offset + field.Offset, _place + field.Place, field.Encoding);
                var value = (field.Symbol.ResolvedType?.DefaultValue ?? VBEmptyValue.Empty) with { Handle = cell };
                _block.Keep(_place + field.Place, value);
                return value;
            case RecordFieldKind.Nested when _block.MadeAt(_place + field.Place) is VBTypedValue nested:
                return nested;
            default:
                var view = new VBUserDefinedTypeValue(new ValueBindingHandle(new VBRuntimeReference(MemoryAddress.Zero)), _block, field.Nested!,
                    _offset + field.Offset, _slot + field.Slot, _place + field.NestedPlace);
                _block.Keep(_place + field.Place, view);
                return view;
        }
    }

    /// <summary>
    /// Replaces the value of the field named <paramref name="name"/>. Mutates the record in place: field
    /// storage is mutable, the way array element storage is.
    /// </summary>
    /// <param name="name">The field name, compared case-insensitively.</param>
    /// <param name="value">The value to store.</param>
    /// <returns><c>false</c> when there is no such field.</returns>
    public bool TrySetField(string name, VBTypedValue value) => TrySetFieldAt(IndexOf(name), value);

    /// <summary>
    /// Replaces the value of the field at <paramref name="index"/> in declaration order.
    /// </summary>
    /// <remarks>
    /// A field held in a reference slot is given <paramref name="value"/> itself. A field held as bytes has <paramref name="value"/> written in its
    /// cell, and a nested record has <paramref name="value"/> let into it (<see cref="TryLetFrom"/>): neither is a value of its own to replace.
    /// </remarks>
    /// <param name="index">The field's position in declaration order.</param>
    /// <param name="value">The value to store.</param>
    /// <returns><c>false</c> when there is no field there, or a nested record is given a value that is not a record of its type.</returns>
    public bool TrySetFieldAt(int index, VBTypedValue value)
    {
        if (!IsField(index))
        {
            return false;
        }

        var field = _shape.Fields[index];
        switch (field.Kind)
        {
            case RecordFieldKind.Reference:
                _block.Slots[_slot + field.Slot] = value;
                return true;
            case RecordFieldKind.Bytes:
                LetBytes(field, value);
                return true;
            default:
                return value is VBUserDefinedTypeValue record && ((VBUserDefinedTypeValue)Stored(index)).TryLetRecord(record);
        }
    }

    /// <summary>
    /// Let-assigns <paramref name="value"/> to the field at <paramref name="index"/> in declaration order, in place (MS-VBAL §5.4.3.8): a <c>ByRef</c>
    /// parameter given the field (<see cref="GetFieldHandle"/>, MS-VBAL §5.3.1.11) reads the value assigned, and the field reads what the parameter is
    /// assigned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A field held as bytes is written in its cell, the cell a <c>ByRef</c> parameter given the field is bound to, and so is a field that holds an object,
    /// or a <c>String</c>. A field declared as a record is let <paramref name="value"/> field by field (<see cref="TryLetFrom"/>), and a fixed-size array
    /// field element by element (<see cref="VBArrayValue.TryLetFrom"/>): the record holds its elements itself. A nested record let a record that is not
    /// known holds its fields, none of them known.
    /// </para>
    /// <para>
    /// A field that holds a <c>Variant</c> or a resizable array is replaced by <paramref name="value"/>, as <see cref="TrySetFieldAt"/> replaces it. A
    /// <c>ByRef</c> parameter given a <c>Variant</c> field is bound to the record and the field's position, so it reads the value that replaced it. A
    /// reference field is replaced the same way when <paramref name="value"/> cannot be written where the field is: a value of another kind than the field
    /// holds, or a fixed-size array that is not known or has other bounds.
    /// </para>
    /// <para>
    /// <paramref name="value"/> is of the field's declared type, which the assignment let-coerced it to, and shares no cell with anything else: an array in it
    /// becomes the field's own. A record let to a nested record is copied into it (<see cref="TryLetFrom"/>), and can be any record of the field's type.
    /// </para>
    /// </remarks>
    /// <param name="index">The field's position in declaration order.</param>
    /// <param name="value">The value to assign, of the field's declared type.</param>
    /// <returns><see langword="false"/> when there is no field at <paramref name="index"/>.</returns>
    public bool TryLetFieldAt(int index, VBTypedValue value)
    {
        if (!IsField(index))
        {
            return false;
        }

        var field = _shape.Fields[index];
        switch (field.Kind)
        {
            case RecordFieldKind.Bytes:
                LetBytes(field, value);
                return true;
            case RecordFieldKind.Nested:
                return value is VBUserDefinedTypeValue record && ((VBUserDefinedTypeValue)Stored(index)).TryLetRecord(record);
            default:
                LetSlot(_slot + field.Slot, value);
                return true;
        }
    }

    // a bytes field let a value of its type: in its cell when one was made of it, which then reads the value let, and in the field's bytes otherwise,
    // which a cell made later reads.
    private void LetBytes(RecordShapeField field, VBTypedValue value)
    {
        var place = _place + field.Place;
        if (_block.MadeAt(place) is VBTypedValue { Handle: BlockBytesCell cell })
        {
            cell.Store(value.RuntimeValue, isKnown: !value.IsIndeterminate);
            return;
        }

        BlockBytesCell.Encode(value.RuntimeValue, _block.Bytes.AsSpan(_offset + field.Offset), field.Encoding);
        _block.Know(place, isKnown: !value.IsIndeterminate);
    }

    // a reference field let value: written in its cell, element by element, or replaced, as TryLetFieldAt says.
    private void LetSlot(int slot, VBTypedValue value)
    {
        switch (_block.Slots[slot])
        {
            case { Handle: IKnowledgeBinding cell } held when IsWrittenInItsCell(held, value):
                cell.Store(value.RuntimeValue, isKnown: !value.IsIndeterminate);
                break;
            case VBFixedSizeArrayValue elements when value is VBFixedSizeArrayValue { IsIndeterminate: false } array && elements.TryLetFrom(array):
                break;
            default:
                _block.Slots[slot] = value;
                break;
        }
    }

    // a record let into this one, in place: a record that is not known leaves each of this one's fields not known.
    private bool TryLetRecord(VBUserDefinedTypeValue record)
    {
        if (!TryLetFrom(record))
        {
            return false;
        }

        if (record.IsIndeterminate)
        {
            Forget();
        }

        return true;
    }

    /// <summary>
    /// Let-assigns <paramref name="source"/> to this record in place (MS-VBAL §5.4.3.8), each field from the same field of <paramref name="source"/>
    /// (<see cref="TryLetFieldAt"/>): a <c>ByRef</c> parameter given a field of this record (MS-VBAL §5.3.1.11) reads the value assigned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bytes of <paramref name="source"/> are copied over this record's at once, and each of its reference fields is let as <see cref="TryLetFieldAt"/>
    /// lets it, a copy of the source's field: the record let is a copy of <paramref name="source"/> (<strong>MS-VBAL §5.5.1.2.8</strong>), written where
    /// this record is. <paramref name="source"/> can be any record of this record's type, the record itself included, and shares no cell with this record
    /// afterwards.
    /// </para>
    /// <para>
    /// A <c>String</c> or object field held in a cell of its own has the source's value itself written in that cell: a <c>String</c> or an object
    /// reference is replaced by a write, never changed in place, so the two records share no write. A field that is replaced (a <c>Variant</c>, an array,
    /// or a value of another kind than the field holds) is given a copy that shares no cell with the source, as <see cref="DataCopy"/> makes, and so is
    /// the source of a fixed-size array field let element by element.
    /// </para>
    /// </remarks>
    /// <param name="source">The record assigned, of this record's type.</param>
    /// <returns><see langword="false"/> when <paramref name="source"/> is of another type, and nothing is written.</returns>
    public bool TryLetFrom(VBUserDefinedTypeValue source)
    {
        if (!Equals(TypeInfo, source.TypeInfo) || source.FieldCount != FieldCount)
        {
            return false;
        }

        if (ReferenceEquals(_shape, source._shape))
        {
            CopyFrom(source);
            return true;
        }

        // two types equal by their declarations, each with a shape of its own: the record is let field by field.
        for (var index = 0; index < FieldCount; index++)
        {
            var field = _shape.Fields[index];
            if (field.Kind == RecordFieldKind.Reference)
            {
                LetSlotFrom(_slot + field.Slot, source.Stored(index));
            }
            else
            {
                _ = TryLetFieldAt(index, source.Stored(index));
            }
        }

        return true;
    }

    // the bytes of source over this record's, and whether each is known; then each reference field of source let into this record's.
    private void CopyFrom(VBUserDefinedTypeValue source)
    {
        source._block.Bytes.AsSpan(source._offset, _shape.ByteSize).CopyTo(_block.Bytes.AsSpan(_offset, _shape.ByteSize));
        _block.Written();
        CopyKnowledgeAndSlots(_shape, source, _slot, _place, source._slot, source._place);
    }

    private void CopyKnowledgeAndSlots(RecordShape shape, VBUserDefinedTypeValue source, int slot, int place, int sourceSlot, int sourcePlace)
    {
        foreach (var field in shape.Fields)
        {
            switch (field.Kind)
            {
                case RecordFieldKind.Bytes:
                    _block.Know(place + field.Place, source._block.IsKnown(sourcePlace + field.Place));
                    break;
                case RecordFieldKind.Nested:
                    CopyKnowledgeAndSlots(field.Nested!, source, slot + field.Slot, place + field.NestedPlace, sourceSlot + field.Slot,
                        sourcePlace + field.NestedPlace);
                    break;
                default:
                    LetSlotFrom(slot + field.Slot, source._block.Slots[sourceSlot + field.Slot]);
                    break;
            }
        }
    }

    // a reference field let the value of another record's field: written in its cell when it is written there, or given a copy of it, which shares no cell
    // with the other record. A slot of a block just made holds nothing yet, and is given the copy.
    private void LetSlotFrom(int slot, VBTypedValue source)
    {
        if (_block.Slots[slot] is { Handle: IKnowledgeBinding cell } held && IsWrittenInItsCell(held, source))
        {
            cell.Store(source.RuntimeValue, isKnown: !source.IsIndeterminate);
        }
        else if (_block.Slots[slot] is null)
        {
            _block.Slots[slot] = CopyOf(source);
        }
        else
        {
            LetSlot(slot, CopyOf(source));
        }
    }

    // each field of this record not known: a bytes field forgotten in its cell, a reference field read as not known, a nested record's fields the same.
    private void Forget()
    {
        for (var index = 0; index < _shape.Fields.Length; index++)
        {
            var field = _shape.Fields[index];
            switch (field.Kind)
            {
                case RecordFieldKind.Bytes:
                    _block.Know(_place + field.Place, isKnown: false);
                    break;
                case RecordFieldKind.Nested:
                    ((VBUserDefinedTypeValue)Stored(index)).Forget();
                    break;
                default:
                    _block.Slots[_slot + field.Slot] = _block.Slots[_slot + field.Slot].AsIndeterminate();
                    break;
            }
        }
    }

    /// <summary>
    /// Resets the field at <paramref name="index"/> in declaration order to the default value of its declared type, as a new record holds it. The field
    /// is written in place wherever <see cref="TryLetFieldAt"/> writes in place.
    /// </summary>
    /// <remarks>
    /// A field declared as a record has each of its own fields reset, and a fixed-size array field each of its elements
    /// (<see cref="VBArrayValue.ResetElements"/>), which keeps its bounds. Any other field is let a copy of the type's default value, in a cell of its own:
    /// the default value of a type is one value every caller shares, and a field that held it would share its cell with every other.
    /// </remarks>
    /// <param name="index">The field's position in declaration order.</param>
    /// <returns><see langword="false"/> when there is no field at <paramref name="index"/>.</returns>
    public bool TryResetFieldAt(int index)
    {
        if (!IsField(index))
        {
            return false;
        }

        switch (Stored(index))
        {
            case VBUserDefinedTypeValue nested:
                for (var field = 0; field < nested.FieldCount; field++)
                {
                    _ = nested.TryResetFieldAt(field);
                }

                return true;
            case VBFixedSizeArrayValue elements:
                elements.ResetElements();
                return true;
            default:
                return TryLetFieldAt(index, DataValues.Copy(Fields[index].ResolvedType?.DefaultValue ?? VBEmptyValue.Empty));
        }
    }

    // a field that holds an object is written where it is, given an object, and so is a field of another type given a value of its type: the cell of each
    // is the one a ByRef parameter given the field is bound to. A field of another kind is replaced, as a value of another type would have to be. The test
    // for the one Nothing every caller shares is defensive: a field starts in a cell of its own (Fresh), and a write that puts the shared one there would
    // make every Nothing in the session what the next write to the field holds.
    private static bool IsWrittenInItsCell(VBTypedValue held, VBTypedValue value) => held switch
    {
        VBObjectValue => value is VBObjectValue && !ReferenceEquals(held.Handle, VBObjectValue.Nothing.Handle),
        VBVariantValue or VBArrayValue or VBUserDefinedTypeValue => false,
        _ => held.GetType() == value.GetType() && Equals(held.TypeInfo, value.TypeInfo),
    };

    /// <summary>
    /// The binding of the field named <paramref name="name"/>, or <c>null</c> when there is no such field.
    /// </summary>
    /// <remarks>
    /// What a <c>ByRef</c> argument of a UDT field needs, and what an assignment to one writes through:
    /// neither of which constructs a value, so neither goes through the indexer.
    /// </remarks>
    /// <param name="name">The field name, compared case-insensitively.</param>
    public IBindingHandle? GetFieldHandle(string name) => IndexOf(name) is >= 0 and var index ? Stored(index).Handle : null;

    /// <inheritdoc/>
    public bool Equals(IVBTypedValue<VBUserDefinedTypeValue, MemoryAddress>? other) => Value.Value.Equals(other?.Value.Value);

    /// <summary>
    /// Reserves storage sized for this UDT through <paramref name="storage"/>, and returns a copy of
    /// this value bound to the resulting address. A UDT's identity is its address, so the allocated
    /// copy reports the address it was allocated at.
    /// </summary>
    /// <param name="storage">The session storage the record is given an address in.</param>
    /// <param name="allocated">The view of this record bound to its address; <c>null</c> when none was reserved.</param>
    /// <returns><c>false</c> if the underlying memory space is exhausted.</returns>
    public bool TryAllocateIn(ISessionStorage storage, [NotNullWhen(true)] out VBUserDefinedTypeValue? allocated)
    {
        if (!storage.TryAllocate(Size, InvalidBindingHandle.Default, out var address))
        {
            allocated = null;
            return false;
        }

        allocated = (VBUserDefinedTypeValue)WithRuntimeValue(new VBRuntimeReference(address));
        storage.TryRebind(address, allocated.Handle);
        return true;
    }

    private int IndexOf(string name)
    {
        for (var index = 0; index < Fields.Length; index++)
        {
            if (Fields[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    // each field as a variable declared as it starts (MS-VBAL gives a UDT no initializer): a bytes field as its zero bytes (the default value of each of
    // its types is 0, False, or the date whose serial is 0), a nested record's fields the same, and a reference field a fresh value of its own.
    private void Start(IVariableDefaults? defaults)
    {
        for (var index = 0; index < _shape.Fields.Length; index++)
        {
            var field = _shape.Fields[index];
            switch (field.Kind)
            {
                case RecordFieldKind.Nested:
                    StartNested(index, field, defaults);
                    break;
                case RecordFieldKind.Reference:
                    var value = defaults is null ? field.Symbol.ResolvedType?.DefaultValue ?? VBEmptyValue.Empty : defaults.DefaultValueOf(field.Symbol);
                    _block.Slots[_slot + field.Slot] = Fresh(value);
                    break;
            }
        }
    }

    // a nested record's fields as they start, without the view of it, which is made when the record is read: a nested record of bytes alone starts as its
    // zero bytes.
    private void StartNested(int index, RecordShapeField field, IVariableDefaults? defaults)
    {
        if (field.Nested!.SlotCount > 0)
        {
            ((VBUserDefinedTypeValue)Stored(index)).Start(defaults);
        }
    }

    // A fresh value per reference field so a write to one never aliases another: a type's default value is cached and reused by every caller, and both
    // binding kinds mutate in place, so storing it directly would let a write to one field reach every other value that started from it: the same hazard
    // SymbolAddressTable.FreshBinding exists for.
    private static VBTypedValue Fresh(VBTypedValue value) => value switch
    {
        VBArrayValue array => array with { },
        { Handle: ValueBindingHandle } scalar => scalar.WithRuntimeValue(scalar.RuntimeValue),
        var other => other,
    };
}
