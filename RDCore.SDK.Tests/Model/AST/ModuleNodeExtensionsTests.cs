using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.Source;

namespace RDCore.Tests.Model.AST;

/// <summary>
/// The name a module declares (<strong>MS-VBAL §4.2</strong>): the directive that says it, and the name it says.
/// </summary>
[TestClass]
public sealed class ModuleNodeExtensionsTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");
    private static int _next;

    private static SyntaxNodeId Id() => new(Module.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation At(int line) => new() { Uri = Module, Range = new SourceRange(line, 0, line, 20) };

    private static ModuleNode ModuleOf(params SyntaxNode[] children) => new(Id(), At(0), [.. children]);

    [TestMethod]
    public void TheVBNameDirective_DeclaresTheModuleName()
    {
        var description = new AttributeDirectiveNode(Id(), At(0), Tokens.VB_Description, "\"Counts\"", "Main");
        var name = new AttributeDirectiveNode(Id(), At(1), "vb_name", "\"Counts\"");
        var another = new AttributeDirectiveNode(Id(), At(2), Tokens.VB_Name, "\"Other\"");
        var module = ModuleOf(description, name, another);

        Assert.AreSame(name, module.GetDeclaredNameDirective());
        Assert.AreEqual("Counts", module.GetDeclaredName());
    }

    [TestMethod]
    public void AModuleThatSaysNoVBName_DeclaresNoName()
    {
        var module = ModuleOf(new AttributeDirectiveNode(Id(), At(0), Tokens.VB_Exposed, "False"));

        Assert.IsNull(module.GetDeclaredNameDirective());
        Assert.IsNull(module.GetDeclaredName());
    }
}
