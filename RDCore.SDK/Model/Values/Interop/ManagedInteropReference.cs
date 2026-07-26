using RDCore.SDK.Model.Symbols.Abstract;
namespace RDCore.SDK.Model.Values.Interop;

public readonly record struct ManagedInteropReference(Type ManagedType, object Value) : IManagedInteropValue
{
    public static readonly ManagedInteropReference NullRef = new(typeof(Object), null!);
    public static readonly ManagedInteropReference NullStringRef = new(typeof(string), null!);
    public static readonly ManagedInteropReference EmptyStringRef = new(typeof(string), string.Empty);

    public object BoxedValue => Value;
}