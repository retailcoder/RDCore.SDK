using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.Completion;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Workspace.Forms;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// In the code of a class module built on a library's class (a Visual Basic 6.0 UserControl's, on <c>VB.UserControl</c>), the base class's members are names,
/// after the module's own; its events are not, and a class module with no base class has none of them.
/// </summary>
[TestClass]
public sealed class BaseClassNamesTests
{
    private static readonly Uri Root = new("file://rdcore-test");
    private static readonly SourceRange R = SourceRange.Empty;

    private static VBModuleFieldVariableMemberSymbol Field(Uri moduleUri, string name, AccessModifier access)
        => new(Root, moduleUri, name, ScopeKind.Module, VBLongType.TypeInfo, R, R, access);

    private static ScopeTree Workspace(out VBClassModuleSymbol userControl1, out VBClassModuleSymbol class1)
    {
        var declared = new VBClassModuleSymbol(Root, Root, "UserControl");
        var backColor = Field(declared.Uri, "BackColor", AccessModifier.Public);
        var resize = new VBEventMemberSymbol(Root, declared.Uri, "Resize", ScopeKind.Module, R, R, AccessModifier.Public);
        var library = (VBClassModuleSymbol)(declared with { DefaultInterfaceMembers = [backColor, resize] }).With(SymbolProperties.Library, "VB");

        userControl1 = new VBClassModuleSymbol(Root, Root, "UserControl1") { BaseClass = new FormControlClass("VB", "UserControl") };
        class1 = new VBClassModuleSymbol(Root, Root, "Class1");
        var caption = Field(userControl1.Uri, "Caption", AccessModifier.Private);
        return ScopeTreeBuilder.Build([library, backColor, resize, userControl1, caption, class1]);
    }

    private static string[] Labels(ScopeTree tree, Uri origin) => [.. CompletionCandidates.Names(tree, origin).Select(candidate => candidate.Label)];

    [TestMethod]
    public void TheMembersOfTheBaseClass_AreNamesOfTheModule_AfterItsOwn()
    {
        var tree = Workspace(out var userControl1, out _);

        var labels = Labels(tree, userControl1.Uri);

        CollectionAssert.Contains(labels, "BackColor");
        Assert.IsLessThan(Array.IndexOf(labels, "BackColor"), Array.IndexOf(labels, "Caption"));
    }

    [TestMethod]
    public void AnEventOfTheBaseClass_IsNoName()
    {
        var tree = Workspace(out var userControl1, out _);

        CollectionAssert.DoesNotContain(Labels(tree, userControl1.Uri), "Resize");
    }

    [TestMethod]
    public void AClassModuleWithNoBaseClass_HasNoneOfItsMembers()
    {
        var tree = Workspace(out _, out var class1);

        CollectionAssert.DoesNotContain(Labels(tree, class1.Uri), "BackColor");
    }
}
