using RDCore.SDK.Model;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;

namespace RDCore.Tests.Model.Symbols;

// placement tests read a member as visible to its project by its access modifier: Private is not, and with none a module variable or a constant is not;
// another project sees nothing.
internal sealed class AccessModifierVisibility : IProjectVisibility
{
    public static AccessModifierVisibility Instance { get; } = new();

    public bool IsProjectVisible(Symbol member, VBModuleSymbol module) => member is AccessibleTypedSymbol accessible && accessible.AccessModifier switch
    {
        AccessModifier.Private => false,
        AccessModifier.Public or AccessModifier.Global or AccessModifier.Friend => true,
        _ => member is not (VBModuleFieldVariableMemberSymbol or VBConstantMemberSymbol),
    };

    public bool IsVisibleFrom(Symbol member, VBModuleSymbol module, Uri? originProject)
        => originProject is not null && originProject.AbsoluteUri == module.WorkspaceRoot.AbsoluteUri && IsProjectVisible(member, module);
}
