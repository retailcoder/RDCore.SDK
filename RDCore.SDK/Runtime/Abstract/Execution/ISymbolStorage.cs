using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Runtime.Shared;
using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Runtime.Abstract.Execution;

/// <summary>
/// The storage a running session binds its symbols to: the binding currently associated with a symbol, or held at an address of the session's
/// <em>memory map</em>.
/// </summary>
/// <remarks>
/// Storage only: what a name binds to is <see cref="ISymbolResolver"/>'s, and a resolver of a running session is both
/// (<see cref="IRuntimeSymbolResolver"/>).
/// </remarks>
public interface ISymbolStorage
{
    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> currently associated with the specified <see cref="Symbol"/>.
    /// </summary>
    /// <param name="symbol">The <see cref="Symbol"/> to retrieve the currently associated binding for.</param>
    IBindingHandle GetValue(Symbol symbol);

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> at the specified address in the runtime <em>memory map</em>.
    /// </summary>
    /// <param name="address">The memory address to read.</param>
    /// <param name="value">The retrieved binding, if successful.</param>
    bool TryRead(MemoryAddress address, [NotNullWhen(true)][MaybeNullWhen(false)] out IBindingHandle? value);

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> of the place at the specified address in the runtime <em>memory map</em> that holds a value of
    /// <paramref name="type"/>: what is bound there, or a field or an element inside it that starts at the address
    /// (<see cref="ISessionStorage.TryRead(MemoryAddress, VBType?, out IBindingHandle?)"/>). This is how a reference parameter's binding is read
    /// (<strong>MS-VBAL §5.3.1.11</strong>): a <see cref="VBRuntimeReference"/> to a record's first field has the record's
    /// address.
    /// </summary>
    /// <param name="address">The memory address to read.</param>
    /// <param name="type">The type of the value the place holds; <see langword="null"/> reads what is bound at the address.</param>
    /// <param name="value">The retrieved binding, if successful.</param>
    /// <returns><see langword="false"/> when nothing is bound at the address and no place of <paramref name="type"/> starts there.</returns>
    /// <remarks>
    /// The default implementation reads what is bound at the address (<see cref="TryRead(MemoryAddress, out IBindingHandle?)"/>), whatever
    /// <paramref name="type"/> is.
    /// </remarks>
    bool TryRead(MemoryAddress address, VBType? type, [NotNullWhen(true)][MaybeNullWhen(false)] out IBindingHandle? value)
        => TryRead(address, out value);

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> of the variable <paramref name="reference"/> refers to, given to a reference parameter of
    /// <paramref name="parameterType"/> (<strong>MS-VBAL §5.3.1.11</strong>). The variable is of the type the reference declares
    /// (<see cref="VBRuntimeReference.DeclaredType"/>), whatever the parameter's type, and of the parameter's type when the reference declares none.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <param name="parameterType">
    /// The declared type of the parameter; <see langword="null"/>, with a reference that declares no type, reads what is bound at the address.
    /// </param>
    /// <param name="value">The variable's binding, if successful.</param>
    /// <returns><see langword="false"/> when nothing is bound at the reference's address and no place of the variable's type starts there.</returns>
    /// <remarks>
    /// Reads the place of the variable's type at the reference's address (<see cref="TryRead(MemoryAddress, VBType?, out IBindingHandle?)"/>).
    /// </remarks>
    bool TryRead(VBRuntimeReference reference, VBType? parameterType, [NotNullWhen(true)][MaybeNullWhen(false)] out IBindingHandle? value)
        => TryRead(reference.Value, reference.DeclaredType ?? parameterType, out value);

    /// <summary>
    /// Gets the <see cref="MemoryAddress"/> currently reserved for <paramref name="symbol"/>, if any:
    /// what a <c>ByRef</c> argument binds to (<strong>MS-VBAL §5.3.1.11</strong>: "a reference parameter
    /// binding... referring to the variable referenced by the argument's expression").
    /// </summary>
    /// <param name="symbol">The <see cref="Symbol"/> whose address to look up.</param>
    /// <param name="address">The reserved address, if <paramref name="symbol"/> is currently allocated.</param>
    bool TryGetAddress(Symbol symbol, out MemoryAddress address);

    /// <summary>
    /// Reserves storage sized for <paramref name="value"/> and binds it to <paramref name="symbol"/>, chiefly for
    /// a <c>Static</c> local's first-call allocation (<strong>MS-VBAL §5.4.3.1</strong>: module extent, so the
    /// storage outlives the local's frame). It does not affect name resolution: a symbol's name resolves through
    /// its declaring symbol (a procedure's <c>Locals</c> and <c>Parameters</c>). Calling it for a symbol that is
    /// already allocated frees the previous allocation and makes a new one.
    /// </summary>
    /// <param name="symbol">The symbol to reserve storage for.</param>
    /// <param name="value">The value to seed the new storage with.</param>
    /// <param name="address">The reserved address, on success.</param>
    /// <returns><c>false</c> if the underlying memory space is exhausted.</returns>
    bool TryAllocate(Symbol symbol, VBTypedValue value, out MemoryAddress address);
}
