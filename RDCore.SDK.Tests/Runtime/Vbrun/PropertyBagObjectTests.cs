using RDCore.SDK.Runtime.Vbrun;

namespace RDCore.Tests.Runtime.Vbrun;

/// <summary>
/// The <c>VBRUN.PropertyBag</c> a container gives a UserControl's <c>ReadProperties</c> (<see cref="PropertyBagObject{TValue}"/>), as VB6
/// reads it while the program runs.
/// </summary>
[TestClass]
public sealed class PropertyBagObjectTests
{
    private static PropertyBagObject<object> Bag(params (string Name, Func<object> Value)[] values) =>
        new(values.Select(v => KeyValuePair.Create(v.Name, v.Value)));

    [TestMethod]
    [DataRow("Caption")]
    [DataRow("caption")]
    [DataRow("CAPTION")]
    public void ReadProperty_OfANameTheBagHolds_InAnyCase_IsItsValue(string name)
    {
        var bag = Bag(("Caption", () => "hello"));

        Assert.AreEqual(PropertyBagFailure.None, bag.TryReadProperty(name, out var value));
        Assert.AreEqual("hello", value);
    }

    [TestMethod]
    public void ReadProperty_WithADefault_IsTheStoredValue_OfItsOwnType()
    {
        var bag = Bag(("Num", () => (short)42));

        Assert.AreEqual(PropertyBagFailure.None, bag.TryReadProperty("Num", "x", out var value));
        Assert.AreEqual((short)42, value);
    }

    [TestMethod]
    public void ReadProperty_OfANameTheBagLacks_IsTheDefaultAsGiven_OrElseError327()
    {
        var bag = Bag();
        var defaultValue = new object();

        Assert.AreEqual(PropertyBagFailure.None, bag.TryReadProperty("Missing", defaultValue, out var value));
        Assert.AreSame(defaultValue, value);
        Assert.AreEqual(PropertyBagFailure.DataValueNotFound, bag.TryReadProperty("Missing", out _));
    }

    [TestMethod]
    public void ReadProperty_OfAnEmptyName_IsError5_WithOrWithoutADefault()
    {
        var bag = Bag(("", () => 1));

        Assert.AreEqual(PropertyBagFailure.InvalidProcedureCall, bag.TryReadProperty("", out _));
        Assert.AreEqual(PropertyBagFailure.InvalidProcedureCall, bag.TryReadProperty("", 0, out _));
    }

    [TestMethod]
    public void ReadProperty_ReadsTheValueWhenItIsAskedFor()
    {
        var width = 100;
        var bag = Bag(("_ExtentX", () => width));
        width = 133;

        Assert.AreEqual(PropertyBagFailure.None, bag.TryReadProperty("_ExtentX", out var value));
        Assert.AreEqual(133, value);
    }

    [TestMethod]
    public void ReadProperty_LetsAHostsExceptionThrough()
    {
        var bag = Bag(("Picture", () => throw new NotSupportedException()));

        Assert.ThrowsExactly<NotSupportedException>(() => bag.TryReadProperty("Picture", out _));
    }

    [TestMethod]
    public void WriteProperty_IsError425_AndTheValueStays()
    {
        var bag = Bag(("Caption", () => "hello"));

        Assert.AreEqual(PropertyBagFailure.InvalidObjectUse, bag.TryWriteProperty("Caption", "x"));
        _ = bag.TryReadProperty("Caption", out var value);
        Assert.AreEqual("hello", value);
    }

    [TestMethod]
    public void TwoValuesOfTheSameName_InAnyCase_AreRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Bag(("Caption", () => 1), ("CAPTION", () => 2)));
    }
}
