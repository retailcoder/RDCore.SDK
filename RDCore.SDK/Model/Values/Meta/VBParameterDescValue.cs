using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Values.Abstract;

namespace RDCore.SDK.Model.Values.Meta;

/// <summary>
/// A meta-value that represents a <see cref="VBParameterSymbol"/>.
/// </summary>
public record class VBParameterDescValue(VBParameterSymbol Parameter) 
    : VBTypedValue(Parameter.ResolvedType)
{
    public override int Size => sizeof(int);
}