#pragma warning disable IDE0130 // Namespace does not match folder structure
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Model.Values.Intrinsic;
using System.Numerics;

namespace RDCore.SDK.Model.Types;

/// <summary>
/// A <see cref="VBNumericType{Decimal}"/> representing the <c>Currency</c> data type.
/// </summary>
/// <remarks>
/// The <em>managed type</em> of a value of this data type is <c>decimal</c>.<br/>
/// 👉 Implements <see cref="IFixedPointNumericType"/>.
/// </remarks>
public sealed record class VBCurrencyType() : VBNumericType<VBRuntimeCurrencyValue>(VBTypeNames.VBCurrency), IFixedPointNumericType
{
    private static readonly Lazy<VBCurrencyType> _instance = new(() => new(), LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// The <c>Currency</c> data type.
    /// </summary>
    public static VBCurrencyType TypeInfo => _instance.Value;

    private static readonly Lazy<VBCurrencyValue> _defaultValue = new(() => VBCurrencyType.Zero, LazyThreadSafetyMode.PublicationOnly);
    public override VBTypedValue DefaultValue => _defaultValue.Value;

    private static readonly Lazy<VBCurrencyValue> _minValue = new(() => new VBCurrencyValue(long.MinValue / 10000m), LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// Gets the minimum representable value for this data type.
    /// </summary>
    public static VBCurrencyValue MinValue => _minValue.Value;
    public override double ManagedMinValue => ToDouble(VBRuntimeValue<VBRuntimeCurrencyValue>.CurrencyMinValue.Value);

    private static readonly Lazy<VBCurrencyValue> _maxValue = new(() => new VBCurrencyValue(long.MaxValue / 10000m), LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// Gets the maximum representable value for this data type.
    /// </summary>
    public static VBCurrencyValue MaxValue => _maxValue.Value;
    public override double ManagedMaxValue => ToDouble(VBRuntimeValue<VBRuntimeCurrencyValue>.CurrencyMaxValue.Value);

    private static readonly Lazy<VBCurrencyValue> _zero = new(() => new VBCurrencyValue(0m), LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// Gets the value <c>0</c> (zero) representation of this data type.
    /// </summary>
    public static VBCurrencyValue Zero => _zero.Value;

    /// <summary>
    /// Converts a <c>Currency</c> to a <c>Double</c> as MS-VBA does: the value rounded to the nearest <c>Double</c>, a tie to the even one
    /// (<strong>MS-VBAL §5.5.1.2.1</strong>).
    /// </summary>
    /// <param name="value">The <c>Currency</c> to convert.</param>
    /// <returns>
    /// The <c>Double</c> nearest the value: <c>CDbl(CCur("75299393168858.7426"))</c> is 75299393168858.75, where the number of ten-thousandths made a
    /// <c>Double</c>, then divided by 10000, is 75299393168858.734375.
    /// </returns>
    /// <remarks>
    /// The value is rounded once (observed in Excel). The result does not depend on the .NET version.
    /// </remarks>
    public static double ToDouble(VBRuntimeCurrencyValue value) => Nearest(value.StoredValue, precision: 53);

    /// <summary>
    /// Converts a <c>Currency</c> to a <c>Single</c> as MS-VBA does: the value rounded to the nearest <c>Single</c>, a tie to the even one
    /// (<strong>MS-VBAL §5.5.1.2.1</strong>).
    /// </summary>
    /// <param name="value">The <c>Currency</c> to convert.</param>
    /// <returns>
    /// The <c>Single</c> nearest the value: <c>CSng(CCur("281474993487872.0001"))</c> is 281475010265088, where the <c>Double</c> nearest the value,
    /// 281474993487872, is halfway between two <c>Single</c> values and rounds to the even one, 281474976710656.
    /// </returns>
    /// <remarks>
    /// The value is rounded once, not through a <c>Double</c> (observed in Excel). The result does not depend on the .NET version.
    /// </remarks>
    public static float ToSingle(VBRuntimeCurrencyValue value) => (float)Nearest(value.StoredValue, precision: 24);

    // ten-thousandths / 10000, rounded to the nearest binary floating-point value of the given precision (53 bits for a Double, 24 for a Single), a tie to
    // the even one. The quotient is computed exactly in 128-bit integers, so that the value is rounded once; the result is exact as a Double.
    private static double Nearest(long tenThousandths, int precision)
    {
        if (tenThousandths == 0)
        {
            return 0d;
        }

        var magnitude = (UInt128)Int128.Abs(tenThousandths);
        // magnitude * 2^shift / 10000 is at least 2^(precision - 1), and below 2^(precision + 1).
        var shift = precision + 14 - (128 - (int)UInt128.LeadingZeroCount(magnitude));
        var (quotient, remainder, divisor) = Divide(magnitude, shift);
        if (quotient >> precision != 0)
        {
            shift--;
            (quotient, remainder, divisor) = Divide(magnitude, shift);
        }

        var twice = remainder * 2;
        if (twice > divisor || (twice == divisor && (quotient & 1) == 1))
        {
            quotient++;
        }

        var result = Math.ScaleB((double)quotient, -shift);
        return tenThousandths < 0 ? -result : result;
    }

    private static (UInt128 Quotient, UInt128 Remainder, UInt128 Divisor) Divide(UInt128 magnitude, int shift)
    {
        var numerator = shift > 0 ? magnitude << shift : magnitude;
        var divisor = shift < 0 ? (UInt128)10000 << -shift : 10000;
        return (numerator / divisor, numerator % divisor, divisor);
    }



    public override VBNumericTypedValue CreateValue(double value)
        => TenThousandthsOf(value) is { } stored
            ? new VBCurrencyValue(stored / 10000m)
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The value is past the range of a Currency.");

    public override VBTypedValue CreateValue(RDCore.SDK.Model.Values.Bindings.IBindingHandle handle) => new VBCurrencyValue(handle);

    // neither bound is a Double: a Double is within the range when the Currency it makes is.
    public override bool Holds(double value) => TenThousandthsOf(value) is not null;

    // a Double's exact value in ten-thousandths, rounded to the nearest, a tie to the even one: what MS-VBA makes a Currency of - 123456789012.3456 keeps its
    // four decimals, and 922337203685477.5 is within the range, where the Decimal a Double converts to has 15 significant digits; 1.03125 is 1.0312 (recorded in
    // Excel, grid oracle\s52). Nothing when it is past the range, or no number.
    private static long? TenThousandthsOf(double value)
    {
        if (!double.IsFinite(value))
        {
            return null;
        }

        // the value is mantissa * 2^exponent, exactly.
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0xF_FFFF_FFFF_FFFF;
        if (exponent == 0)
        {
            exponent = 1;
        }
        else
        {
            mantissa |= 1L << 52;
        }

        exponent -= 1075;
        var scaled = new BigInteger(mantissa) * 10000;
        if (exponent >= 0)
        {
            scaled <<= exponent;
        }
        else
        {
            var denominator = BigInteger.One << -exponent;
            scaled = BigInteger.DivRem(scaled, denominator, out var remainder);
            var twice = remainder * 2;
            if (twice > denominator || (twice == denominator && !scaled.IsEven))
            {
                scaled += 1;
            }
        }

        if (bits < 0)
        {
            scaled = -scaled;
        }

        return scaled >= long.MinValue && scaled <= long.MaxValue ? (long)scaled : null;
    }
}
