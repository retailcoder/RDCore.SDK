using RDCore.SDK.Model;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

/// <summary>
/// The types that moved from the SDK to RDCore.SDK.Protocols are forwarded: code built against the SDK names them in the SDK's assembly, and
/// finds them in the Protocols one.
/// </summary>
[TestClass]
public sealed class ProtocolsTypeForwardsTests
{
    private static IEnumerable<object[]> MovedTypes() =>
    [
        [typeof(SupportedLanguage)],
        [typeof(SupportedLanguages)],
        [typeof(WorkspacePaths)],
        [typeof(ImplicitDeclarationScope)],
        [typeof(AccessModifier)],
        [typeof(RDCore.SDK.Runtime.Abstract.Execution.ErrorBreakMode)],
        [typeof(RDCore.SDK.Workspace.Forms.FormControlClass)],
    ];

    [TestMethod]
    [DynamicData(nameof(MovedTypes))]
    public void AMovedType_NamedInTheSdksAssembly_IsTheOneInTheProtocolsAssembly(Type moved)
    {
        var sdk = typeof(RDCoreDocument).Assembly.GetName().Name;

        var resolved = Type.GetType($"{moved.FullName}, {sdk}", throwOnError: true);

        Assert.AreSame(moved, resolved);
        Assert.AreSame(typeof(ProjectFile).Assembly, resolved!.Assembly);
    }
}
