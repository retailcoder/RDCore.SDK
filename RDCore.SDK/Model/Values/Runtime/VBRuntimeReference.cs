using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Runtime.Shared;
namespace RDCore.SDK.Model.Values.Runtime;

public readonly record struct VBRuntimeReference(MemoryAddress Value) : IRuntimeValue
{
    public static readonly VBRuntimeReference NullRef = new(MemoryAddress.Zero);

    public object BoxedValue => Value;
}