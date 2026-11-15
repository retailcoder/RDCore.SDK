using RDCore.SDK.Model.Values;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Platform.Protocol;

namespace RDCore.Tests.Model;

/// <summary>
/// A value of the language is written as the <see cref="RunValue"/> its remarks give for its type, and the <see cref="RunValue"/> is read back as the same
/// value, of the same type.
/// </summary>
[TestClass]
public sealed class RunValuesTests
{
    private static IEnumerable<object?[]> Values()
    {
        yield return [new VBEmptyValue(), "Empty", null];
        yield return [new VBNullValue(), "Null", null];
        yield return [new VBBooleanValue(true), "Boolean", "True"];
        yield return [new VBBooleanValue(false), "Boolean", "False"];
        yield return [new VBByteValue(255), "Byte", "255"];
        yield return [new VBIntegerValue(-32768), "Integer", "-32768"];
        yield return [new VBLongValue(42), "Long", "42"];
        yield return [new VBLongLongValue(long.MinValue), "LongLong", "-9223372036854775808"];
        yield return [new VBSingleValue(0.1f), "Single", "0.1"];
        yield return [new VBDoubleValue(0.1), "Double", "0.1"];
        yield return [new VBDoubleValue(1e300), "Double", "1E+300"];
        yield return [new VBCurrencyValue(-922337203685477.5808m), "Currency", "-922337203685477.5808"];
        yield return [new VBCurrencyValue(1234.5m), "Currency", "1234.5"];
        yield return [new VBDecimalValue(-0.5m), "Decimal", "-0.5"];
        yield return [new VBDateValue(new DateTime(2026, 11, 15, 13, 45, 0).ToOADate()), "Date", "2026-11-15T13:45:00"];
        yield return [new VBDateValue(new DateTime(100, 1, 1).ToOADate()), "Date", "0100-01-01T00:00:00"];
        yield return [new VBStringValue("Hello"), "String", "Hello"];
        yield return [new VBStringValue(string.Empty), "String", ""];
        yield return [new VBErrorValue(2042), "Error", "2042"];
        yield return [new VBNothingValue(), "Object", null];
    }

    [TestMethod]
    [DynamicData(nameof(Values))]
    public void AValue_IsWrittenAsItsTypesRowSays_AndReadBackAsTheSameValue(VBTypedValue value, string typeName, string? text)
    {
        var written = value.ToRunValue();

        Assert.AreEqual(new RunValue(typeName, text), written);
        var read = written!.ToTypedValue();
        Assert.IsNotNull(read);
        Assert.AreEqual(value.GetType(), read.GetType());
        Assert.AreEqual(written, read.ToRunValue());
    }

    [TestMethod]
    public void AVariant_IsWrittenAsTheValueItHolds()
        => Assert.AreEqual(new RunValue("Long", "7"), new VBVariantValue(new VBLongValue(7)).ToRunValue());

    [TestMethod]
    public void AValueThatIsNotKnown_IsNoRunValue()
        => Assert.IsNull(new VBLongValue(42).AsIndeterminate().ToRunValue());

    [TestMethod]
    [DataRow("2026-11-15", 2026, 11, 15, 0, 0, 0)]
    [DataRow("13:45:30", 1899, 12, 30, 13, 45, 30)]
    public void ADatePartAlone_IsMidnight_AndATimePartAlone_IsOnDayZero(string text, int year, int month, int day, int hour, int minute, int second)
        => Assert.AreEqual(new DateTime(year, month, day, hour, minute, second), ((VBDateValue)new RunValue("Date", text).ToTypedValue()!).Value);

    [TestMethod]
    [DataRow("Object", "Collection")]
    [DataRow("Long()", null)]
    [DataRow("Point", null)]
    public void AValueThatDoesNotTravel_IsReadAsNoValue(string typeName, string? text)
        => Assert.IsNull(new RunValue(typeName, text).ToTypedValue());

    [TestMethod]
    [DataRow("Boolean", "true")]
    [DataRow("Long", "4.5")]
    [DataRow("Currency", "1E3")]
    [DataRow("Date", "11/15/2026")]
    [DataRow("String", null)]
    [DataRow("Empty", "0")]
    public void TextNotOfItsTypesForm_IsRefused(string typeName, string? text)
        => Assert.ThrowsExactly<FormatException>(() => new RunValue(typeName, text).ToTypedValue());

    [TestMethod]
    public void ANumberOutOfItsTypesRange_IsRefused()
        => Assert.ThrowsExactly<OverflowException>(() => new RunValue("Integer", "32768").ToTypedValue());
}
