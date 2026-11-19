using RDCore.SDK.Model;

namespace RDCore.Tests.Model;

/// <summary>
/// The keywords of the language (<strong>MS-VBAL §3.3.5.2</strong>): every <c>reserved-identifier</c> is one, and so is a
/// <c>reserved-type-identifier</c>, which the production of <c>reserved-identifier</c> does not name. Only a <c>reserved-type-identifier</c> that is no
/// <c>reserved-name</c> is a name the specification allows.
/// </summary>
[TestClass]
public sealed class VBIdentifierTests
{
    [TestMethod]
    [DataRow("Circle", true, true, DisplayName = "a special-form")]
    [DataRow("Dim", true, true, DisplayName = "a statement-keyword")]
    [DataRow("Len", true, true, DisplayName = "a reserved-name")]
    [DataRow("Date", true, true, DisplayName = "a reserved-name that is a reserved-type-identifier")]
    [DataRow("string", true, true, DisplayName = "a reserved-name that is a reserved-type-identifier, in any case")]
    [DataRow("True", true, true, DisplayName = "a literal-identifier")]
    [DataRow("Attribute", true, true, DisplayName = "a reserved-for-implementation-use")]
    [DataRow("Decimal", true, true, DisplayName = "a future-reserved")]
    [DataRow("Long", true, false, DisplayName = "a reserved-type-identifier only")]
    [DataRow("variant", true, false, DisplayName = "a reserved-type-identifier only, in any case")]
    [DataRow("Local", false, false, DisplayName = "no keyword")]
    [DataRow("Shapes", false, false, DisplayName = "a name")]
    public void AKeyword_IsAReservedIdentifier_UnlessItIsOnlyATypeIdentifier(string name, bool keyword, bool reservedIdentifier)
    {
        Assert.AreEqual(keyword, VBIdentifier.IsReserved(name), nameof(VBIdentifier.IsReserved));
        Assert.AreEqual(reservedIdentifier, VBIdentifier.IsReservedIdentifier(name), nameof(VBIdentifier.IsReservedIdentifier));
    }
}
