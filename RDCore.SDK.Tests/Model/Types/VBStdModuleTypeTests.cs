using RDCore.SDK.Model.Types.Complex;

namespace RDCore.Tests.Model.Types;

/// <summary>
/// <see cref="VBStdModuleType"/>: the type of a standard module, named as the module, with its members.
/// </summary>
[TestClass]
public sealed class VBStdModuleTypeTests
{
    [TestMethod]
    public void AModuleTypeOfItsNameAlone_HasNoMembers()
    {
        var type = new VBStdModuleType("Module1");

        Assert.AreEqual("Module1", type.Name);
        Assert.IsFalse(type.IsHidden);
        Assert.IsFalse(type.Members.IsDefault);
        Assert.AreEqual(0, type.Members.Length);
    }

    [TestMethod]
    public void AHiddenModuleType_IsHidden()
        => Assert.IsTrue(new VBStdModuleType("Module1", isHidden: true).IsHidden);
}
