using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.SDK.Model.Values.Intrinsic;

/// <summary>
/// Represents a <c>LongPtr</c> value.
/// </summary>
/// <param name="Is64Bit">Indicates whether the pointer is a 32-bit (<c>false</c>) or 64-bit (<c>true</c>) pointer.</param>
public sealed record class VBLongPtrValue(bool Is64Bit)
    : VBNumericTypedValue(Is64Bit ? VBLongPtrType_x64.TypeInfo : VBLongPtrType_x86.TypeInfo),
    IVBTypedValue<VBLongPtrValue, long>, INumericValue<VBLongPtrValue>
{
    public VBLongPtrValue(IBindingHandle handle) : this(handle, is64Bit: true) { }

    /// <summary>
    /// A <c>LongPtr</c> value read from a cell, 4 or 8 bytes wide as <paramref name="is64Bit"/> says.
    /// </summary>
    /// <remarks>
    /// What <see cref="VBLongPtrType_x86.CreateValue"/> and <see cref="VBLongPtrType_x64.CreateValue"/> make: a value of the type that reads the cell. The
    /// cell holds an <c>int</c> for a 32-bit pointer and a <c>long</c> for a 64-bit one.
    /// </remarks>
    /// <param name="handle">The cell the value is read from.</param>
    /// <param name="is64Bit"><see langword="true"/> for a 64-bit pointer; <see langword="false"/> for a 32-bit one.</param>
    public VBLongPtrValue(IBindingHandle handle, bool is64Bit) : this(is64Bit) { Handle = handle; }

    public VBLongPtrValue(long value) : this(true)
    {
        Handle = new ValueBindingHandle(new VBRuntimeValue<long>(value));
    }
    // a 32-bit pointer is a Long: its own type, and an Int32 in storage.
    public VBLongPtrValue(int value) : this(false)
    {
        Handle = new ValueBindingHandle(new VBRuntimeValue<int>(value));
    }

    // widened from whichever integer the platform's pointer is stored as: unboxing an Int32 as an Int64 would throw.
    public long Value => Convert.ToInt64(RuntimeValue.BoxedValue, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The width of the pointer: 8 bytes for a 64-bit one, 4 for a 32-bit one, however the value was made.
    /// </summary>
    public override int Size => Is64Bit ? VBLongPtrType_x64.BitnessAwarePtrSize : VBLongPtrType_x86.BitnessAwarePtrSize;

    public bool Equals(IVBTypedValue<VBLongPtrValue, long>? other) => Value.Equals(other?.Value);
}
