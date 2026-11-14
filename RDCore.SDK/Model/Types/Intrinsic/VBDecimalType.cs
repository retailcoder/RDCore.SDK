#pragma warning disable IDE0130 // Namespace does not match folder structure
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.SDK.Model.Types;

/// <summary>
/// A <see cref="VBNumericType{Decimal}"/> representing the <c>Decimal</c> data type.
/// </summary>
/// <remarks>
/// The <em>managed type</em> of a value of this data type is <c>decimal</c>.<br/>
/// 👉 Implements <see cref="IFixedPointNumericType"/>.
/// </remarks>
public sealed record class VBDecimalType() : VBNumericType<decimal>(VBTypeNames.VBDecimal), IFixedPointNumericType
{
    private static readonly Lazy<VBDecimalType> _instance = new(() => new(), LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// The <c>Decimal</c> data type.
    /// </summary>
    public static VBDecimalType TypeInfo => _instance.Value;

    private static readonly Lazy<VBDecimalValue> _defaultValue = new(() => VBDecimalType.Zero, LazyThreadSafetyMode.PublicationOnly);
    public override VBTypedValue DefaultValue => _defaultValue.Value;

    private static readonly Lazy<VBDecimalValue> _minValue = new(() => new VBDecimalValue(decimal.MinValue), LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// Gets the minimum representable value for this data type.
    /// </summary>
    public static VBDecimalValue MinValue => _minValue.Value;
    public override double ManagedMinValue => ToDouble(decimal.MinValue);

    private static readonly Lazy<VBDecimalValue> _maxValue = new(() => new VBDecimalValue(decimal.MaxValue), LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// Gets the maximum representable value for this data type.
    /// </summary>
    public static VBDecimalValue MaxValue => _maxValue.Value;
    public override double ManagedMaxValue => ToDouble(decimal.MaxValue);

    private static readonly Lazy<VBDecimalValue> _zero = new(() => new VBDecimalValue(0m), LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// Gets the value <c>0</c> (zero) representation of this data type.
    /// </summary>
    public static VBDecimalValue Zero => _zero.Value;


    /// <summary>
    /// Converts a <c>Double</c> to a <c>Decimal</c> as MS-VBA does: the value rounded to 15 significant digits, a tie to the even digit
    /// (<strong>MS-VBAL §5.5.1.2.1</strong>).
    /// </summary>
    /// <param name="value">The <c>Double</c> to convert.</param>
    /// <returns>
    /// The <c>Decimal</c>: <c>0.1</c> is 0.1, and <c>1 / 3</c> is 0.333333333333333. A value too small for the 28 decimal places of a <c>Decimal</c> is 0.
    /// </returns>
    /// <exception cref="OverflowException"><paramref name="value"/> is an infinity, a NaN, or past the range of a <c>Decimal</c>.</exception>
    /// <remarks>
    /// MS-VBAL §5.5.1.2.1 rounds the value to the nearest <c>Decimal</c>. MS-VBA first rounds it to the 15 significant digits a <c>Double</c> holds, so that
    /// no digit of its binary representation past them reaches the <c>Decimal</c> (observed in Excel). The result does not depend on the .NET version.
    /// </remarks>
    public static decimal FromDouble(double value) => FloatingPointToDecimal.FromDouble(value);

    /// <summary>
    /// Converts a <c>Single</c> to a <c>Decimal</c> as MS-VBA does: the value rounded to 7 significant digits, a tie to the even digit
    /// (<strong>MS-VBAL §5.5.1.2.1</strong>).
    /// </summary>
    /// <param name="value">The <c>Single</c> to convert.</param>
    /// <returns>
    /// The <c>Decimal</c>: <c>0.1</c> is 0.1, and 16777217, which is the <c>Single</c> 16777216, is 16777220. A value too small for the 28 decimal places
    /// of a <c>Decimal</c> is 0.
    /// </returns>
    /// <exception cref="OverflowException"><paramref name="value"/> is an infinity, a NaN, or past the range of a <c>Decimal</c>.</exception>
    /// <remarks>
    /// MS-VBAL §5.5.1.2.1 rounds the value to the nearest <c>Decimal</c>. MS-VBA first rounds it to the 7 significant digits a <c>Single</c> holds
    /// (observed in Excel). A <c>Single</c> converted to a <c>Double</c> first keeps 15 digits instead: <c>CDec(CDbl(CSng(0.1)))</c> is 0.100000001490116.
    /// The result does not depend on the .NET version.
    /// </remarks>
    public static decimal FromSingle(float value) => FloatingPointToDecimal.FromSingle(value);

    /// <summary>
    /// Converts a <c>Decimal</c> to a <c>Double</c> as MS-VBA does: the 96-bit integer of the <c>Decimal</c> made a <c>Double</c>, then divided by the
    /// <c>Double</c> power of ten of its scale (<strong>MS-VBAL §5.5.1.2.1</strong>).
    /// </summary>
    /// <param name="value">The <c>Decimal</c> to convert.</param>
    /// <returns>
    /// The <c>Double</c>. It is the <c>Double</c> nearest the value, or the other <c>Double</c> next to the value when the value is close to halfway between
    /// the two: <c>CDbl(CDec("0.000708160857053799161669089"))</c> is the <c>Double</c> above it, where the nearest is the one below.
    /// </returns>
    /// <remarks>
    /// MS-VBAL §5.5.1.2.1 rounds the value to the nearest <c>Double</c>. MS-VBA rounds it twice, as the OLEAUT32 <c>VarR8FromDec</c> function does
    /// (observed in Excel). <c>CDbl</c>, a <c>Let</c> to a <c>Double</c> and the <c>^</c> operator make this <c>Double</c>, and so does a conversion of a
    /// <c>Decimal</c> to a whole number before it rounds: <c>CLng(CDec("2.50000000000000000000000001"))</c> is 2. The result does not depend on the .NET
    /// version.
    /// </remarks>
    public static double ToDouble(decimal value) => DecimalToFloatingPoint.ToDouble(value);

    /// <summary>
    /// Converts a <c>Decimal</c> to a <c>Single</c> as MS-VBA does: the <c>Double</c> that <see cref="ToDouble(decimal)"/> makes, rounded to the nearest
    /// <c>Single</c> (<strong>MS-VBAL §5.5.1.2.1</strong>).
    /// </summary>
    /// <param name="value">The <c>Decimal</c> to convert.</param>
    /// <returns>
    /// The <c>Single</c>: <c>CSng(CDec("1.000000059604644775390626"))</c> is 1, where the nearest <c>Single</c> is the one above 1.
    /// </returns>
    /// <remarks>
    /// MS-VBAL §5.5.1.2.1 rounds the value to the nearest <c>Single</c>. MS-VBA rounds the <c>Double</c> it makes first, as the OLEAUT32
    /// <c>VarR4FromDec</c> function does (observed in Excel). The result does not depend on the .NET version.
    /// </remarks>
    public static float ToSingle(decimal value) => DecimalToFloatingPoint.ToSingle(value);

    /// <summary>
    /// Creates a <c>Decimal</c> value of a <c>Double</c>, rounded to 15 significant digits as <see cref="FromDouble(double)"/> rounds it.
    /// </summary>
    /// <param name="value">The <c>Double</c> to convert.</param>
    /// <exception cref="OverflowException"><paramref name="value"/> is an infinity, a NaN, or past the range of a <c>Decimal</c>.</exception>
    public override VBNumericTypedValue CreateValue(double value) => new VBDecimalValue(FromDouble(value));
    public override VBTypedValue CreateValue(RDCore.SDK.Model.Values.Bindings.IBindingHandle handle) => new VBDecimalValue(handle);

    // neither bound is a Double: the Double nearest each is ±2^96, which is past it, and is left out.
    public override bool Holds(double value) => value > ManagedMinValue && value < ManagedMaxValue;
}
