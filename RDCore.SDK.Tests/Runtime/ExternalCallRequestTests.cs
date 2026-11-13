using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Runtime.Abstract.Execution;

namespace RDCore.Tests.Runtime;

[TestClass]
[TestCategory("RD-VBAL §2.5 Runtime Values")]
public sealed class ExternalCallRequestTests
{
    // an array an assignment copies is bound to no storage until a variable holds it (VBArrayValue.DataCopy): the value assigned to a property of a server
    // is one, and the call's description is what the error of a refused call says.
    [TestMethod]
    public void AnArrayInAVariant_ThatNoVariableHolds_IsDescribedByItsShape()
    {
        var array = new VBFixedSizeArrayValue([(1, 2), (0, 3)], VBVariantType.TypeInfo).DataCopy();

        var request = new ExternalCallRequest(ClassLifecycleInterface.Initialize, [new VBVariantValue(array).RuntimeValue]);

        Assert.AreEqual("Initialize(Variant(1 To 2, 0 To 3))", request.Describe());
    }

    [TestMethod]
    public void AnArrayInItsStorage_IsDescribedByItsShape()
    {
        var array = new VBFixedSizeArrayValue([(0, 1)], VBLongType.TypeInfo);

        var request = new ExternalCallRequest(ClassLifecycleInterface.Initialize, [new VBRuntimeValue<VBRuntimeArrayValue>(new VBRuntimeArrayValue(array))]);

        Assert.AreEqual("Initialize(Long(0 To 1))", request.Describe());
    }

    [TestMethod]
    public void AnArrayThatIsNotAllocated_IsDescribedAsOne()
    {
        var array = new VBResizableArrayValue([], VBStringType.TypeInfo);

        var request = new ExternalCallRequest(ClassLifecycleInterface.Initialize, [new VBVariantValue(array).RuntimeValue]);

        Assert.AreEqual("Initialize(String())", request.Describe());
    }
}
