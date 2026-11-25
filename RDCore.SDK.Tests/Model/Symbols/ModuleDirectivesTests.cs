using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;

namespace RDCore.Tests.Model.Symbols;

/// <summary>
/// The directives of a module that declares none (<see cref="ModuleDirectives.None"/>): what is read of such a module, a class module creatable.
/// </summary>
[TestClass]
public sealed class ModuleDirectivesTests
{
    private static readonly Uri Module = new("file:///c:/ws/Class1.cls");

    [TestMethod]
    public void None_IsWhatIsReadOfAModuleThatDeclaresNone()
    {
        var empty = new ModuleNode(new SyntaxNodeId(Module.ToString(), [1]), new SourceLocation { Uri = Module, Range = SourceRange.Empty }, []);

        Assert.AreEqual(empty.GetModuleDirectives(), ModuleDirectives.None);
        Assert.AreEqual(((ModuleNode?)null).GetModuleDirectives(), ModuleDirectives.None);
    }

    [TestMethod]
    public void None_IsCreatable()
        => Assert.IsTrue(ModuleDirectives.None.Creatable);
}
