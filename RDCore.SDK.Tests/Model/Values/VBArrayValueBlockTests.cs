using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.Tests.Model.Values;

/// <summary>
/// The elements of an array of a fixed-width intrinsic type are bytes of one block: read and written as payloads without a cell, a cell made only when an
/// element is read or given to a caller and kept, and copied, let, reset, forgotten and preserved over the bytes.
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.5 Runtime Values")]
public sealed class VBArrayValueBlockTests
{
    private static VBFixedSizeArrayValue NewLongs(int uBound) => new([(0, uBound)], VBLongType.TypeInfo);

    private static object? Read(VBArrayValue array, int index) => array.ElementAt(index)!.RuntimeValue.BoxedValue;

    private static bool IsKnown(VBArrayValue array, int index) => ((IKnowledgeBinding)array.CellAt(index)!).IsKnown;

    private static long Payload(VBArrayValue array, int index)
    {
        Assert.IsTrue(array.TryReadElementAt(index, RecordFieldEncoding.Int32, out var integral, out _));
        return integral;
    }

    public static IEnumerable<object[]> Encodings =>
    [
        [VBByteType.TypeInfo, RecordFieldEncoding.Byte],
        [VBIntegerType.TypeInfo, RecordFieldEncoding.Int16],
        [VBBooleanType.TypeInfo, RecordFieldEncoding.Boolean],
        [VBLongType.TypeInfo, RecordFieldEncoding.Int32],
        [VBLongLongType.TypeInfo, RecordFieldEncoding.Int64],
        [VBSingleType.TypeInfo, RecordFieldEncoding.Single],
        [VBDoubleType.TypeInfo, RecordFieldEncoding.Double],
        [VBCurrencyType.TypeInfo, RecordFieldEncoding.Currency],
        [VBDateType.TypeInfo, RecordFieldEncoding.Double],
        [VBStringType.TypeInfo, RecordFieldEncoding.None],
        [VBVariantType.TypeInfo, RecordFieldEncoding.None],
    ];

    // an element is held as bytes as the field of a record of its type is.
    [TestMethod]
    [DynamicData(nameof(Encodings))]
    public void AnElementOfAFixedWidthType_IsHeldAsBytes_AsARecordFieldOfTheType(VBType type, RecordFieldEncoding encoding)
        => Assert.AreEqual(encoding, new VBFixedSizeArrayValue([(1, 3)], type).ElementEncoding);

    [TestMethod]
    public void AnElementWrittenAsAPayload_IsReadBack_AsAPayloadAndAsAValue_AndMakesNoCell()
    {
        var array = NewLongs(9);

        Assert.IsTrue(array.TryWriteElementAt(4, RecordFieldEncoding.Int32, -7, 0));

        Assert.AreEqual(-7L, Payload(array, 4));
        Assert.AreEqual(0L, Payload(array, 5));
        Assert.IsEmpty(array.MadeCells);
        Assert.AreEqual(-7, Read(array, 4));
    }

    [TestMethod]
    public void APayloadOfAnotherEncoding_OrOutOfTheBounds_IsNeitherReadNorWritten()
    {
        var array = NewLongs(9);

        Assert.IsFalse(array.TryWriteElementAt(4, RecordFieldEncoding.Int16, 1, 0));
        Assert.IsFalse(array.TryWriteElementAt(10, RecordFieldEncoding.Int32, 1, 0));
        Assert.IsFalse(array.TryReadElementAt(-1, RecordFieldEncoding.Int32, out _, out _));
        Assert.IsFalse(new VBFixedSizeArrayValue([(0, 9)], VBStringType.TypeInfo).TryReadElementAt(0, RecordFieldEncoding.None, out _, out _));
        Assert.AreEqual(0L, Payload(array, 4));
    }

    [TestMethod]
    public void AnElementsCell_IsKept_AndReadsAndWritesTheElementsBytes()
    {
        var array = NewLongs(9);
        var cell = array.CellAt(2)!;
        Assert.IsInstanceOfType<BlockBytesCell>(cell);
        Assert.AreSame(cell, array.GetElementHandle(2));

        Assert.IsTrue(array.TryWriteElementAt(2, RecordFieldEncoding.Int32, 11, 0));
        Assert.AreEqual(11, cell.Value.BoxedValue);

        ((IKnowledgeBinding)cell).Store(new VBLongValue(12).RuntimeValue, isKnown: true);
        Assert.AreEqual(12L, Payload(array, 2));
        CollectionAssert.AreEqual(new[] { 2 }, array.MadeCells.Select(made => made.FlatIndex).ToArray());
    }

    // an element held as bytes is written where it is: a binding given it is written, not kept, and the cell made before reads it.
    [TestMethod]
    public void AnElementGivenABinding_IsWrittenWhatItHolds_AndKeepsItsCell()
    {
        var array = NewLongs(9);
        var cell = array.CellAt(1)!;
        var binding = new ValueBindingHandle(new VBLongValue(9).RuntimeValue);

        Assert.IsTrue(array.TrySetElementAt(1, binding));
        Assert.IsTrue(array.TryLetElementAt(3, new VBLongValue(5)));

        Assert.AreSame(cell, array.CellAt(1));
        Assert.AreEqual(9, cell.Value.BoxedValue);
        Assert.AreEqual(5L, Payload(array, 3));
    }

    [TestMethod]
    public void ACopy_SharesNoElement_AndMakesNoCell()
    {
        var array = NewLongs(999);
        Assert.IsTrue(array.TryWriteElementAt(10, RecordFieldEncoding.Int32, 3, 0));
        var cell = array.CellAt(10)!;

        var copy = array.DataCopy();
        Assert.IsEmpty(copy.MadeCells);
        Assert.IsTrue(copy.TryWriteElementAt(10, RecordFieldEncoding.Int32, 4, 0));

        Assert.AreEqual(3, cell.Value.BoxedValue);
        Assert.AreEqual(4L, Payload(copy, 10));
        Assert.AreNotSame(cell, copy.CellAt(10));
    }

    // MS-VBAL §5.4.3.8: the elements are let in place, which a cell made of one before reads.
    [TestMethod]
    public void AnArrayLetAnother_IsWrittenWhereItIs_WhichItsCellsRead()
    {
        var array = NewLongs(9);
        var cell = array.CellAt(4)!;
        Assert.AreEqual(0, cell.Value.BoxedValue);
        var source = NewLongs(9);
        Assert.IsTrue(source.TryWriteElementAt(4, RecordFieldEncoding.Int32, 8, 0));

        Assert.IsTrue(array.TryLetFrom(source.DataCopy()));

        Assert.AreEqual(8, cell.Value.BoxedValue);
        Assert.IsFalse(array.TryLetFrom(NewLongs(8)), "the premise: an array of other bounds is not let");
    }

    [TestMethod]
    public void ResettingTheElements_GivesEachTheDefault_WhichItsCellReads_AndMakesThemKnown()
    {
        var array = NewLongs(9);
        Assert.IsTrue(array.TryLetElementAt(4, new VBLongValue(6)));
        var cell = array.CellAt(4)!;
        Assert.AreEqual(6, cell.Value.BoxedValue);
        array.ForgetElements();

        array.ResetElements();

        Assert.AreEqual(0, cell.Value.BoxedValue);
        Assert.IsTrue(IsKnown(array, 4));
        Assert.IsTrue(IsKnown(array, 9));
    }

    [TestMethod]
    public void ForgettingTheElements_ForAValue_KeepsTheElementsThatHoldIt_AndACopyOrALetKnowsWhatItKnows()
    {
        var array = NewLongs(9);
        Assert.IsTrue(array.TryWriteElementAt(1, RecordFieldEncoding.Int32, 2, 0));

        array.ForgetElements(new VBLongValue(2).RuntimeValue);

        Assert.IsTrue(IsKnown(array, 1));
        Assert.IsFalse(IsKnown(array, 2));
        var copy = array.DataCopy();
        Assert.IsTrue(IsKnown(copy, 1));
        Assert.IsFalse(IsKnown(copy, 2));
        var let = NewLongs(9);
        Assert.IsTrue(let.TryLetFrom(copy));
        Assert.IsFalse(IsKnown(let, 2));
        Assert.IsTrue(let.TryWriteElementAt(2, RecordFieldEncoding.Int32, 5, 0));
        Assert.IsTrue(IsKnown(let, 2));
    }

    [TestMethod]
    public void ForgettingTheElements_ForTheDefault_KeepsThemKnown_AndForNoValue_ForgetsThem()
    {
        var array = NewLongs(9);

        array.ForgetElements(VBLongType.TypeInfo.DefaultValue.RuntimeValue);
        Assert.IsTrue(IsKnown(array, 9));

        array.ForgetElements();
        Assert.IsFalse(IsKnown(array, 9));
    }

    // MS-VBAL §5.4.3.3: a ReDim Preserve changes the upper bound of the last dimension only, so each element kept is at the flat index it was at.
    [TestMethod]
    [DataRow(4, DisplayName = "grown")]
    [DataRow(1, DisplayName = "shrunk")]
    public void APreservedArray_KeepsEachElementStillInItsBounds_AndAnElementGainedIsTheDefault(int lastUpper)
    {
        var array = new VBResizableArrayValue([(0, 1), (1, 2)], VBDoubleType.TypeInfo);
        for (var index = 0; index < array.Length; index++)
        {
            Assert.IsTrue(array.TryWriteElementAt(index, RecordFieldEncoding.Double, 0, index + 0.5));
        }

        var resized = new VBResizableArrayValue([(0, 1), (1, lastUpper)], VBDoubleType.TypeInfo);
        Assert.IsTrue(resized.TryPreserveFrom(array));

        for (var index = 0; index < resized.Length; index++)
        {
            Assert.IsTrue(resized.TryReadElementAt(index, RecordFieldEncoding.Double, out _, out var floating));
            Assert.AreEqual(index < array.Length ? index + 0.5 : 0d, floating, $"element {index}");
        }

        Assert.IsEmpty(resized.MadeCells);
    }

    [TestMethod]
    [DataRow(0, 1, 1, 3, DisplayName = "another lower bound of the last dimension")]
    [DataRow(0, 2, 1, 2, DisplayName = "another bound of the first dimension")]
    public void AnArrayOfOtherBoundsThanTheLastUpperOne_IsNotPreserved(int firstLower, int firstUpper, int lastLower, int lastUpper)
    {
        var array = new VBResizableArrayValue([(0, 1), (1, 2)], VBLongType.TypeInfo);
        Assert.IsTrue(array.TryWriteElementAt(0, RecordFieldEncoding.Int32, 1, 0));
        var other = new VBResizableArrayValue([(firstLower, firstUpper), (lastLower == 1 ? 0 : lastLower, lastUpper)], VBLongType.TypeInfo);

        Assert.IsFalse(other.TryPreserveFrom(array));
        Assert.AreEqual(0L, Payload(other, 0));
    }

    // an element held in a cell keeps its cell, which a ByRef parameter given it may be bound to.
    [TestMethod]
    public void APreservedArrayOfCells_KeepsTheCellsOfTheElementsKept()
    {
        var array = new VBResizableArrayValue([(1, 3)], VBStringType.TypeInfo);
        Assert.IsTrue(array.TryLetElementAt(1, new VBStringValue("kept")));
        var cell = array.CellAt(1)!;
        var dropped = array.CellAt(2)!;

        var resized = new VBResizableArrayValue([(1, 2)], VBStringType.TypeInfo);
        Assert.IsTrue(resized.TryPreserveFrom(array));

        Assert.AreSame(cell, resized.CellAt(1));
        Assert.AreEqual("kept", Read(resized, 1));
        Assert.IsFalse(resized.MadeCells.Any(made => ReferenceEquals(made.Cell, dropped)));
    }
}
