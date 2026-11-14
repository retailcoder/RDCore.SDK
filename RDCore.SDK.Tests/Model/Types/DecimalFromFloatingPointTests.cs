using System.Globalization;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.Tests.Model.Types;

/// <summary>
/// MS-VBA makes a <c>Single</c> a <c>Decimal</c> as its value to 7 significant digits, and a <c>Double</c> as its value to 15, a tie to the even digit. Every
/// expected value was observed in Excel.
/// </summary>
[TestClass]
[TestCategory("MS-VBAL 5.5.1.2.1 Let-coercion between numeric types")]
public sealed class DecimalFromFloatingPointTests
{
    // the value as MS-VBA writes it: a Decimal's trailing zeros are not written.
    private static string Text(decimal value) => value.ToString(CultureInfo.InvariantCulture) is var text && text.Contains('.') ? text.TrimEnd('0').TrimEnd('.') : text;

    [TestMethod]
    [DataRow(16777217f, "16777220")]
    [DataRow(0.1f, "0.1")]
    [DataRow(1f / 3f, "0.3333333")]
    [DataRow(123456.789f, "123456.8")]
    [DataRow(1E-20f, "0.00000000000000000001")]
    [DataRow(-2.5f, "-2.5")]
    [DataRow(9999999f, "9999999")]
    [DataRow(99999999f, "100000000")]
    [DataRow(1.5E-07f, "0.00000015")]
    [DataRow(12345678f, "12345680")]
    [DataRow(0.000001234567f, "0.000001234567")]
    [DataRow(0.5f, "0.5")]
    [DataRow(1E+28f, "9999999000000000000000000000")]
    [DataRow(2.5E-28f, "0.0000000000000000000000000003")]
    [DataRow(1E-28f, "0.0000000000000000000000000001")]
    [DataRow(1.25f, "1.25")]
    [DataRow(1234.5678f, "1234.568")]
    [DataRow(12345665f, "12345660", DisplayName = "a tie, to the even digit below")]
    [DataRow(12345675f, "12345680", DisplayName = "a tie, to the even digit above")]
    [DataRow(-12345665f, "-12345660", DisplayName = "a negative tie")]
    [DataRow(12345645f, "12345640")]
    [DataRow(1234566.5f, "1234566")]
    [DataRow(1234567.5f, "1234568")]
    [DataRow(16777215f, "16777220")]
    [DataRow(0.0001234565f, "0.0001234565")]
    public void ASingle_IsItsSevenSignificantDigits(float value, string expected)
        => Assert.AreEqual(expected, Text(VBDecimalType.FromSingle(value)));

    [TestMethod]
    [DataRow(0.1, "0.1")]
    [DataRow(1.0 / 3, "0.333333333333333")]
    [DataRow(16777217.123456789, "16777217.1234568")]
    [DataRow((double)0.1f, "0.100000001490116", DisplayName = "a Single made a Double first")]
    [DataRow(3E+20, "300000000000000000000")]
    public void ADouble_IsItsFifteenSignificantDigits(double value, string expected)
        => Assert.AreEqual(expected, Text(VBDecimalType.FromDouble(value)));

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-0.0)]
    [DataRow(1E-300)]
    [DataRow(-4E-29)]
    public void ADoubleTooSmallForTheScale_IsZero(double value)
    {
        var result = VBDecimalType.FromDouble(value);
        Assert.AreEqual(0m, result);
        Assert.AreEqual("0", Text(result));
    }

    [TestMethod]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(double.NaN)]
    [DataRow(1E+29)]
    [DataRow(-1E+29)]
    public void ADoublePastTheRange_Overflows(double value)
        => Assert.ThrowsExactly<OverflowException>(() => VBDecimalType.FromDouble(value));

    [TestMethod]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NaN)]
    [DataRow(float.MaxValue)]
    public void ASinglePastTheRange_Overflows(float value)
        => Assert.ThrowsExactly<OverflowException>(() => VBDecimalType.FromSingle(value));

    [TestMethod]
    public void ADecimalValueOfADouble_IsItsFifteenSignificantDigits()
        => Assert.AreEqual(0.333333333333333m, ((VBDecimalValue)VBDecimalType.TypeInfo.CreateValue(1.0 / 3)).Value);
}
