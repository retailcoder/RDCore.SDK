using RDCore.SDK.Model.Symbols.Abstract;
namespace RDCore.SDK.Model.Values.Runtime;

public readonly record struct VBRuntimeReference(Type ManagedType, object Value) : IRuntimeValue
{
    public static readonly VBRuntimeReference NullRef = new(typeof(Object), null!);
    public static readonly VBRuntimeReference NullStringRef = new(typeof(string), null!);
    public static readonly VBRuntimeReference EmptyStringRef = new(typeof(string), string.Empty);

    public object BoxedValue => Value;
}