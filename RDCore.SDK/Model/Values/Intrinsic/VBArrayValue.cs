using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Runtime.Abstract.Execution;
using RDCore.SDK.Runtime.Shared;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// A <see cref="VBTypedValue"/> representing a runtime value of the <see cref="VBArrayType"/> data type.
/// </summary>
/// <remarks>
/// <para>
/// 👉 The element store is a single flat block of <see cref="IBindingHandle"/> cells addressed in
/// <strong>column-major</strong> order (the first subscript varies fastest, as an OLE <c>SAFEARRAY</c>).
/// Bounds live in <see cref="Dimensions"/> as metadata (<c>LBound</c>/<c>UBound</c>/rank); the cells
/// are mutable storage. An uninitialized array (<c>Dim a()</c>) has no dimensions and no cells.
/// </para>
/// <para>
/// An element's cell is made the first time the element is read, written or given to a caller (<see cref="CellAt"/>), and not when the array is:
/// an array of millions of elements costs nothing until its elements are used. A cell is made as the array's constructor would have made it, holding
/// the element type's default value, so when it is made is not something a caller sees.
/// </para>
/// <para>
/// The elements of an array of a fixed-width intrinsic type (<see cref="ElementEncoding"/>) are bytes of one <see cref="ValueBlock"/>, side by side in the
/// same order. An element's cell is a <see cref="BlockBytesCell"/> of the block, made when the element is read or given to a caller and kept. Writing an
/// element (<see cref="TryWriteElementAt"/>, <see cref="TryLetElementAt"/>, <see cref="TrySetElementAt"/>) makes none, nor does copying, letting,
/// resetting, forgetting or preserving the array.
/// </para>
/// <para>
/// The elements of an array of a user-defined type are records of one <see cref="ValueBlock"/>, laid end to end in the same order. An element's cell holds a
/// view of the block at the element's offsets (<see cref="VBUserDefinedTypeValue"/>), made as other cells are and kept, and a copy of the array
/// (<see cref="DataCopy"/>) copies each record into the copy's block.
/// </para>
/// </remarks>
public abstract record class VBArrayValue : VBTypedValue
{
    // the cells this array and the views a `with` makes of it share, each made as it is first needed.
    private ElementStore _store;

    // the cells this view was given in place of the shared ones (TrySetElementAt), by flat index; null until it is given one.
    private IBindingHandle?[]? _own;

    private readonly int _length;

    /// <summary>
    /// Creates an array value with the given dimension bounds and element type.
    /// </summary>
    /// <param name="dimensions">The lower and upper bound of each dimension, outermost first.</param>
    /// <param name="itemType">The element type. Use <see cref="VBVariantType"/> when unspecified.</param>
    /// <param name="elementDefault">What each element starts as, when it is more than the element type's own default value can say
    /// (<see cref="IVariableDefaults.ElementDefaultOf"/>); <see langword="null"/> for that default. For an array of records it is called once, when the
    /// first element is made, and each element starts as the record it gives.</param>
    /// <param name="elementCell">Makes the cell each element is stored in, holding the element type's default value, when the runtime stores elements of
    /// <paramref name="itemType"/> in cells of its own (<see cref="IVariableDefaults.ElementCellOf"/>); <see langword="null"/> for a cell that holds the
    /// default value as it is. Not used for an element that is a record, which starts as <paramref name="elementDefault"/> says, nor for an element held as
    /// bytes (<see cref="ElementEncoding"/>).</param>
    protected VBArrayValue((int lBound, int uBound)[] dimensions, VBType itemType, Func<VBTypedValue>? elementDefault = null, Func<IBindingHandle>? elementCell = null)
        : base(VBArrayType.TypeInfo)
    {
        ItemType = itemType;
        Dimensions = [.. dimensions.Select(d => new VBArrayDimension(d.lBound, d.uBound))];
        _length = LengthOf(Dimensions);
        _store = new ElementStore(_length, itemType, elementDefault, itemType is VBUserDefinedType ? null : elementCell);
    }

    /// <summary>
    /// Creates an array value bound to <paramref name="handle"/>. The array's own binding is a
    /// <em>storage</em> concern distinct from the element cells; it is currently inert (see the
    /// session-storage follow-up).
    /// </summary>
    protected VBArrayValue(IBindingHandle handle, (int lBound, int uBound)[] dimensions, VBType itemType)
        : this(dimensions, itemType)
    {
        Handle = handle;
    }

    // C# records synthesize `with` from a memberwise copy constructor. This one gives a `with`-derived copy
    // (TryAllocateIn's included) slots of its own, so that replacing an element's cell in one
    // (TrySetElement) does not replace it in the other - but the cells in those slots are the same
    // cells, and a write to one is seen by both: a `with` is another view of the array. The views share
    // the store, so a cell one of them makes is the cell of every other. The copy an assignment makes,
    // which shares nothing, is DataCopy.
    protected VBArrayValue(VBArrayValue original) : base(original)
    {
        ItemType = original.ItemType;
        Dimensions = original.Dimensions;
        _length = original._length;
        _store = original._store;
        _own = original._own is null ? null : [.. original._own];
    }

    // the cell of the element at a flat index in this view, made where it is not yet.
    private IBindingHandle Cell(int index) => _own?[index] ?? _store.Made(index);

    // the cell of the element at a flat index in this view, or none where it is not made yet.
    private IBindingHandle? MadeCell(int index) => _own?[index] ?? _store.MadeOrNone(index);

    // whether any cell of this view is made: an array none of whose cells is made holds its element type's default value in each element.
    private bool AnyMade => _own is not null || _store.AnyMade;

    private void SetCell(int index, IBindingHandle cell) => (_own ??= new IBindingHandle?[_length])[index] = cell;

    /// <summary>
    /// A copy of this array that shares no cell with it: the copy an assignment makes of an array (<see cref="DataValues"/>).
    /// </summary>
    /// <remarks>
    /// Each element is a copy in a cell of its own, deep through what a <c>Variant</c> element holds, and the copy is bound to no storage: it is
    /// whichever variable it is assigned to that gives it an address. Elements held as bytes (<see cref="ElementEncoding"/>) are a copy of the bytes, and
    /// of what is known of them, with no cell made.
    /// </remarks>
    public VBArrayValue DataCopy()
    {
        var copy = this with { Handle = InvalidBindingHandle.Default };
        copy._store = _store.Fresh();
        copy._own = null;
        if (_store.IsBytes)
        {
            // a copy of the bytes, and of what is known of them: no cell is made of either array.
            copy._store.BlockFrom(_store.BlockOrNone);
            return copy;
        }

        if (!AnyMade)
        {
            return copy;
        }

        // an element whose cell is not made holds the default value, as the copy's own element does until its cell is made.
        for (var index = 0; index < _length; index++)
        {
            if (MadeCell(index) is { } cell)
            {
                copy._store.AdoptCopyOf(index, cell);
            }
        }

        return copy;
    }

    /// <summary>
    /// Let-assigns the elements of <paramref name="source"/> to the elements of this array, in place (MS-VBAL §5.4.3.8). A fixed-size array
    /// field of a record is let this way when the record is let, since the record holds the array's elements itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each element is written in its own cell, which a <c>ByRef</c> parameter given the element is bound to (MS-VBAL §5.3.1.11), so the parameter reads
    /// what the element holds after the assignment: an element of an object type or a <c>Variant</c> as much as one of a scalar type. An element that is
    /// a record is let in place in its turn (<see cref="VBUserDefinedTypeValue.TryLetFrom"/>).
    /// </para>
    /// <para>
    /// An element whose cell cannot be written, one never assigned, is given the cell of <paramref name="source"/>'s element instead: no <c>ByRef</c>
    /// parameter is bound to it, since passing the element gives it a cell that can be written first. <paramref name="source"/> is the copy the
    /// assignment made (<see cref="DataCopy"/>), which shares no cell with anything else.
    /// </para>
    /// </remarks>
    /// <param name="source">The array assigned, of this array's element type and bounds.</param>
    /// <returns><see langword="false"/> when <paramref name="source"/> has another element type or other bounds, and nothing is written.</returns>
    public bool TryLetFrom(VBArrayValue source)
    {
        if (!Equals(ItemType, source.ItemType) || !Dimensions.SequenceEqual(source.Dimensions) || _length != source._length)
        {
            return false;
        }

        if (_store.IsBytes)
        {
            // the bytes are written where they are, which each cell made of an element reads.
            LetBytesFrom(source, _length);
            return true;
        }

        if (AnyMade || source.AnyMade)
        {
            for (var index = 0; index < _length; index++)
            {
                // two elements whose cells are not made both hold the default value: there is nothing to write, but what is known of it (below).
                if (MadeCell(index) is null && source.MadeCell(index) is null)
                {
                    continue;
                }

                var (to, from) = (Cell(index), source.Cell(index));
                if (ItemType is VBUserDefinedType ? !IsRecordLetInPlace(to, from) : !IsWrittenInItsCell(to, from))
                {
                    SetCell(index, from);
                }
            }
        }

        // an element not made in either array is let the default value as the source knows it; a record's cell is not written, but its fields.
        if (ItemType is not VBUserDefinedType)
        {
            _store.Unmade = source._store.Unmade;
        }

        return true;
    }

    /// <summary>
    /// Gives each element of this array the default value of its element type, in place: the array keeps its bounds, and a <c>ByRef</c> parameter given
    /// an element (MS-VBAL §5.3.1.11) reads the default.
    /// </summary>
    /// <remarks>
    /// An element that is a record has each of its fields reset in its turn (<see cref="VBUserDefinedTypeValue.TryResetFieldAt"/>). An element never
    /// assigned is left as it is, since it already holds the default.
    /// </remarks>
    public void ResetElements()
    {
        if (_store.IsBytes)
        {
            _store.BlockOrNone?.Clear(_length, _store.Width);
            return;
        }

        // an element whose cell is not made holds the default, which is then known, as a cell written the default is; a record's cell is not written, but
        // its fields.
        if (ItemType is not VBUserDefinedType)
        {
            _store.Unmade = UnmadeKnowledge.Known;
        }

        if (!AnyMade)
        {
            return;
        }

        for (var index = 0; index < _length; index++)
        {
            if (MadeCell(index) is not { } to)
            {
                continue;
            }

            if (ItemType is VBUserDefinedType)
            {
                if (to.Value is VBRuntimeValue<VBRuntimeUserDefinedTypeValue> { StoredValue.UserDefinedType: { } record })
                {
                    for (var field = 0; field < record.FieldCount; field++)
                    {
                        _ = record.TryResetFieldAt(field);
                    }
                }

                continue;
            }

            _ = IsWrittenInItsCell(to, InvalidBindingHandle.Default);
        }
    }

    /// <summary>
    /// Let-assigns <paramref name="value"/> to the element at a flat index, in the order <see cref="ElementAt"/> reads them, in its own cell: a
    /// <c>ByRef</c> parameter given the element (MS-VBAL §5.3.1.11) reads the value assigned, as it does after <see cref="TryLetFrom"/>.
    /// </summary>
    /// <remarks>
    /// An element whose cell cannot be written, one never assigned, is given the cell of <paramref name="value"/> instead, which must share no cell with
    /// anything else. An element held as bytes (<see cref="ElementEncoding"/>) is written what <paramref name="value"/> holds, where it is, and no cell is
    /// made of it.
    /// </remarks>
    /// <param name="flatIndex">From 0 to <see cref="Length"/> - 1.</param>
    /// <param name="value">The value to assign, of the array's element type.</param>
    /// <returns><see langword="false"/> when there is no element at the index.</returns>
    public bool TryLetElementAt(int flatIndex, VBTypedValue value)
    {
        if (flatIndex < 0 || flatIndex >= _length)
        {
            return false;
        }

        if (_store.IsBytes)
        {
            WriteBytes(flatIndex, value.Handle);
        }
        else if (!IsWrittenInItsCell(Cell(flatIndex), value.Handle))
        {
            SetCell(flatIndex, value.Handle);
        }

        return true;
    }

    // an element held as bytes written what a binding holds, as IsWrittenInItsCell writes a cell: the default for a binding that cannot be read. The cell made
    // of the element, if any, is written, so that it reads what it holds; otherwise the bytes are, and no cell is made.
    private void WriteBytes(int index, IBindingHandle from)
    {
        var (value, isKnown) = from.BindingCapabilities.HasFlag(BindingCapabilities.GetValue)
            ? (from.Value, from is not IKnowledgeBinding { IsKnown: false })
            : (ItemType.DefaultValue.RuntimeValue, true);
        if (_store.MadeOrNone(index) is BlockBytesCell cell)
        {
            cell.Store(value, isKnown);
            return;
        }

        var block = _store.Block;
        BlockBytesCell.Encode(value, block.Bytes.AsSpan(index * _store.Width), _store.Encoding);
        block.Know(index, isKnown);
    }

    // the first elements of this array let what those of source hold, both held as bytes of the same encoding: what source never wrote is zero, and known.
    private void LetBytesFrom(VBArrayValue source, int count)
    {
        if (source._store.BlockOrNone is { } from)
        {
            _store.Block.LetFrom(from, count, _store.Width);
        }
        else
        {
            _store.BlockOrNone?.Clear(count, _store.Width);
        }
    }

    /// <summary>
    /// How an element of this array is held as bytes: an element of a fixed-width intrinsic type (<c>Byte</c>, <c>Integer</c>, <c>Boolean</c>, <c>Long</c>,
    /// <c>LongLong</c>, <c>LongPtr</c>, <c>Single</c>, <c>Double</c>, <c>Currency</c>, <c>Date</c>, or an <c>Enum</c>) as the field of a record of the type
    /// is (<see cref="RecordShape"/>); <see cref="RecordFieldEncoding.None"/> for an element held in a cell of its own.
    /// </summary>
    public RecordFieldEncoding ElementEncoding => _store.Encoding;

    /// <summary>
    /// Reads the element at a flat index, held as bytes encoded as <paramref name="encoding"/>, as its payload: an integer, a <c>Boolean</c> as its -1 or 0
    /// and a <c>Currency</c> as its ten-thousandths in <paramref name="integral"/>; a <c>Single</c>, a <c>Double</c> or the serial of a <c>Date</c> in
    /// <paramref name="floating"/>, the other being 0.
    /// </summary>
    /// <remarks>
    /// The element's bytes are read where the array holds them: no cell is made of the element and no value is boxed. Whether the element is known is not
    /// read; an array that is not known (<see cref="VBTypedValue.IsIndeterminate"/>) is read as it holds its bytes.
    /// </remarks>
    /// <param name="flatIndex">From 0 to <see cref="Length"/> - 1, in the order <see cref="ElementAt"/> reads the elements.</param>
    /// <param name="encoding">How the element is encoded, which the caller expects (<see cref="ElementEncoding"/>).</param>
    /// <param name="integral">The payload of an encoding of an integer, a <c>Boolean</c> or a <c>Currency</c>.</param>
    /// <param name="floating">The payload of an encoding of a <c>Single</c> or a <c>Double</c>.</param>
    /// <returns><see langword="false"/> when there is no element at the index, or the elements are not held as bytes encoded as
    /// <paramref name="encoding"/>.</returns>
    public bool TryReadElementAt(int flatIndex, RecordFieldEncoding encoding, out long integral, out double floating)
    {
        if (encoding == RecordFieldEncoding.None || encoding != _store.Encoding || flatIndex < 0 || flatIndex >= _length)
        {
            (integral, floating) = (0, 0);
            return false;
        }

        if (_store.BlockOrNone is not { } block)
        {
            (integral, floating) = (0, 0);
            return true;
        }

        BlockBytesCell.Read(block.Bytes.AsSpan(flatIndex * _store.Width), encoding, out integral, out floating);
        return true;
    }

    /// <summary>
    /// Let-assigns a payload to the element at a flat index, held as bytes encoded as <paramref name="encoding"/>, as <see cref="TryReadElementAt"/> reads
    /// one: the element is known, and its cell, if one was made, reads what was written.
    /// </summary>
    /// <remarks>
    /// The payload is a value of the element type: what a let-coercion to that type makes (<strong>MS-VBAL §5.5.1.2</strong>). No cell is made of the
    /// element and no value is boxed.
    /// </remarks>
    /// <param name="flatIndex">From 0 to <see cref="Length"/> - 1, in the order <see cref="ElementAt"/> reads the elements.</param>
    /// <param name="encoding">How the element is encoded, which the caller expects (<see cref="ElementEncoding"/>).</param>
    /// <param name="integral">The payload of an encoding of an integer, a <c>Boolean</c> or a <c>Currency</c>.</param>
    /// <param name="floating">The payload of an encoding of a <c>Single</c> or a <c>Double</c>.</param>
    /// <returns><see langword="false"/> when there is no element at the index, or the elements are not held as bytes encoded as <paramref name="encoding"/>:
    /// nothing is written.</returns>
    /// <exception cref="OverflowException"><paramref name="integral"/> is out of the range of <paramref name="encoding"/>.</exception>
    public bool TryWriteElementAt(int flatIndex, RecordFieldEncoding encoding, long integral, double floating)
    {
        if (encoding == RecordFieldEncoding.None || encoding != _store.Encoding || flatIndex < 0 || flatIndex >= _length)
        {
            return false;
        }

        var block = _store.Block;
        BlockBytesCell.Write(block.Bytes.AsSpan(flatIndex * _store.Width), encoding, integral, floating);
        if (_store.MadeOrNone(flatIndex) is BlockBytesCell cell)
        {
            cell.Reread();
        }

        block.Know(flatIndex, isKnown: true);
        return true;
    }

    /// <summary>
    /// Gives this array the elements of <paramref name="source"/> that a <c>ReDim Preserve</c> keeps (<strong>MS-VBAL §5.4.3.3</strong>): this array is the
    /// one the statement makes, of <paramref name="source"/>'s element type and rank, and of its bounds but the last dimension's upper bound.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The last subscript varies slowest (<strong>MS-VBAL §5.4.2.4.1</strong>), so each element still in the bounds is at the flat index it has in
    /// <paramref name="source"/>, and the kept elements are the first of either array. An element this array gains holds its type's default value.
    /// </para>
    /// <para>
    /// Elements held as bytes are copied, with what is known of them, and no cell is made. Otherwise each cell made of a kept element becomes the cell of
    /// this array's element; when the elements of <paramref name="source"/> that have no cell are not known, each kept element is given its cell, so that
    /// what is not known of it is kept.
    /// </para>
    /// </remarks>
    /// <param name="source">The array the <c>ReDim Preserve</c> resizes.</param>
    /// <returns><see langword="false"/> when <paramref name="source"/> has another element type, another rank, or other bounds than the last upper one:
    /// nothing is kept.</returns>
    public bool TryPreserveFrom(VBArrayValue source)
    {
        if (!Equals(ItemType, source.ItemType) || Rank != source.Rank || Rank == 0
            || !Dimensions.Take(Rank - 1).SequenceEqual(source.Dimensions.Take(Rank - 1))
            || Dimensions[^1].LowerBound != source.Dimensions[^1].LowerBound)
        {
            return false;
        }

        var kept = Math.Min(_length, source._length);
        if (_store.IsBytes)
        {
            LetBytesFrom(source, kept);
            return true;
        }

        if (!source.AnyMade && !source._store.Unmade.IsForgotten)
        {
            return true;
        }

        // every element without a cell holds the same default value, so what one of them is known to hold, each does.
        var unmadeForgotten = source._store.Unmade.IsForgotten;
        for (var index = 0; index < kept; index++)
        {
            if (source.MadeCell(index) is { } cell)
            {
                _store.Adopt(index, cell);
            }
            else if (unmadeForgotten && source.Cell(index) is { } forgotten)
            {
                _store.Adopt(index, forgotten);
            }
        }

        return true;
    }

    // an element given a cell that can be written holds what the other element holds in that cell, the one a ByRef parameter given it is bound to: an object
    // or a Variant as much as a number, since a Let counts the objects a cell holds before it writes the cell and after. An element never assigned has an
    // inert cell (CreateDefaultCell), which stands for the element type's default.
    private bool IsWrittenInItsCell(IBindingHandle to, IBindingHandle from)
    {
        if (to is not IKnowledgeBinding cell || !to.BindingCapabilities.HasFlag(BindingCapabilities.SetValue))
        {
            return false;
        }

        switch (from)
        {
            case IKnowledgeBinding { IsKnown: var isKnown } when from.BindingCapabilities.HasFlag(BindingCapabilities.GetValue):
                cell.Store(from.Value, isKnown);
                return true;
            case { BindingCapabilities: var capabilities } when !capabilities.HasFlag(BindingCapabilities.GetValue):
                cell.Store(ItemType.DefaultValue.RuntimeValue, isKnown: true);
                return true;
            default:
                return false;
        }
    }

    // an element that holds a record, let the record another element holds: written into the record it holds.
    private static bool IsRecordLetInPlace(IBindingHandle to, IBindingHandle from)
        => to.Value is VBRuntimeValue<VBRuntimeUserDefinedTypeValue> { StoredValue.UserDefinedType: { IsIndeterminate: false } held }
            && from.Value is VBRuntimeValue<VBRuntimeUserDefinedTypeValue> { StoredValue.UserDefinedType: { IsIndeterminate: false } record }
            && held.TryLetFrom(record);

    /// <summary>
    /// The declared <see cref="VBType"/> of the elements in this array.
    /// </summary>
    public VBType ItemType { get; init; }

    /// <summary>
    /// The bounds of each dimension (outermost first). Empty for an uninitialized array.
    /// </summary>
    public ImmutableArray<VBArrayDimension> Dimensions { get; init; }

    /// <summary>
    /// The number of dimensions of this array. <c>0</c> for an uninitialized array.
    /// </summary>
    public int Rank => Dimensions.Length;

    /// <summary>
    /// <c>true</c> if this array has at least one dimension.
    /// </summary>
    public bool IsInitialized => Dimensions.Length > 0;

    /// <summary>
    /// The total number of elements across every dimension.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// The most elements an array holds in RD-VBA: 16,777,216 (2<sup>24</sup>), in every dimension together.
    /// </summary>
    /// <remarks>
    /// An element of an array of a type other than a fixed-width intrinsic one is a cell of its own (<see cref="IBindingHandle"/>), where an element of an
    /// MS-VBA array is the bytes of its data type in one block of memory: MS-VBA (64-bit) allocates <c>ReDim b(2147483647) As Byte</c> as long as the
    /// memory is there, and RD-VBA refuses an array that size, of any element type, before making a cell or a byte of it. A <c>ReDim</c> of more elements
    /// is run-time error 7, <em>Out of memory</em> (<strong>RD-VBAL §5.4.3.3</strong>).
    /// </remarks>
    public const long MaximumLength = 1 << 24;

    /// <summary>
    /// The number of elements an array of the given bounds has, which may be more than an <see cref="int"/> holds: <c>(0, 2147483647)</c> is
    /// 2,147,483,648 of them.
    /// </summary>
    /// <param name="bounds">The lower and upper bound of each dimension.</param>
    /// <returns>
    /// The product of the lengths of the dimensions, or <see cref="MaximumLength"/> + 1 for any product greater than that (which three dimensions of
    /// 2<sup>31</sup> elements would overflow); <c>0</c> when a dimension has an upper bound below its lower bound.
    /// </returns>
    public static long LengthOf(IEnumerable<(int LBound, int UBound)> bounds)
    {
        var lengths = bounds.Select(dimension => Math.Max(0L, (long)dimension.UBound - dimension.LBound + 1)).ToArray();
        if (lengths.Contains(0L))
        {
            return 0;
        }

        var total = 1L;
        foreach (var length in lengths)
        {
            total = Math.Min(total * length, MaximumLength + 1);
        }

        return total;
    }

    /// <inheritdoc/>
    public override int Size => _length == 0 ? 0 : ItemType.DefaultValue.Size * _length;

    /// <summary>
    /// Gets the element at the given subscripts, or <c>null</c> when any subscript is out of bounds
    /// (semantics raise <c>SubscriptOutOfRange</c> in that case) or the subscript count does not match
    /// the array rank.
    /// </summary>
    public VBTypedValue? this[params int[] subscripts]
    {
        get
        {
            var index = LinearIndex(subscripts);
            return index < 0 ? null : ReadElement(Cell(index));
        }
    }

    // an element of an array that is not known is not known either, whatever its own cell says: the cells are shared by every view of the array,
    // so what the array is read as decides.
    private VBTypedValue ReadElement(IBindingHandle cell)
    {
        var element = ItemType.CreateValue(cell.ForReading());
        return IsIndeterminate ? element.AsIndeterminate() : element;
    }

    /// <summary>
    /// Makes the elements that an assignment to an element that could be any of them may have replaced not known.
    /// </summary>
    /// <param name="assigned">
    /// The value the assignment stores, when it is known: an element that already holds it is the same after the assignment whichever element
    /// was assigned, so it stays known. <see langword="null"/> when the value is not known, which leaves no element that is.
    /// </param>
    /// <remarks>
    /// An assignment to <c>A(i)</c> with an unknown <c>i</c> replaces exactly one element, and which one is not known: afterwards each element holds
    /// either what it held or the value assigned. That is a value the array does not know, unless the two are the same.
    /// </remarks>
    public void ForgetElements(IRuntimeValue? assigned = null)
    {
        if (_store.IsBytes)
        {
            var block = _store.Block;
            for (var index = 0; index < _length; index++)
            {
                if (block.IsKnown(index) && !(assigned is not null && Equals(HeldAt(block, index).BoxedValue, assigned.BoxedValue)))
                {
                    block.Know(index, isKnown: false);
                }
            }

            return;
        }

        // an element whose cell is not made holds the default, as a cell made now would: it is forgotten as that cell would be, once it is made.
        _store.Unmade = _store.Unmade.Forget(assigned);
        if (!AnyMade)
        {
            return;
        }

        for (var index = 0; index < _length; index++)
        {
            if (MadeCell(index) is IKnowledgeBinding known && IsForgotten(known, assigned))
            {
                known.Forget();
            }
        }
    }

    // what the element held as bytes at an index holds.
    private IRuntimeValue HeldAt(ValueBlock block, int index) => BlockBytesCell.Decode(block.Bytes.AsSpan(index * _store.Width), _store.Encoding);

    private static bool IsForgotten(IBindingHandle cell, IRuntimeValue? assigned)
        => cell is IKnowledgeBinding { IsKnown: true } known && !(assigned is not null && Equals(known.Value.BoxedValue, assigned.BoxedValue));

    /// <summary>
    /// Gets the element at the given flat (column-major) index, or <c>null</c> when out of bounds.
    /// </summary>
    /// <remarks>
    /// <strong>MS-VBAL §5.4.2.4.1</strong> Array Enumeration Order: the first element has every index
    /// at its dimension's lower bound; the next increments the leftmost (outermost) dimension, carrying
    /// into the next dimension to its right on overflow — exactly this store's own column-major layout,
    /// so a <c>For Each</c> over an array can walk <see cref="Length"/> flat indices directly, without
    /// re-deriving per-dimension subscripts.
    /// </remarks>
    public VBTypedValue? ElementAt(int flatIndex)
        => flatIndex < 0 || flatIndex >= _length ? null : ReadElement(Cell(flatIndex));

    /// <summary>
    /// Gets the binding of the element at the given subscripts, or <c>null</c> when any subscript is out of
    /// bounds or the subscript count does not match the array rank.
    /// </summary>
    /// <remarks>
    /// Unlike the indexer, this never constructs a typed value, so it also reads an element whose type has no
    /// value that can be built from a binding alone (a class, user-defined type or Object element). A cell that was
    /// never assigned holds an inert binding.
    /// </remarks>
    public IBindingHandle? GetElementHandle(params int[] subscripts)
    {
        var index = LinearIndex(subscripts);
        return index < 0 ? null : Cell(index);
    }

    /// <summary>
    /// Gets the binding of the element at a flat index, in the order <see cref="ElementAt"/> reads them, or <c>null</c> when there is no element
    /// there.
    /// </summary>
    /// <remarks>
    /// For whoever walks every cell of the array without subscripts - what the objects its elements hold are held by, each by its own cell.
    /// </remarks>
    /// <param name="flatIndex">From 0 to <see cref="Length"/> - 1.</param>
    public IBindingHandle? CellAt(int flatIndex) => flatIndex < 0 || flatIndex >= _length ? null : Cell(flatIndex);

    /// <summary>
    /// The elements of this array that have a cell, each with its flat index and its cell. Walking them makes no cell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An element is given its cell the first time it is read, written or given to a caller (the indexer, <see cref="ElementAt"/>, <see cref="CellAt"/>,
    /// <see cref="GetElementHandle"/>, <see cref="TryLetElementAt"/>, <see cref="TrySetElementAt"/>), and keeps it. An element without a cell holds the
    /// default value of <see cref="ItemType"/>, which holds no object, and nothing is bound to it: a caller that walks every element for what it holds,
    /// or for what is bound to it, walks these instead and makes no cell for the others.
    /// </para>
    /// <para>
    /// An element held as bytes (<see cref="ElementEncoding"/>) is given its cell only when it is read or given to a caller, and one without a cell holds
    /// what its bytes hold (<see cref="TryReadElementAt"/>). It holds no object either.
    /// </para>
    /// <para>
    /// The elements come in increasing flat index, the column-major order <see cref="ElementAt"/> reads them in (<strong>MS-VBAL §5.4.2.4.1</strong>).
    /// A view of the array made with <c>with</c> yields the cells it was given in place of the shared ones (<see cref="TrySetElementAt"/>), and the shared
    /// cells at every other index.
    /// </para>
    /// <para>
    /// The walk is deferred: it reads each element when it reaches it. A cell made or replaced at an index the walk has not reached yet is the one it
    /// yields. One made or replaced at an index it has passed is not yielded. When no cell is made as the walk starts (its first step), it yields none,
    /// whatever cells are made while it runs.
    /// </para>
    /// </remarks>
    public IEnumerable<(int FlatIndex, IBindingHandle Cell)> MadeCells
    {
        get
        {
            if (!AnyMade)
            {
                yield break;
            }

            for (var index = 0; index < _length; index++)
            {
                if (MadeCell(index) is { } cell)
                {
                    yield return (index, cell);
                }
            }
        }
    }

    /// <summary>
    /// Rebinds the element at a flat index, in the order <see cref="ElementAt"/> reads them, to <paramref name="value"/>: what writes an element at its
    /// address in memory, which is where it is in that order.
    /// </summary>
    /// <remarks>
    /// An element held as bytes (<see cref="ElementEncoding"/>) is not rebound: it is written what <paramref name="value"/> holds, where it is, and its cell
    /// reads it.
    /// </remarks>
    /// <param name="flatIndex">From 0 to <see cref="Length"/> - 1.</param>
    /// <param name="value">The element's new binding.</param>
    /// <returns><c>false</c> when there is no element at the index.</returns>
    public bool TrySetElementAt(int flatIndex, IBindingHandle value)
    {
        if (flatIndex < 0 || flatIndex >= _length)
        {
            return false;
        }

        SetOrWrite(flatIndex, value);
        return true;
    }

    /// <summary>
    /// Rebinds the element at the given subscripts to <paramref name="value"/>, in place: array element storage is mutable.
    /// </summary>
    /// <remarks>
    /// An element held as bytes (<see cref="ElementEncoding"/>) is written what <paramref name="value"/> holds, as <see cref="TrySetElementAt"/> writes it.
    /// </remarks>
    /// <param name="value">The element's new binding.</param>
    /// <param name="subscripts">One subscript per dimension of the array.</param>
    /// <returns><c>false</c> when the subscripts are out of bounds or their count does not match the array's rank.</returns>
    public bool TrySetElement(IBindingHandle value, params int[] subscripts)
    {
        var index = LinearIndex(subscripts);
        if (index < 0)
        {
            return false;
        }

        SetOrWrite(index, value);
        return true;
    }

    private void SetOrWrite(int index, IBindingHandle value)
    {
        if (_store.IsBytes)
        {
            WriteBytes(index, value);
        }
        else
        {
            SetCell(index, value);
        }
    }

    /// <summary>
    /// Reserves storage sized for this array's own slot through <paramref name="storage"/>, and returns
    /// a copy of this value bound to the resulting address. This is the array <em>variable's</em>
    /// identity, distinct from its element cells (which remain the same managed storage): a real
    /// address is what lets <c>ReDim</c>, <c>Erase</c>, and array-identity comparisons have something
    /// to point at, matching how a <c>SAFEARRAY</c> variable's own slot is a pointer, never the array's
    /// contents.
    /// </summary>
    /// <returns><c>false</c> if the underlying memory space is exhausted.</returns>
    public bool TryAllocateIn(ISessionStorage storage, [NotNullWhen(true)] out VBArrayValue? allocated)
    {
        if (!storage.TryAllocate(Size, InvalidBindingHandle.Default, out var address))
        {
            allocated = null;
            return false;
        }

        allocated = (VBArrayValue)WithRuntimeValue(new VBRuntimeReference(address));
        storage.TryRebind(address, allocated.Handle);
        return true;
    }

    /// <summary>
    /// The flat index of the element at the given subscripts, in the order <see cref="ElementAt"/> and <see cref="CellAt"/> read them; <c>-1</c> when any
    /// subscript is out of bounds or the subscript count does not match the array rank (<strong>MS-VBAL §5.6.13</strong>'s error 9, for the caller to
    /// raise).
    /// </summary>
    /// <remarks>
    /// For a caller that already has the subscripts as integers and reads or writes the element's cell itself, without the array of subscripts the
    /// <c>params</c> members are given.
    /// </remarks>
    /// <param name="subscripts">One subscript per dimension, outermost first.</param>
    public int IndexOf(ReadOnlySpan<int> subscripts) => LinearIndex(subscripts);

    private int LinearIndex(ReadOnlySpan<int> subscripts)
    {
        if (_length == 0 || subscripts.Length != Dimensions.Length)
        {
            return -1;
        }

        var index = 0;
        var stride = 1;
        for (var k = 0; k < Dimensions.Length; k++)
        {
            var dimension = Dimensions[k];
            var subscript = subscripts[k];
            if (subscript < dimension.LowerBound || subscript > dimension.UpperBound)
            {
                return -1;
            }

            index += (subscript - dimension.LowerBound) * stride;
            stride *= dimension.Length;
        }

        return index;
    }

    // the number of elements of the given dimensions: 0 for none, or where a dimension has none.
    private static int LengthOf(ImmutableArray<VBArrayDimension> dimensions)
    {
        if (dimensions.Length == 0)
        {
            return 0;
        }

        var total = 1;
        foreach (var dimension in dimensions)
        {
            total *= Math.Max(0, dimension.Length);
        }

        return total;
    }

    // the cells of an array and of the views a `with` makes of it, each made the first time it is needed, as the array's constructor was told to make
    // it: by elementCell where it was given one, holding the element type's default value. The elements of a fixed-width intrinsic type are bytes of one
    // block instead, made on the first write, and a cell of one of them is the cell of its bytes. The elements that are records are records of one block
    // too, laid end to end, each a view of the block in a cell of its own.
    private sealed class ElementStore(int length, VBType itemType, Func<VBTypedValue>? elementDefault, Func<IBindingHandle>? elementCell)
    {
        private IBindingHandle?[]? _made;
        private ValueBlock? _block;

        // what makes an element beyond its type's default: the shape of an element that is a record and the record each starts as, or else elementCell,
        // which an array of records is never given. One field for either, so that an array of another type pays nothing for records.
        private object? _making = itemType is VBUserDefinedType record ? new RecordStart(RecordShape.Of(record)) : elementCell;

        // how an element is held as bytes; None for an element held in a cell, as an element that something other than its type's default starts as is.
        public RecordFieldEncoding Encoding { get; } = elementDefault is null ? RecordShape.EncodingOf(itemType) : RecordFieldEncoding.None;

        public int Width => RecordShape.WidthOf(Encoding);

        public bool IsBytes => Encoding != RecordFieldEncoding.None;

        // the shape of an element that is a record; null for an element of another type.
        public RecordShape? Records => (_making as RecordStart)?.Shape;

        // the block of the elements held as bytes, or of the records, made on first need: an array none of whose elements is written holds zero bytes, each
        // known.
        public ValueBlock Block => _block ??= Records is { } shape ? new ValueBlock(shape, length) : new ValueBlock(length, Width);

        public ValueBlock? BlockOrNone => _block;

        // what is known of the elements not made yet, which a cell made from now on is told.
        public UnmadeKnowledge Unmade { get; set; } = UnmadeKnowledge.Known;

        public bool AnyMade => _made is not null;

        public IBindingHandle? MadeOrNone(int index) => _made?[index];

        public IBindingHandle Made(int index)
        {
            var made = _made ??= new IBindingHandle?[length];
            if (made[index] is not { } cell)
            {
                if (IsBytes)
                {
                    return made[index] = new BlockBytesCell(Block, index * Width, index, Encoding);
                }

                cell = made[index] = Records is { } shape
                    ? RecordCell(RecordAt(index, shape), isKnown: true)
                    : (_making as Func<IBindingHandle>)?.Invoke() ?? CreateDefaultCell(itemType);
                if (Unmade.IsForgotten && IsForgotten(cell, Unmade.UnlessHolding) && cell is IKnowledgeBinding known)
                {
                    known.Forget();
                }
            }

            return cell;
        }

        // the record at an index, a view of the block, its fields as those of the record each element starts as. A record of another shape than the
        // element type's, which a start of a type equal by its declarations would be, is a record of its own.
        private VBUserDefinedTypeValue RecordAt(int index, RecordShape shape)
        {
            var start = ((RecordStart)_making!).StartOf(elementDefault);
            return start is null || ReferenceEquals(start.Shape, shape)
                ? VBUserDefinedTypeValue.ElementOf(Block, shape, index, start)
                : (VBUserDefinedTypeValue)elementDefault!.Invoke();
        }

        // an element that is a record is boxed as a variable stores one: its fields are written where they are, which an element built again at each read
        // would lose.
        private static ValueBindingHandle RecordCell(VBUserDefinedTypeValue record, bool isKnown)
            => BindingKnowledge.NewCell(new VBRuntimeValue<VBRuntimeUserDefinedTypeValue>(new VBRuntimeUserDefinedTypeValue(record)), isKnown);

        // keeps a cell made elsewhere as the one at an index: the copy of an element of another array.
        public void Adopt(int index, IBindingHandle cell) => (_made ??= new IBindingHandle?[length])[index] = cell;

        // keeps a copy of the cell of another array's element as the one at an index, as DataValues.CopyCell makes it: a record of the source's shape is
        // copied into this store's block.
        public void AdoptCopyOf(int index, IBindingHandle cell)
            => Adopt(index, Records is { } shape && cell is IKnowledgeBinding { IsKnown: var isKnown } and not ICopyableBinding
                && cell.BindingCapabilities.HasFlag(BindingCapabilities.GetValue)
                && cell.Value is VBRuntimeValue<VBRuntimeUserDefinedTypeValue> { StoredValue.UserDefinedType: { } record }
                && ReferenceEquals(record.Shape, shape)
                    ? RecordCell(VBUserDefinedTypeValue.ElementCopyOf(Block, index, record), isKnown)
                    : DataValues.CopyCell(cell));

        // the elements held as bytes become a copy of another block's, which no cell is made of yet.
        public void BlockFrom(ValueBlock? block) => _block = block?.Copy();

        // a store of the same length and element type, none of whose cells is made, which knows of its elements what this one knows of those not made.
        public ElementStore Fresh() => new(length, itemType, elementDefault, _making as Func<IBindingHandle>) { Unmade = Unmade, _making = _making };
    }

    // the shape of an array's records, and the record each of them starts as (elementDefault), made once, when the first element is: an array's copies
    // share it, and start their records as it does.
    private sealed class RecordStart(RecordShape shape)
    {
        private VBUserDefinedTypeValue? _start;
        private bool _isMade;

        public RecordShape Shape { get; } = shape;

        public VBUserDefinedTypeValue? StartOf(Func<VBTypedValue>? elementDefault)
        {
            if (!_isMade)
            {
                (_start, _isMade) = (elementDefault?.Invoke() as VBUserDefinedTypeValue, true);
            }

            return _start;
        }
    }

    // what is known of the elements whose cells are not made, all of which hold the same default value: each is known, or each is forgotten as
    // ForgetElements forgets a cell, unless it holds UnlessHolding where that is given. Known without making a cell, which is what tells the default.
    private readonly record struct UnmadeKnowledge(bool IsForgotten, IRuntimeValue? UnlessHolding)
    {
        public static UnmadeKnowledge Known => default;

        // what is known of them after ForgetElements(assigned): an element that holds a value no assignment differs from stays known.
        public UnmadeKnowledge Forget(IRuntimeValue? assigned) => this switch
        {
            { IsForgotten: false } => new(true, assigned),
            { UnlessHolding: { } kept } when assigned is not null && Equals(kept.BoxedValue, assigned.BoxedValue) => this,
            _ => new(true, null),
        };
    }

    // A fresh binding per cell so an element write never aliases another cell. Complex element types (Variant, Object) whose default value has no
    // readable binding get an inert cell, which a write replaces.
    private static IBindingHandle CreateDefaultCell(VBType itemType)
    {
        var defaultValue = itemType.DefaultValue;
        return defaultValue.Handle.BindingCapabilities.HasFlag(BindingCapabilities.GetValue)
            ? new ValueBindingHandle(defaultValue.RuntimeValue)
            : InvalidBindingHandle.Default;
    }

    /// <summary>
    /// The bounds of a single array dimension.
    /// </summary>
    /// <param name="LowerBound">The lower subscript bound, returned by <c>LBound</c>.</param>
    /// <param name="UpperBound">The upper subscript bound, returned by <c>UBound</c>.</param>
    public record class VBArrayDimension(int LowerBound, int UpperBound)
    {
        /// <summary>
        /// The number of elements in this dimension. <c>0</c> when the bounds are inverted.
        /// </summary>
        public int Length => IsInitialized ? UpperBound - LowerBound + 1 : 0;

        /// <summary>
        /// <c>true</c> when <see cref="LowerBound"/> is less than or equal to <see cref="UpperBound"/>.
        /// </summary>
        public bool IsInitialized => LowerBound <= UpperBound;

        /// <summary>
        /// Deconstructs this dimension into its lower and upper bounds:
        /// <c>var (lower, upper) = arrayValue.Dimensions[0];</c>
        /// </summary>
        public void Deconstruct(out int lBound, out int uBound)
        {
            lBound = LowerBound;
            uBound = UpperBound;
        }
    }
}
