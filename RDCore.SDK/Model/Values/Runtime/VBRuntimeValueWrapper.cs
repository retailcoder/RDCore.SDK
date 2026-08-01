namespace RDCore.SDK.Model.Values.Runtime;

public readonly record struct VBRuntimeValueWrapper
{
    public VBRuntimeValueWrapper(IRuntimeValue value)
    {
        RuntimeValue = value;
    }
    public VBRuntimeValueWrapper(VBRuntimeReference reference)
    {
        RuntimeReference = reference;
    }
    public VBRuntimeValueWrapper(VBRuntimeVariantValue variant)
    {
        RuntimeVariant = variant;
    }

    public IRuntimeValue? RuntimeValue { get; init; }
    public VBRuntimeReference? RuntimeReference { get; init; }
    public VBRuntimeVariantValue? RuntimeVariant { get; init; }
}
