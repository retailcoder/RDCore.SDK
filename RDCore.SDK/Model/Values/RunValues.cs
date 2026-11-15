using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Platform.Protocol;
using System.Globalization;

namespace RDCore.SDK.Model.Values;

/// <summary>
/// Converts between a value of the language and a <see cref="RunValue"/>: its type's name (<strong>MS-VBAL §2.1</strong>), and its value as the invariant
/// text <see cref="RunValue"/>'s remarks give for the type.
/// </summary>
/// <remarks>
/// The two conversions are each other's inverse for every type that travels: a value written and read back is the same value, of the same type. A
/// <c>LongPtr</c> is the exception: it is written as the type it is on the platform, and read back as that type.
/// </remarks>
public static class RunValues
{
    private const string DateFormat = "yyyy-MM-ddTHH:mm:ss";
    private const string DateOnlyFormat = "yyyy-MM-dd";
    private const string TimeOnlyFormat = "HH:mm:ss";

    // the day a time alone is on: day 0 of the Date serial (MS-VBAL §2.1).
    private static readonly DateTime DayZero = new(1899, 12, 30);

    /// <summary>
    /// The value as a <see cref="RunValue"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>Variant</c> is written as the value it holds. A <c>LongPtr</c> is written as the type it is on the platform: <c>LongLong</c> on a 64-bit one,
    /// <c>Long</c> otherwise. A fixed-length string is a <c>String</c>. A <c>Date</c> is written whole, as <c>yyyy-MM-ddTHH:mm:ss</c>. An <c>Error</c> is its
    /// number, as <c>CVErr</c> takes it (<strong>MS-VBAL §6.1.2.3.1.14</strong>).
    /// </para>
    /// <para>
    /// An array, a user-defined type and an object are written with their type's name and a <see langword="null"/> value; <c>Nothing</c> is an
    /// <c>Object</c> with a <see langword="null"/> value.
    /// </para>
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The value as a <see cref="RunValue"/>; <see langword="null"/> for an indeterminate value, and for a value of no type the remarks of
    /// <see cref="RunValue"/> list, such as <c>Missing</c>.
    /// </returns>
    public static RunValue? ToRunValue(this VBTypedValue value)
    {
        if (value.IsIndeterminate)
        {
            return null;
        }

        return value switch
        {
            VBVariantValue variant => variant.TypedValue is VBVariantValue ? null : variant.TypedValue.ToRunValue(),
            VBEmptyValue => new("Empty", null),
            VBNullValue => new("Null", null),
            VBBooleanValue boolean => new("Boolean", boolean.Value.StoredValue != 0 ? "True" : "False"),
            VBByteValue number => new("Byte", number.Value.ToString(CultureInfo.InvariantCulture)),
            VBIntegerValue number => new("Integer", number.Value.ToString(CultureInfo.InvariantCulture)),
            VBLongValue number => new("Long", number.Value.ToString(CultureInfo.InvariantCulture)),
            VBLongLongValue number => new("LongLong", number.Value.ToString(CultureInfo.InvariantCulture)),
            VBLongPtrValue pointer => new(pointer.Is64Bit ? "LongLong" : "Long", pointer.Value.ToString(CultureInfo.InvariantCulture)),
            VBSingleValue number => new("Single", number.Value.ToString("R", CultureInfo.InvariantCulture)),
            VBDoubleValue number => new("Double", number.Value.ToString("R", CultureInfo.InvariantCulture)),
            VBCurrencyValue number => new("Currency", number.Value.Value.ToString(CultureInfo.InvariantCulture)),
            VBDecimalValue number => new("Decimal", number.Value.ToString(CultureInfo.InvariantCulture)),
            VBDateValue date => new("Date", date.Value.ToString(DateFormat, CultureInfo.InvariantCulture)),
            VBStringValue text => new("String", text.Value),
            VBErrorValue error => new("Error", error.Value.ToString(CultureInfo.InvariantCulture)),
            VBNothingValue => new("Object", null),
            VBArrayValue or VBUserDefinedTypeValue or VBObjectValue => new(value.TypeInfo.Name, null),
            _ => null,
        };
    }

    /// <summary>
    /// The value a <see cref="RunValue"/> says.
    /// </summary>
    /// <remarks>
    /// A <c>Date</c> is read whole (<c>yyyy-MM-ddTHH:mm:ss</c>), as a date alone (<c>yyyy-MM-dd</c>, at midnight), or as a time alone (<c>HH:mm:ss</c>, on
    /// 1899-12-30, day 0 of the <c>Date</c> serial). An <c>Error</c> is the error of that number, as <c>CVErr</c> makes it
    /// (<strong>MS-VBAL §6.1.2.3.1.14</strong>). An <c>Object</c> with a <see langword="null"/> value is <c>Nothing</c>.
    /// </remarks>
    /// <param name="value">The value as it travels.</param>
    /// <returns>
    /// The value; <see langword="null"/> for a value that does not travel: an object other than <c>Nothing</c>, an array, a user-defined type, or a type name
    /// the remarks of <see cref="RunValue"/> do not list.
    /// </returns>
    /// <exception cref="FormatException">The text is not of the form the type's row of the table gives, or is <see langword="null"/> for a type that has text.</exception>
    /// <exception cref="OverflowException">The text is a number out of the type's range.</exception>
    public static VBTypedValue? ToTypedValue(this RunValue value)
    {
        switch (value.TypeName)
        {
            case "Empty": return NoText(value, new VBEmptyValue());
            case "Null": return NoText(value, new VBNullValue());
            case "Object": return value.Value is null ? new VBNothingValue() : null;
        }

        var text = value.Value;
        if (text is null)
        {
            return value.TypeName is "Boolean" or "Byte" or "Integer" or "Long" or "LongLong" or "Single" or "Double" or "Currency" or "Decimal" or "Date" or "String" or "Error"
                ? throw new FormatException($"A {value.TypeName} has text.")
                : null;
        }

        const NumberStyles integer = NumberStyles.AllowLeadingSign;
        const NumberStyles fixedPoint = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        const NumberStyles floatingPoint = NumberStyles.Float;
        var invariant = CultureInfo.InvariantCulture;
        return value.TypeName switch
        {
            "Boolean" => text switch
            {
                "True" => new VBBooleanValue(true),
                "False" => new VBBooleanValue(false),
                _ => throw new FormatException($"A Boolean is True or False: '{text}'."),
            },
            "Byte" => new VBByteValue(byte.Parse(text, integer, invariant)),
            "Integer" => new VBIntegerValue(short.Parse(text, integer, invariant)),
            "Long" => new VBLongValue(int.Parse(text, integer, invariant)),
            "LongLong" => new VBLongLongValue(long.Parse(text, integer, invariant)),
            "Single" => new VBSingleValue(float.Parse(text, floatingPoint, invariant)),
            "Double" => new VBDoubleValue(double.Parse(text, floatingPoint, invariant)),
            "Currency" => new VBCurrencyValue(decimal.Parse(text, fixedPoint, invariant)),
            "Decimal" => new VBDecimalValue(decimal.Parse(text, fixedPoint, invariant)),
            "Date" => new VBDateValue(DateOf(text).ToOADate()),
            "String" => new VBStringValue(text),
            "Error" => new VBErrorValue(int.Parse(text, integer, invariant)),
            _ => null,
        };
    }

    private static VBTypedValue NoText(RunValue value, VBTypedValue typed)
        => value.Value is null ? typed : throw new FormatException($"{value.TypeName} has no text: '{value.Value}'.");

    private static DateTime DateOf(string text)
    {
        var invariant = CultureInfo.InvariantCulture;
        if (DateTime.TryParseExact(text, [DateFormat, DateOnlyFormat], invariant, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return DateTime.TryParseExact(text, TimeOnlyFormat, invariant, DateTimeStyles.NoCurrentDateDefault, out var time)
            ? DayZero + time.TimeOfDay
            : throw new FormatException($"A Date is {DateFormat}, {DateOnlyFormat} or {TimeOnlyFormat}: '{text}'.");
    }
}
