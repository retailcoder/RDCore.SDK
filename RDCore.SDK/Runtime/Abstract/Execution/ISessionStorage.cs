using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Runtime.Shared;
using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Runtime.Abstract.Execution;

/// <summary>
/// Holds the actual value bound at each address in a session's memory space, as either a typed
/// <see cref="IBindingHandle"/> or, for the handful of MS-VBAL constructs that operate on raw storage
/// (<c>LSet</c> between two UDT variables; MS-VBAL only ever allows this and the already-value-level
/// fixed-length <c>String</c> form), a plain byte buffer. An address is one kind or the other, never
/// both.
/// </summary>
/// <remarks>
/// <see cref="ISessionMemoryAllocator"/> deliberately does not cover this: it only accounts for
/// allocation size and fragmentation, never values. An <see cref="ISessionStorage"/> depends on one
/// to reserve address space, then binds the caller's value at the resulting address.
/// <para>
/// The storage gives the session's objects their addresses (<see cref="IObjectPointers"/>).
/// </para>
/// <para>
/// ⚖️<strong>RDCore</strong> provides an implementation of this interface <strong>licensed under GPLv3</strong>.
/// </para>
/// </remarks>
public interface ISessionStorage : IObjectPointers
{
    /// <summary>
    /// Reserves <paramref name="size"/> bytes through the underlying <see cref="ISessionMemoryAllocator"/>
    /// and binds <paramref name="handle"/> at the resulting address. A non-positive <paramref name="size"/>
    /// (<c>Nothing</c>, <c>Null</c>, <c>Empty</c>, an uninitialized array, a UDT with no resolvable
    /// fields) never reaches the allocator at all — it gets an address of its own that can never collide
    /// with an allocated one, since nothing about it needs real memory.
    /// </summary>
    /// <returns><c>false</c> if the underlying memory space is exhausted.</returns>
    bool TryAllocate(int size, IBindingHandle handle, out MemoryAddress address);

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> bound at <paramref name="address"/>.
    /// </summary>
    bool TryRead(MemoryAddress address, [NotNullWhen(true)][MaybeNullWhen(false)] out IBindingHandle? handle);

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> of the place at <paramref name="address"/> that holds a value of <paramref name="type"/>: what is bound
    /// there, or a place inside it that starts at the address, such as a field of a record or an element of an array field of one. A reference
    /// parameter is bound to the variable its argument is, and a field or an element is a variable too (<strong>MS-VBAL §5.3.1.11</strong>). A record's first
    /// field starts at the record's own address; the type tells the two apart.
    /// </summary>
    /// <param name="address">The address of the place.</param>
    /// <param name="type">The type of the value the place holds; <see langword="null"/> reads what is bound at the address.</param>
    /// <param name="handle">The binding of the place, if successful.</param>
    /// <returns><see langword="false"/> when nothing is bound at the address and no place of <paramref name="type"/> starts there.</returns>
    /// <remarks>
    /// The default implementation reads what is bound at the address (<see cref="TryRead(MemoryAddress, out IBindingHandle?)"/>), whatever
    /// <paramref name="type"/> is.
    /// </remarks>
    bool TryRead(MemoryAddress address, VBType? type, [NotNullWhen(true)][MaybeNullWhen(false)] out IBindingHandle? handle)
        => TryRead(address, out handle);

    /// <summary>
    /// Frees the binding and the underlying storage at <paramref name="address"/>.
    /// </summary>
    /// <returns><c>true</c> if a binding existed at <paramref name="address"/> and was released.</returns>
    bool TryDeallocate(MemoryAddress address);

    /// <summary>
    /// Replaces the <see cref="IBindingHandle"/> currently bound at <paramref name="address"/>, e.g. to
    /// let a location-identified value (a UDT, an array) bind itself to the very address that was just
    /// reserved for it.
    /// </summary>
    /// <returns><c>true</c> if <paramref name="address"/> was already allocated and its binding was replaced.</returns>
    bool TryRebind(MemoryAddress address, IBindingHandle handle);

    /// <summary>
    /// Reserves <paramref name="size"/> bytes through the underlying <see cref="ISessionMemoryAllocator"/>
    /// as a raw byte buffer, zero-initialized, at the resulting address.
    /// </summary>
    /// <returns><c>false</c> if the underlying memory space is exhausted.</returns>
    bool TryAllocateBytes(int size, out MemoryAddress address);

    /// <summary>
    /// Gets a copy of the byte buffer at <paramref name="address"/>.
    /// </summary>
    /// <returns><c>false</c> if <paramref name="address"/> is not a byte-backed allocation.</returns>
    bool TryReadBytes(MemoryAddress address, [NotNullWhen(true)][MaybeNullWhen(false)] out byte[]? bytes);

    /// <summary>
    /// Replaces the byte buffer at <paramref name="address"/> with a copy of <paramref name="bytes"/>.
    /// The new buffer's length need not match the original allocation's.
    /// </summary>
    /// <returns><c>false</c> if <paramref name="address"/> is not a byte-backed allocation.</returns>
    bool TryWriteBytes(MemoryAddress address, ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Reads the single byte at <paramref name="address"/>, wherever it falls inside an allocation:
    /// a raw byte buffer, or the byte image of a bound value.
    /// </summary>
    /// <remarks>
    /// Direct, unchecked, byte-level access to the session's memory space: what a <c>PEEK</c> is.
    /// Nothing about the address needs to be the start of anything, or to mean anything.
    /// </remarks>
    /// <param name="address">Any address in the session's memory space.</param>
    /// <param name="value">The byte there.</param>
    /// <returns><c>false</c> if nothing is allocated at that address, or what is has no byte image.</returns>
    bool TryPeek(MemoryAddress address, out byte value);

    /// <summary>
    /// Writes a single byte at <paramref name="address"/>, wherever it falls inside an allocation.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="TryPeek"/>, and every bit as unchecked: writing one byte of a
    /// bound value's image changes that value to whatever the new image says, valid or not. That is
    /// what a <c>POKE</c> is for.
    /// </remarks>
    /// <param name="address">Any address in the session's memory space.</param>
    /// <param name="value">The byte to write.</param>
    /// <returns>
    /// <c>false</c> if nothing is allocated at that address, or what is has no byte image, or the
    /// mutated image is not a value of the same shape any more.
    /// </returns>
    bool TryPoke(MemoryAddress address, byte value);
}
