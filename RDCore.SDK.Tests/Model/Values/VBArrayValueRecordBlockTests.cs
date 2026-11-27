using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Model.Values.Runtime;
using System.Buffers.Binary;

namespace RDCore.Tests.Model.Values;

/// <summary>
/// The elements of an array of records are records of one block, laid end to end: each element a view of the block at its index times the record's shape,
/// in a cell of its own that is kept, starting as the element's default starts, and copied into the block of the array's copy, which shares no cell with it.
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.5 Runtime Values")]
public sealed class VBArrayValueRecordBlockTests
{
    private static VBUserDefinedType Udt(string name, params (string Name, VBType Type)[] fields)
    {
        var uri = TestUri.TestModuleUserDefinedTypeUri(name);
        var symbol = new VBUserDefinedTypeMemberSymbol(uri, uri, name, ScopeKind.Module, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public);
        return new VBUserDefinedType(symbol, [.. fields.Select(field => Field(name, field.Name, field.Type))]);
    }

    private static VBUserDefinedTypeFieldSymbol Field(string typeName, string name, VBType type)
    {
        var uri = TestUri.TestUserDefinedTypeMemberUri(name, typeName);
        return new VBUserDefinedTypeFieldSymbol(uri, uri, name, type, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public);
    }

    // Type TItem: S As String, P As TPair (X As Long, Y As Long), N As Long: a reference slot, a nested record and a bytes field.
    private static VBUserDefinedType Item()
        => Udt("TItem", ("S", VBStringType.TypeInfo), ("P", Udt("TPair", ("X", VBLongType.TypeInfo), ("Y", VBLongType.TypeInfo))), ("N", VBLongType.TypeInfo));

    private const int S = 0;
    private const int P = 1;
    private const int N = 2;

    private static VBUserDefinedTypeValue RecordAt(VBArrayValue array, int index)
        => ((VBRuntimeValue<VBRuntimeUserDefinedTypeValue>)array.CellAt(index)!.Value).StoredValue.UserDefinedType;

    private static VBUserDefinedTypeValue Nested(VBUserDefinedTypeValue record) => (VBUserDefinedTypeValue)record.StoredFieldAt(P)!;

    private static object? Read(VBUserDefinedTypeValue record, int index) => record.FieldAt(index)!.RuntimeValue.BoxedValue;

    private static int LongAt(ValueBlock block, int offset) => BinaryPrimitives.ReadInt32LittleEndian(block.Bytes.AsSpan(offset));

    [TestMethod]
    public void TheElementsOfAnArrayOfRecords_AreViewsOfOneBlock_EachAtItsIndexTimesTheShape()
    {
        var item = Item();
        var shape = RecordShape.Of(item);
        Assert.IsTrue(shape.ByteSize > 0 && shape.SlotCount > 0, "the premise: a record of both bytes and reference slots");
        var array = new VBFixedSizeArrayValue([(0, 2)], item);

        var third = RecordAt(array, 2);
        Assert.IsTrue(third.TryLetFieldAt(N, new VBLongValue(7)));

        Assert.AreSame(RecordAt(array, 0).Block, third.Block);
        Assert.AreEqual((2 * shape.ByteSize, 2 * shape.SlotCount, 2 * shape.PlaceCount), (third.ByteOffset, third.SlotOffset, third.PlaceOffset));
        Assert.AreEqual(7, LongAt(third.Block, 2 * shape.ByteSize + shape.Fields[N].Offset));
        Assert.AreEqual(0, Read(RecordAt(array, 1), N));
        Assert.AreEqual(7, Read(RecordAt(array, 2), N));
    }

    [TestMethod]
    public void ANestedRecordOfAnElement_IsAViewOfTheArraysBlock_AtTheElementsOffsets()
    {
        var item = Item();
        var shape = RecordShape.Of(item);
        var array = new VBFixedSizeArrayValue([(0, 2)], item);

        var nested = Nested(RecordAt(array, 1));
        Assert.IsTrue(nested.TryLetFieldAt(1, new VBLongValue(9)));

        Assert.AreSame(RecordAt(array, 0).Block, nested.Block);
        Assert.AreEqual(9, LongAt(nested.Block, shape.ByteSize + shape.Fields[P].Offset + nested.Shape.Fields[1].Offset));
        Assert.AreEqual(9, Read(Nested(RecordAt(array, 1)), 1));
        Assert.AreEqual(0, Read(Nested(RecordAt(array, 2)), 1));
    }

    // a ByRef parameter given an element, or a field of one, is bound to its cell, which every later read of the element is the cell of.
    [TestMethod]
    public void AnElementsCell_AndTheCellOfAFieldOfIt_AreKept_AndReadWhatIsWrittenInThem()
    {
        var array = new VBFixedSizeArrayValue([(0, 2)], Item());
        var cell = array.CellAt(1)!;
        var field = (IKnowledgeBinding)RecordAt(array, 1).GetFieldHandle("N")!;

        field.Store(new VBLongValue(5).RuntimeValue, isKnown: true);

        Assert.AreSame(cell, array.GetElementHandle(1));
        Assert.AreSame(field, RecordAt(array, 1).GetFieldHandle("N"));
        Assert.AreEqual(5, Read(RecordAt(array, 1), N));
    }

    [TestMethod]
    public void EachElement_StartsAsTheElementsDefaultStarts_WithReferenceFieldsOfItsOwn()
    {
        var item = Item();
        var array = new VBFixedSizeArrayValue([(0, 2)], item, elementDefault: () =>
        {
            var start = new VBUserDefinedTypeValue(item);
            Assert.IsTrue(start.TryLetFieldAt(S, new VBStringValue("start")));
            return start;
        });

        Assert.IsTrue(RecordAt(array, 0).TryLetFieldAt(S, new VBStringValue("changed")));

        Assert.AreEqual("changed", Read(RecordAt(array, 0), S));
        Assert.AreEqual("start", Read(RecordAt(array, 1), S));
        Assert.AreEqual("start", Read(RecordAt(array, 2), S));
    }

    [TestMethod]
    public void ACopyOfAnArrayOfRecords_HoldsItsRecordsInABlockOfItsOwn_AndSharesNoCellWithIt()
    {
        var array = new VBFixedSizeArrayValue([(0, 2)], Item());
        var source = RecordAt(array, 1);
        Assert.IsTrue(source.TryLetFieldAt(S, new VBStringValue("one")));
        Assert.IsTrue(source.TryLetFieldAt(N, new VBLongValue(3)));
        Assert.IsTrue(Nested(source).TryLetFieldAt(0, new VBLongValue(4)));
        ((IKnowledgeBinding)array.CellAt(2)!).Forget();

        var copy = array.DataCopy();
        var copied = RecordAt(copy, 1);

        Assert.AreNotSame(source.Block, copied.Block);
        Assert.AreSame(copied.Block, RecordAt(copy, 2).Block);
        Assert.AreEqual(("one", 3, 4), (Read(copied, S), Read(copied, N), Read(Nested(copied), 0)));
        Assert.IsFalse(((IKnowledgeBinding)copy.CellAt(2)!).IsKnown, "what is known of an element is copied with it");

        Assert.IsTrue(copied.TryLetFieldAt(S, new VBStringValue("copy")));
        Assert.IsTrue(copied.TryLetFieldAt(N, new VBLongValue(30)));
        Assert.AreEqual(("one", 3), (Read(source, S), Read(source, N)));
    }

    // an element held in a cell keeps its cell through a ReDim Preserve (MS-VBAL §5.4.3.3), which a ByRef parameter given it may be bound to.
    [TestMethod]
    public void APreservedArrayOfRecords_KeepsTheCellsOfTheRecordsKept()
    {
        var item = Item();
        var array = new VBResizableArrayValue([(1, 3)], item);
        Assert.IsTrue(RecordAt(array, 1).TryLetFieldAt(N, new VBLongValue(8)));
        var cell = array.CellAt(1)!;

        var resized = new VBResizableArrayValue([(1, 5)], item);
        Assert.IsTrue(resized.TryPreserveFrom(array));

        Assert.AreSame(cell, resized.CellAt(1));
        Assert.AreEqual(8, Read(RecordAt(resized, 1), N));
        Assert.AreEqual(0, Read(RecordAt(resized, 4), N));
    }
}
