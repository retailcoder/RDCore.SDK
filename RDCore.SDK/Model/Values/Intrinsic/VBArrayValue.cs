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
    /// (<see cref="IVariableDefaults.ElementDefaultOf"/>); <see langword="null"/> for that default.</param>
    /// <param name="elementCell">Makes the cell each element is stored in, holding the element type's default value, when the runtime stores elements of
    /// <paramref name="itemType"/> in cells of its own (<see cref="IVariableDefaults.ElementCellOf"/>); <see langword="null"/> for a cell that holds the
    /// default value as it is. Not used for an element that is a record, which <paramref name="elementDefault"/> makes.</param>
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
    /// whichever variable it is assigned to that gives it an address.
    /// </remarks>
    public VBArrayValue DataCopy()
    {
        var copy = this with { Handle = InvalidBindingHandle.Default };
        copy._store = _store.Fresh();
        copy._own = null;
        if (!AnyMade)
        {
            return copy;
        }

        // an element whose cell is not made holds the default value, as the copy's own element does until its cell is made.
        for (var index = 0; index < _length; index++)
        {
            if (MadeCell(index) is { } cell)
            {
                copy._store.Adopt(index, DataValues.CopyCell(cell));
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
    /// anything else.
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

        if (!IsWrittenInItsCell(Cell(flatIndex), value.Handle))
        {
            SetCell(flatIndex, value.Handle);
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
    /// Each element of an array is a cell of its own (<see cref="IBindingHandle"/>), where an element of an MS-VBA array is the bytes of its data type in one
    /// block of memory: MS-VBA (64-bit) allocates <c>ReDim b(2147483647) As Byte</c> as long as the memory is there, and RD-VBA refuses an array that size
    /// before making a cell of it. A <c>ReDim</c> of more elements is run-time error 7, <em>Out of memory</em>
    /// (<strong>RD-VBAL §5.4.3.3</strong>).
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
    /// <param name="flatIndex">From 0 to <see cref="Length"/> - 1.</param>
    /// <param name="value">The element's new binding.</param>
    /// <returns><c>false</c> when there is no element at the index.</returns>
    public bool TrySetElementAt(int flatIndex, IBindingHandle value)
    {
        if (flatIndex < 0 || flatIndex >= _length)
        {
            return false;
        }

        SetCell(flatIndex, value);
        return true;
    }

    /// <summary>
    /// Rebinds the element at the given subscripts to <paramref name="value"/>. Mutates the cell in
    /// place — array element storage is mutable. Returns <c>false</c> when the subscripts are out of
    /// bounds or their count does not match the array rank.
    /// </summary>
    public bool TrySetElement(IBindingHandle value, params int[] subscripts)
    {
        var index = LinearIndex(subscripts);
        if (index < 0)
        {
            return false;
        }

        SetCell(index, value);
        return true;
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
    // it: by elementCell where it was given one, holding the element type's default value.
    private sealed class ElementStore(int length, VBType itemType, Func<VBTypedValue>? elementDefault, Func<IBindingHandle>? elementCell)
    {
        private IBindingHandle?[]? _made;

        // what is known of the elements not made yet, which a cell made from now on is told.
        public UnmadeKnowledge Unmade { get; set; } = UnmadeKnowledge.Known;

        public bool AnyMade => _made is not null;

        public IBindingHandle? MadeOrNone(int index) => _made?[index];

        public IBindingHandle Made(int index)
        {
            var made = _made ??= new IBindingHandle?[length];
            if (made[index] is not { } cell)
            {
                cell = made[index] = elementCell?.Invoke() ?? CreateDefaultCell(itemType, elementDefault);
                if (Unmade.IsForgotten && IsForgotten(cell, Unmade.UnlessHolding) && cell is IKnowledgeBinding known)
                {
                    known.Forget();
                }
            }

            return cell;
        }

        // keeps a cell made elsewhere as the one at an index: the copy of an element of another array.
        public void Adopt(int index, IBindingHandle cell) => (_made ??= new IBindingHandle?[length])[index] = cell;

        // a store of the same length and element type, none of whose cells is made, which knows of its elements what this one knows of those not made.
        public ElementStore Fresh() => new(length, itemType, elementDefault, elementCell) { Unmade = Unmade };
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

    // A fresh binding per cell so an element write never aliases another cell. An element that is a record is a record of its own,
    // boxed as a variable stores one: its fields are written where they are, which an element built again at each read would lose. Other
    // complex element types (Variant, Object) whose default value has no readable binding get an inert cell, which a write replaces.
    private static IBindingHandle CreateDefaultCell(VBType itemType, Func<VBTypedValue>? elementDefault)
    {
        if (itemType is VBUserDefinedType record)
        {
            var element = elementDefault?.Invoke() as VBUserDefinedTypeValue ?? new VBUserDefinedTypeValue(record);
            return BindingKnowledge.NewCell(new VBRuntimeValue<VBRuntimeUserDefinedTypeValue>(new VBRuntimeUserDefinedTypeValue(element)), isKnown: true);
        }

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
