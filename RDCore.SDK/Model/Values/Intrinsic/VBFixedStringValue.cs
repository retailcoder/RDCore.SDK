using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.SDK.Model.Values.Intrinsic;

public sealed record class VBFixedStringValue : VBStringValue
{
    public VBFixedStringValue(int length) 
        : base(new ValueBindingHandle(new VBRuntimeValue<string>(new string(' ', length))))
    {
        Length = length;
    }

    public VBFixedStringValue(VBStringValue value) : base(new ValueBindingHandle(new VBRuntimeValue<string>(FixLength(value.Value, value.Length))))
    {
        Length = value.Length;
    }

    public override int Length { get; }

    public VBFixedStringValue WithFixedValue(string value) => new(WithValue(value));

    public override VBStringValue WithValue(string? value)
    {
        var fixedValue = FixLength(value, Length);
        return this with { Handle = new ValueBindingHandle(new VBRuntimeValue<string>(fixedValue)) };
    }

    private static string FixLength(string? value, int length)
    {
        // MS-VBAL 5.5.1.2.5 let-coercion to String*length (fixed-length strings)
        value ??= string.Empty;
        if (value.Length > length)
        {
            value = value[..length];
        }
        else if (value.Length < length)
        {
            value = value.PadRight(length, ' ');
        }
        return value;
    }
}