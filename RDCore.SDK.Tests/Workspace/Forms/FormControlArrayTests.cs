using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Workspace.Forms;

namespace RDCore.Tests.Workspace.Forms;

/// <summary>
/// The class of a Visual Basic 6.0 control array: its default member takes the index and returns a control of the array's class, <c>Count</c>, <c>LBound</c>
/// and <c>UBound</c> are <c>Integer</c>, and its hidden enumerator is what <c>For Each</c> asks for.
/// </summary>
[TestClass]
public sealed class FormControlArrayTests
{
    private static readonly Uri Root = new("file:///C:/workspace/");

    private static VBTypeMemberSymbol[] MembersOf(VBType element) => [.. ((VBClassType)FormControlArray.TypeOf(element, Root)).Symbol.Members.OfType<VBTypeMemberSymbol>()];

    private static bool IsHidden(Symbol member) => (member.GetProperty(SymbolProperties.MemberFlags) & SymbolProperties.HiddenMemberFlag) != 0;

    [TestMethod]
    public void TheEnumerator_IsAHiddenMember_OfDispatchIdentifierMinus4()
    {
        var enumerator = MembersOf(VBIntegerType.TypeInfo).Single(member => member.GetProperty(SymbolProperties.UserMemId) == -4);

        Assert.AreEqual("_NewEnum", enumerator.Name);
        Assert.IsInstanceOfType<VBPropertyGetMemberSymbol>(enumerator);
        Assert.IsTrue(IsHidden(enumerator));
    }

    [TestMethod]
    public void TheDefaultMember_ReturnsTheElement_AndTheBoundsAreIntegers()
    {
        var members = MembersOf(VBStringType.TypeInfo);

        var item = members.Single(member => member.Name == "_Default");
        Assert.AreEqual(0, item.GetProperty(SymbolProperties.UserMemId));
        Assert.AreEqual(VBStringType.TypeInfo, ((ITypedSymbol)item).ResolvedType);
        Assert.IsTrue(IsHidden(item));
        foreach (var bound in new[] { "Count", "LBound", "UBound" })
        {
            Assert.AreEqual(VBIntegerType.TypeInfo, ((ITypedSymbol)members.Single(member => member.Name == bound)).ResolvedType, bound);
        }
    }

    [TestMethod]
    public void TheElementType_IsReadBack_FromTheArraysType()
        => Assert.AreEqual(VBStringType.TypeInfo, FormControlArray.ElementOf(FormControlArray.TypeOf(VBStringType.TypeInfo, Root)));
}
