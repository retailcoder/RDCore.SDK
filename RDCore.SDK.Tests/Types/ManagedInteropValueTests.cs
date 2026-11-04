using RDCore.SDK.Model.Values.Runtime;
using System.Runtime.CompilerServices;

namespace RDCore.Tests.Types;

[TestClass]
public class ManagedInteropValueTests
{
    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Byte_I1()
    {
        var expected = 1;
        var size = Unsafe.SizeOf<VBRuntimeValue<byte>>();
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Boolean_I2()
    {
        var expected = 2;
        var size = Unsafe.SizeOf<VBRuntimeValue<VBRuntimeBooleanValue>>();
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Int16_I2()
    {
        var expected = 2;
        var size = Unsafe.SizeOf<VBRuntimeValue<short>>();
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Int32_I4()
    {
        var expected = 4;
        var size = Unsafe.SizeOf<VBRuntimeValue<int>>();
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Int64_I8()
    {
        var expected = 8;
        var size = Unsafe.SizeOf<VBRuntimeValue<long>>();
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Single()
    {
        var expected = 4;
        var size = Unsafe.SizeOf<VBRuntimeValue<float>>();
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Double()
    {
        var expected = 8;
        var size = Unsafe.SizeOf<VBRuntimeValue<double>>();
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Currency()
    {
        var expected = 8;
        var size = Unsafe.SizeOf<VBRuntimeValue<VBRuntimeCurrencyValue>>();
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [TestCategory("ManagedInterop")]
    public void IsExpectedSize_Decimal()
    {
        var expected = 14;
        var size = Unsafe.SizeOf<VBRuntimeValue<VBRuntimeDecimalValue>>();
        Assert.AreEqual(expected, size);
    }
}
