using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.Tests.Model.Values;

/// <summary>
/// A record as a view of its <see cref="ValueBlock"/>: fields of fixed-width intrinsic types held as bytes, a nested record inline, any other field in a
/// reference slot (<see cref="RecordShape"/>), and the record's semantics kept over them: a field's cell is one cell, a <c>with</c> copy is another view of
/// the record, and the copy an assignment makes shares nothing (<strong>MS-VBAL §5.4.3.8</strong>).
/// </summary>
[TestClass]
public sealed class ValueBlockTests
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

    // a value of each type a bytes field holds, other than the type's default, and the type.
    private static (VBType Type, VBTypedValue Value) Sample(string type) => type switch
    {
        "Byte" => (VBByteType.TypeInfo, new VBByteValue(200)),
        "Integer" => (VBIntegerType.TypeInfo, new VBIntegerValue(-1234)),
        "Boolean" => (VBBooleanType.TypeInfo, new VBBooleanValue(true)),
        "Long" => (VBLongType.TypeInfo, new VBLongValue(-123456789)),
        "LongLong" => (VBLongLongType.TypeInfo, new VBLongLongValue(1L << 40)),
        "Single" => (VBSingleType.TypeInfo, new VBSingleValue(1.5f)),
        "Double" => (VBDoubleType.TypeInfo, new VBDoubleValue(-2.25)),
        "Currency" => (VBCurrencyType.TypeInfo, new VBCurrencyValue(12.3456m)),
        "Date" => (VBDateType.TypeInfo, new VBDateValue(45000.5)),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static VBTypedValue Text(string text) => new VBStringValue(text);

    // a field of each fixed-width type is bytes of the record, and reads back what it was let, beside a String field held in a slot.
    [TestMethod]
    [DataRow("Byte")]
    [DataRow("Integer")]
    [DataRow("Boolean")]
    [DataRow("Long")]
    [DataRow("LongLong")]
    [DataRow("Single")]
    [DataRow("Double")]
    [DataRow("Currency")]
    [DataRow("Date")]
    public void AFieldOfAFixedWidthType_IsBytesOfTheRecord_AndReadsWhatItWasLet(string type)
    {
        var (fieldType, value) = Sample(type);
        var record = new VBUserDefinedTypeValue(Udt("T", ("S", VBStringType.TypeInfo), ("F", fieldType)));
        Assert.AreEqual(RecordFieldKind.Bytes, record.Shape.Fields[1].Kind);
        Assert.AreEqual(RecordFieldKind.Reference, record.Shape.Fields[0].Kind);
        Assert.AreNotEqual(value.RuntimeValue.BoxedValue, record.FieldAt(1)!.RuntimeValue.BoxedValue, "the premise: the value is not the default");

        Assert.IsTrue(record.TryLetFieldAt(1, value));

        Assert.AreEqual(value.RuntimeValue, record.FieldAt(1)!.RuntimeValue);
        Assert.AreEqual(value.GetType(), record.FieldAt(1)!.GetType());
        Assert.IsTrue(record.Block.Bytes.Any(each => each != 0), "the value is in the record's bytes");
    }

    // a bytes field's payload written in its bytes: the cell made of the field before reads the value the payload is, the field is known, and the
    // payload is read back. A Boolean is its -1, a Currency its ten-thousandths, a Date its serial.
    [TestMethod]
    [DataRow("Byte", 200L, 0d)]
    [DataRow("Integer", -1234L, 0d)]
    [DataRow("Boolean", -1L, 0d)]
    [DataRow("Long", -123456789L, 0d)]
    [DataRow("LongLong", 1L << 40, 0d)]
    [DataRow("Single", 0L, 1.5d)]
    [DataRow("Double", 0L, -2.25d)]
    [DataRow("Currency", 123456L, 0d)]
    [DataRow("Date", 0L, 45000.5d)]
    public void ABytesFieldsPayload_IsWrittenInItsBytes_AndItsCellReadsIt(string type, long integral, double floating)
    {
        var (fieldType, value) = Sample(type);
        var record = new VBUserDefinedTypeValue(Udt("T", ("S", VBStringType.TypeInfo), ("F", fieldType)));
        var encoding = record.Shape.Fields[1].Encoding;
        var cell = (IKnowledgeBinding)record.GetFieldHandle("F")!;
        Assert.AreNotEqual(value.RuntimeValue, cell.Value, "the premise: the cell was read before the write");
        cell.Forget();

        Assert.IsTrue(record.TryWriteBytesField(1, encoding, integral, floating));

        Assert.AreEqual(value.RuntimeValue, cell.Value, "the cell reads what was written");
        Assert.IsTrue(cell.IsKnown);
        Assert.IsTrue(record.TryReadBytesField(1, encoding, out var readIntegral, out var readFloating));
        Assert.AreEqual((integral, floating), (readIntegral, readFloating));
    }

    // a payload is written only to a bytes field of the encoding the caller expects; nothing is written otherwise.
    [TestMethod]
    public void ABytesFieldsPayload_IsNotWritten_ToAFieldOfAnotherEncodingOrKind()
    {
        var record = new VBUserDefinedTypeValue(Udt("T", ("S", VBStringType.TypeInfo), ("N", VBLongType.TypeInfo)));

        Assert.IsFalse(record.TryWriteBytesField(1, RecordFieldEncoding.Int16, 5, 0));
        Assert.IsFalse(record.TryWriteBytesField(0, RecordFieldEncoding.None, 5, 0));
        Assert.IsFalse(record.TryWriteBytesField(2, RecordFieldEncoding.Int32, 5, 0));
        Assert.IsFalse(record.TryReadBytesField(0, RecordFieldEncoding.Int32, out _, out _));

        Assert.AreEqual(new VBLongValue(0).RuntimeValue, record.FieldAt(1)!.RuntimeValue);
        Assert.IsTrue(record.Block.Bytes.All(each => each == 0));
    }

    private static VBUserDefinedType Nesting()
    {
        var inner = Udt("TInner", ("X", VBLongType.TypeInfo), ("S", VBStringType.TypeInfo), ("D", VBDoubleType.TypeInfo));
        var middle = Udt("TMiddle", ("N", VBIntegerType.TypeInfo), ("In", inner));
        return Udt("TOuter", ("A", VBLongType.TypeInfo), ("Mid", middle), ("B", VBDoubleType.TypeInfo));
    }

    // the place of a field of a record two levels in is that field, read and written from the outermost record or from a view of a record it nests, each
    // view reading from its own first place: the field's cell reads what was written, and no other field is written.
    [TestMethod]
    public void ThePlaceOfAFieldOfANestedRecord_IsThatField_FromTheRecordOrAViewOfARecordItNests()
    {
        var outer = new VBUserDefinedTypeValue(Nesting());
        var middle = (VBUserDefinedTypeValue)outer.StoredFieldAt(1)!;
        var inner = (VBUserDefinedTypeValue)middle.StoredFieldAt(1)!;
        var cell = (IKnowledgeBinding)inner.GetFieldHandle("D")!;
        Assert.AreEqual(new VBDoubleValue(0).RuntimeValue, cell.Value, "the premise: the cell was read before the write");
        var place = outer.Shape.PlaceOf([1, 1, 2]);
        Assert.IsTrue(place >= outer.Shape.Fields.Length, "the premise: a nested field's place is past the record's own fields");

        Assert.IsTrue(outer.TryWriteBytesField(place, RecordFieldEncoding.Double, 0, 7.5));
        Assert.IsTrue(middle.TryWriteBytesField(middle.Shape.PlaceOf([1, 0]), RecordFieldEncoding.Int32, 42, 0));

        Assert.AreEqual(new VBDoubleValue(7.5).RuntimeValue, cell.Value, "the cell reads what was written");
        Assert.AreEqual(new VBLongValue(42).RuntimeValue, inner.FieldAt(0)!.RuntimeValue);
        Assert.IsTrue(outer.TryReadBytesField(outer.Shape.PlaceOf([1, 1, 0]), RecordFieldEncoding.Int32, out var integral, out _));
        Assert.AreEqual(42L, integral);
        Assert.IsTrue(middle.TryReadBytesField(middle.Shape.PlaceOf([1, 2]), RecordFieldEncoding.Double, out _, out var floating));
        Assert.AreEqual(7.5, floating);
        Assert.AreEqual(new VBLongValue(0).RuntimeValue, outer.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual(new VBIntegerValue(0).RuntimeValue, middle.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual(new VBDoubleValue(0).RuntimeValue, outer.FieldAt(2)!.RuntimeValue);
    }

    // a field of the record itself is at the place of its position; a path that names no field has no place, and a place that is not a bytes field of the
    // encoding expected is neither read nor written.
    [TestMethod]
    public void APathThatNamesNoField_HasNoPlace_AndAPlaceThatIsNotABytesField_IsNotReadOrWritten()
    {
        var outer = new VBUserDefinedTypeValue(Nesting());
        var shape = outer.Shape;

        Assert.AreEqual(2, shape.PlaceOf([2]));
        Assert.AreEqual(-1, shape.PlaceOf([]));
        Assert.AreEqual(-1, shape.PlaceOf([3]));
        Assert.AreEqual(-1, shape.PlaceOf([-1]));
        Assert.AreEqual(-1, shape.PlaceOf([0, 0]), "a Long field holds no field");
        Assert.AreEqual(-1, shape.PlaceOf([1, 1, 3]));

        Assert.IsFalse(outer.TryReadBytesField(shape.PlaceOf([1, 1, 1]), RecordFieldEncoding.Int32, out _, out _), "a String field");
        Assert.IsFalse(outer.TryWriteBytesField(shape.PlaceOf([1]), RecordFieldEncoding.Int16, 5, 0), "a nested record");
        Assert.IsFalse(outer.TryWriteBytesField(shape.PlaceOf([1, 0]), RecordFieldEncoding.Int32, 5, 0), "an Integer field");
        Assert.IsFalse(outer.TryWriteBytesField(shape.PlaceCount, RecordFieldEncoding.Int32, 5, 0));
        Assert.IsTrue(outer.Block.Bytes.All(each => each == 0));
    }

    // the bytes fields of a record each have bytes of their own: a value let to each is read back from each, none written over by another.
    [TestMethod]
    public void EachBytesField_HasBytesOfItsOwn()
    {
        string[] types = ["Byte", "Integer", "Boolean", "Long", "LongLong", "Single", "Double", "Currency", "Date"];
        var samples = types.Select(Sample).ToArray();
        var record = new VBUserDefinedTypeValue(Udt("T", [.. samples.Select((sample, at) => ($"F{at}", sample.Type))]));

        for (var at = 0; at < samples.Length; at++)
        {
            Assert.IsTrue(record.TryLetFieldAt(at, samples[at].Value));
        }

        for (var at = 0; at < samples.Length; at++)
        {
            Assert.AreEqual(samples[at].Value.RuntimeValue, record.FieldAt(at)!.RuntimeValue, types[at]);
        }
    }

    // a field's cell is one cell however often it is asked for: a ByRef parameter given the field reads what the field is let, and the field reads what is
    // stored in the cell (MS-VBAL §5.3.1.11).
    [TestMethod]
    public void ABytesFieldsCell_IsOneCell_ThatTheFieldIsReadAndWrittenThrough()
    {
        var record = new VBUserDefinedTypeValue(Udt("T", ("N", VBLongType.TypeInfo)));
        var cell = (IKnowledgeBinding)record.GetFieldHandle("N")!;
        Assert.AreSame(cell, record.StoredFieldAt(0)!.Handle);

        Assert.IsTrue(record.TryLetFieldAt(0, new VBLongValue(7)));
        Assert.AreEqual(new VBLongValue(7).RuntimeValue, cell.Value);

        cell.Store(new VBLongValue(9).RuntimeValue, isKnown: true);
        Assert.AreEqual(new VBLongValue(9).RuntimeValue, record.FieldAt(0)!.RuntimeValue);
    }

    // a nested record is inline: its bytes and its slots are the outer record's, and its view is one view, whose fields are written in the outer block.
    [TestMethod]
    public void ANestedRecord_IsInline_AndOneView()
    {
        var inner = Udt("TInner", ("X", VBLongType.TypeInfo), ("S", VBStringType.TypeInfo));
        var outer = new VBUserDefinedTypeValue(Udt("TOuter", ("A", VBLongType.TypeInfo), ("In", inner)));
        Assert.AreEqual(RecordFieldKind.Nested, outer.Shape.Fields[1].Kind);
        Assert.AreEqual(1, outer.Shape.SlotCount, "the nested record's String is a slot of the outer record");

        var view = (VBUserDefinedTypeValue)outer.StoredFieldAt(1)!;
        Assert.AreSame(view, outer.StoredFieldAt(1));
        Assert.AreSame(outer.Block, view.Block);

        Assert.IsTrue(view.TryLetFieldAt(0, new VBLongValue(5)));
        Assert.IsTrue(view.TryLetFieldAt(1, Text("in")));
        Assert.IsTrue(outer.TryLetFieldAt(0, new VBLongValue(6)));

        Assert.AreEqual(new VBLongValue(5).RuntimeValue, view.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual("in", view.FieldAt(1)!.RuntimeValue.BoxedValue);
        Assert.AreEqual(new VBLongValue(6).RuntimeValue, outer.FieldAt(0)!.RuntimeValue);
    }

    // a record's reference fields are its own and its nested records', each the value its field stores; a nested record's are its own alone.
    [TestMethod]
    public void TheReferenceFields_AreTheStoredValuesOfTheFieldsNotHeldAsBytes_NestedRecordsIncluded()
    {
        var inner = Udt("TInner", ("X", VBLongType.TypeInfo), ("S", VBStringType.TypeInfo));
        var outer = new VBUserDefinedTypeValue(Udt("TOuter", ("A", VBLongType.TypeInfo), ("T", VBStringType.TypeInfo), ("In", inner)));
        var view = (VBUserDefinedTypeValue)outer.StoredFieldAt(2)!;

        var fields = outer.ReferenceFields.ToArray();

        Assert.HasCount(2, fields);
        Assert.AreSame(outer.StoredFieldAt(1), fields[0]);
        Assert.AreSame(view.StoredFieldAt(1), fields[1]);
        var nested = view.ReferenceFields.ToArray();
        Assert.HasCount(1, nested);
        Assert.AreSame(view.StoredFieldAt(1), nested[0]);
    }

    // a `with` copy is another view of the same record: a field let through one is read through the other.
    [TestMethod]
    public void AWithCopy_IsAnotherViewOfTheRecord()
    {
        var record = new VBUserDefinedTypeValue(Udt("T", ("N", VBLongType.TypeInfo), ("S", VBStringType.TypeInfo)));
        var view = record with { };

        Assert.IsTrue(view.TryLetFieldAt(0, new VBLongValue(3)));
        Assert.IsTrue(view.TryLetFieldAt(1, Text("x")));

        Assert.AreEqual(new VBLongValue(3).RuntimeValue, record.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual("x", record.FieldAt(1)!.RuntimeValue.BoxedValue);
    }

    // the copy an assignment makes shares no cell with the record, nested record included: what is let to the copy is not the record's, and what the
    // record held is the copy's.
    [TestMethod]
    public void ADataCopy_SharesNothing_AndHoldsWhatTheRecordHeld()
    {
        var inner = Udt("TInner", ("X", VBLongType.TypeInfo), ("S", VBStringType.TypeInfo));
        var record = new VBUserDefinedTypeValue(Udt("TOuter", ("N", VBLongType.TypeInfo), ("In", inner)));
        var nested = (VBUserDefinedTypeValue)record.StoredFieldAt(1)!;
        _ = record.TryLetFieldAt(0, new VBLongValue(1));
        _ = nested.TryLetFieldAt(0, new VBLongValue(2));
        _ = nested.TryLetFieldAt(1, Text("a"));

        var copy = record.DataCopy();
        var copied = (VBUserDefinedTypeValue)copy.StoredFieldAt(1)!;
        Assert.AreNotSame(record.Block, copy.Block);
        Assert.AreNotSame(nested.StoredFieldAt(1)!.Handle, copied.StoredFieldAt(1)!.Handle, "the String field's cell is the copy's own");
        Assert.AreEqual(new VBLongValue(1).RuntimeValue, copy.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual(new VBLongValue(2).RuntimeValue, copied.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual("a", copied.FieldAt(1)!.RuntimeValue.BoxedValue);

        _ = copy.TryLetFieldAt(0, new VBLongValue(10));
        _ = copied.TryLetFieldAt(0, new VBLongValue(20));
        _ = copied.TryLetFieldAt(1, Text("b"));

        Assert.AreEqual(new VBLongValue(1).RuntimeValue, record.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual(new VBLongValue(2).RuntimeValue, nested.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual("a", nested.FieldAt(1)!.RuntimeValue.BoxedValue);
    }

    // a record let a record that is not a copy (the variable assigned, not the copy an assignment makes of it) takes a copy of it: a String is written in the
    // record's own cell, and a Variant is a copy of the source's, so a write to either afterwards is not the other's.
    [TestMethod]
    public void ARecordLetARecordThatIsNoCopy_SharesNoCellWithIt()
    {
        var type = Udt("T", ("N", VBLongType.TypeInfo), ("S", VBStringType.TypeInfo), ("V", VBVariantType.TypeInfo));
        var record = new VBUserDefinedTypeValue(type);
        var source = new VBUserDefinedTypeValue(type);
        _ = source.TryLetFieldAt(0, new VBLongValue(4));
        _ = source.TryLetFieldAt(1, Text("s"));
        _ = source.TryLetFieldAt(2, new VBVariantValue(new VBLongValue(7)));
        var cell = record.GetFieldHandle("S")!;

        Assert.IsTrue(record.TryLetFrom(source));

        Assert.AreSame(cell, record.GetFieldHandle("S"), "the String is written in the record's own cell");
        Assert.AreEqual("s", cell.Value.BoxedValue);
        Assert.AreEqual(new VBLongValue(4).RuntimeValue, record.FieldAt(0)!.RuntimeValue);
        Assert.AreNotSame(source.StoredFieldAt(2), record.StoredFieldAt(2), "the Variant is a copy of the source's");
        Assert.AreEqual(new VBLongValue(7).RuntimeValue, ((VBVariantValue)record.StoredFieldAt(2)!).TypedValue.RuntimeValue);

        ((IKnowledgeBinding)cell).Store(Text("t").RuntimeValue, isKnown: true);
        Assert.AreEqual("s", source.FieldAt(1)!.RuntimeValue.BoxedValue);
    }

    // a record let itself holds what it held.
    [TestMethod]
    public void ARecordLetItself_HoldsWhatItHeld()
    {
        var type = Udt("T", ("N", VBLongType.TypeInfo), ("S", VBStringType.TypeInfo), ("V", VBVariantType.TypeInfo));
        var record = new VBUserDefinedTypeValue(type);
        _ = record.TryLetFieldAt(0, new VBLongValue(4));
        _ = record.TryLetFieldAt(1, Text("s"));
        _ = record.TryLetFieldAt(2, new VBVariantValue(Text("v")));

        Assert.IsTrue(record.TryLetFrom(record));

        Assert.AreEqual(new VBLongValue(4).RuntimeValue, record.FieldAt(0)!.RuntimeValue);
        Assert.AreEqual("s", record.FieldAt(1)!.RuntimeValue.BoxedValue);
        Assert.AreEqual("v", ((VBVariantValue)record.StoredFieldAt(2)!).TypedValue.RuntimeValue.BoxedValue);
    }

    // a record let another in place: a cell taken from it before (a ByRef parameter given a field) reads what was let, and the record reads what the
    // source held, whether or not each field was known.
    [TestMethod]
    public void ARecordLetAnother_IsWrittenInPlace_AndItsCellsReadWhatWasLet()
    {
        var type = Udt("T", ("N", VBLongType.TypeInfo), ("M", VBLongType.TypeInfo), ("S", VBStringType.TypeInfo));
        var record = new VBUserDefinedTypeValue(type);
        var cell = record.GetFieldHandle("N")!;
        Assert.AreEqual(new VBLongValue(0).RuntimeValue, cell.Value, "the premise: the cell was read before the Let");
        var source = new VBUserDefinedTypeValue(type);
        _ = source.TryLetFieldAt(0, new VBLongValue(4));
        _ = source.TryLetFieldAt(2, Text("s"));
        ((IKnowledgeBinding)source.GetFieldHandle("M")!).Forget();

        Assert.IsTrue(record.TryLetFrom(source.DataCopy()));

        Assert.AreEqual(new VBLongValue(4).RuntimeValue, cell.Value);
        Assert.AreEqual("s", record.FieldAt(2)!.RuntimeValue.BoxedValue);
        Assert.IsFalse(record.FieldAt(0)!.IsIndeterminate);
        Assert.IsTrue(record.FieldAt(1)!.IsIndeterminate, "a field not known in the source is not known in the record");
    }

    // a record of another type is not let, and nothing of it is written.
    [TestMethod]
    public void ARecordOfAnotherType_IsNotLet()
    {
        var record = new VBUserDefinedTypeValue(Udt("T", ("N", VBLongType.TypeInfo)));
        var other = new VBUserDefinedTypeValue(Udt("U", ("N", VBLongType.TypeInfo)));
        _ = other.TryLetFieldAt(0, new VBLongValue(8));

        Assert.IsFalse(record.TryLetFrom(other));
        Assert.AreEqual(new VBLongValue(0).RuntimeValue, record.FieldAt(0)!.RuntimeValue);
    }
}
