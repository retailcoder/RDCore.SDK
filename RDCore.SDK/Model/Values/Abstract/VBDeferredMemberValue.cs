using RDCore.SDK.Model.Types;

namespace RDCore.SDK.Model.Values.Abstract;

public abstract record class VBDeferredMemberValue() : VBTypedValue(VBVariantType.TypeInfo)
{
    public override int Size => sizeof(int);

    public string Name { get; init; } = string.Empty;
    public VBDeferredMemberValue WithName(string name) => this with { Name = name };
}