using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.Tests.Model.Types;

[TestClass]
[TestCategory("MS-VBAL §2.2 Entities and Declared Types")]
public sealed class VBLongPtrTypeTests
{
    // a value a cell is read as is of the type that reads it: a 32-bit LongPtr is a Long, 4 bytes wide.
    [TestMethod]
    public void TheX86Type_CreatesA32BitValue_OfItsOwnType()
    {
        var value = VBLongPtrType_x86.TypeInfo.CreateValue(new ValueBindingHandle(new VBRuntimeValue<int>(1234)));

        Assert.AreSame(VBLongPtrType_x86.TypeInfo, value.TypeInfo);
        Assert.AreEqual(4, value.Size);
        Assert.AreEqual(1234L, ((VBLongPtrValue)value).Value);
    }

    // a 64-bit LongPtr is a LongLong, 8 bytes wide.
    [TestMethod]
    public void TheX64Type_CreatesA64BitValue_OfItsOwnType()
    {
        var value = VBLongPtrType_x64.TypeInfo.CreateValue(new ValueBindingHandle(new VBRuntimeValue<long>(1234L)));

        Assert.AreSame(VBLongPtrType_x64.TypeInfo, value.TypeInfo);
        Assert.AreEqual(8, value.Size);
        Assert.AreEqual(1234L, ((VBLongPtrValue)value).Value);
    }

    // a value made of a number is as wide as the number's type says, as one made of a cell is.
    [TestMethod]
    public void AValueMadeOfANumber_IsAsWideAsOneMadeOfACell()
    {
        Assert.AreEqual(4, new VBLongPtrValue(7).Size);
        Assert.AreEqual(8, new VBLongPtrValue(7L).Size);
    }
}
