using System.Globalization;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.Tests.Model.Types;

/// <summary>
/// MS-VBA makes a <c>Decimal</c> a <c>Double</c> by rounding twice, and a <c>Single</c> of that <c>Double</c>; it makes a <c>Currency</c> a <c>Double</c>
/// or a <c>Single</c> by rounding once. The expected values were observed in Excel, as the bits of the result, except where a row says otherwise.
/// </summary>
[TestClass]
[TestCategory("MS-VBAL 5.5.1.2.1 Let-coercion between numeric types")]
public sealed class FloatingPointFromFixedPointTests
{
    private static decimal Number(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
    private static string Bits(double value) => BitConverter.DoubleToInt64Bits(value).ToString("X16");
    private static string Bits(float value) => BitConverter.SingleToInt32Bits(value).ToString("X8");

    // the Double nearest each value is the one next to the expected Double.
    [TestMethod]
    [DataRow("0.000708160857053799161669089", "3F47347BDC554EE4")]
    [DataRow("0.00015142993388462449420578", "3F23D9253A32F114")]
    [DataRow("10.05816778433631985469989", "40241DC82AF6CF81")]
    [DataRow("0.00002589723100652162911", "3EFB27BC29E6E603")]
    [DataRow("8.4301759287599060567112", "4020DC4004F319B6")]
    [DataRow("327655774543766665.222526", "43923044C3892593")]
    [DataRow("13.95534734926221211527227", "402BE92349AACD91")]
    [DataRow("49594124253617810.4183273", "43660632FBE2ABD3")]
    [DataRow("0.00713636791795131845878286", "3F7D3B062D1E0051")]
    [DataRow("48524.1786737239221237754", "40E7B185B7B1F51C")]
    [DataRow("0.00095148689214839527120584", "3F4F2DA68ACBFB64")]
    [DataRow("0.349548356288253327503803", "3FD65F0011A83B0D")]
    public void ADecimal_IsADoubleRoundedTwice(string value, string expected)
        => Assert.AreEqual(expected, Bits(VBDecimalType.ToDouble(Number(value))));

    // not observed: a negative value is the negated Double, and the bounds of a Decimal are -2^96 and 2^96.
    [TestMethod]
    [DataRow("-0.000708160857053799161669089", "BF47347BDC554EE4")]
    [DataRow("0", "0000000000000000")]
    [DataRow("0.5", "3FE0000000000000")]
    [DataRow("79228162514264337593543950335", "45F0000000000000")]
    [DataRow("-79228162514264337593543950335", "C5F0000000000000")]
    public void ADecimal_KeepsItsSignAndRange(string value, string expected)
        => Assert.AreEqual(expected, Bits(VBDecimalType.ToDouble(Number(value))));

    // the Single nearest each of the first, third, fifth, sixth and seventh is the one next to the expected Single.
    [TestMethod]
    [DataRow("1.000000059604644775390626", "3F800000")]
    [DataRow("1.000000059604644775390624", "3F800000")]
    [DataRow("1.000000178813934326171874", "3F800002")]
    [DataRow("1.000000178813934326171876", "3F800002")]
    [DataRow("16777217.000000000000000001", "4B800000")]
    [DataRow("16777218.9999999999999999999", "4B800002")]
    [DataRow("-1.000000059604644775390626", "BF800000")]
    public void ADecimal_IsASingleOfTheDouble(string value, string expected)
        => Assert.AreEqual(expected, Bits(VBDecimalType.ToSingle(Number(value))));

    // the number of ten-thousandths made a Double, then divided by 10000, is the Double next to each expected one.
    [TestMethod]
    [DataRow("75299393168858.7426", "42D11F00653976B0")]
    [DataRow("474349068582334.0163", "42FAF6AFFBB7DBE0")]
    [DataRow("507673376957223.0605", "42FCDB9EB4BA3271")]
    [DataRow("451745006929646.274", "42F9ADC14AAC6EE4")]
    [DataRow("92350004808661.5097", "42D4FF7A4ED2F561")]
    [DataRow("97430104267771.7294", "42D6272DB056FEEF")]
    [DataRow("0.0001", "3F1A36E2EB1C432D")]
    [DataRow("-922337203685477.5808", "C30A36E2EB1C432D")]
    [DataRow("922337203685477.5807", "430A36E2EB1C432D")]
    [DataRow("0", "0000000000000000")]
    public void ACurrency_IsTheNearestDouble(string value, string expected)
        => Assert.AreEqual(expected, Bits(VBCurrencyType.ToDouble(new VBRuntimeCurrencyValue(Number(value)))));

    [TestMethod]
    [DataRow("281474993487872.0001", "57800001", DisplayName = "above a tie, where the nearest Double is the tie")]
    [DataRow("281475027042303.9999", "57800001", DisplayName = "below a tie, where the nearest Double is the tie")]
    [DataRow("75299393168858.7426", "5688F803")]
    [DataRow("92350004808661.5097", "56A7FBD2")]
    [DataRow("0.0001", "38D1B717")]
    [DataRow("-281474993487872.0001", "D7800001", DisplayName = "not observed: a negative value is the negated Single")]
    public void ACurrency_IsTheNearestSingle(string value, string expected)
        => Assert.AreEqual(expected, Bits(VBCurrencyType.ToSingle(new VBRuntimeCurrencyValue(Number(value)))));

    [TestMethod]
    public void ADecimalValue_IsItsDouble()
        => Assert.AreEqual("3F47347BDC554EE4", Bits(new VBDecimalValue(Number("0.000708160857053799161669089")).AsDouble));

    [TestMethod]
    public void ACurrencyValue_IsItsDouble()
        => Assert.AreEqual("42D11F00653976B0", Bits(new VBCurrencyValue(Number("75299393168858.7426")).AsDouble));

    [TestMethod]
    public void TheRangeOfACurrency_IsItsBoundsAsDoubles()
    {
        Assert.AreEqual("C30A36E2EB1C432D", Bits(VBCurrencyType.TypeInfo.ManagedMinValue));
        Assert.AreEqual("430A36E2EB1C432D", Bits(VBCurrencyType.TypeInfo.ManagedMaxValue));
    }
}
