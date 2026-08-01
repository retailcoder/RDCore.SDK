using System.Runtime.InteropServices;
namespace RDCore.SDK.Model.Values.Runtime;

[StructLayout(LayoutKind.Explicit)]
public readonly record struct VBRuntimeBooleanValue : IRuntimeValue
{
    public VBRuntimeBooleanValue(bool value)
    {
        StoredValue = (short)(value ? -1 : 0);
    }
    public VBRuntimeBooleanValue(short value)
    {
        StoredValue = value;
    }

    [FieldOffset(0)] public readonly short StoredValue;

    public object BoxedValue => StoredValue != 0 ? -1 : 0;

    public static explicit operator bool(VBRuntimeBooleanValue value) => value.StoredValue != 0;
}
