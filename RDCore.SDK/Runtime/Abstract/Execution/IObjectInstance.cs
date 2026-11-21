using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Runtime;
using RDCore.SDK.Runtime.Shared;
using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Runtime.Abstract.Execution;

/// <summary>
/// Represents a single live object instance: a class module's activation record, holding every
/// instance field it declares (<strong>RD-VBAL §2.3.1.2</strong>'s instance heap tier). Two
/// instances of the same class module never share a field's storage: each gets its own independent
/// binding, exactly as two activations of the same procedure never share a local's storage on
/// <see cref="ICallStackFrame"/>.
/// </summary>
/// <remarks>
/// ⚠️ An <c>IObjectInstance</c> is <strong>not immutable</strong>: the value retrieved for a given
/// field may be different at a subsequent retrieval. Its lifetime is tracked separately, by
/// <see cref="ISessionObjects"/> and the <see cref="VBRuntimeObjectId"/> it was created for.
/// </remarks>
public interface IObjectInstance
{
    /// <summary>
    /// The identity <see cref="ISessionObjects"/> tracks this instance's lifetime under.
    /// </summary>
    VBRuntimeObjectId ObjectId { get; }

    /// <summary>
    /// The <see cref="VBClassModuleSymbol"/> this is an instance of.
    /// </summary>
    VBClassModuleSymbol ClassModule { get; }

    /// <summary>
    /// The fields this instance holds, in the order it was given them: those its class declared when the instance was created
    /// (<see cref="ISessionSymbols.CreateInstance"/>), then those <see cref="Push"/> declared on it since. A field removed from the class while the
    /// instance lives is still one of them, and holds what it was set to until the instance is released.
    /// </summary>
    IReadOnlyList<Symbol> Fields { get; }

    /// <summary>
    /// Declares <paramref name="field"/> on this instance, holding <paramref name="value"/>, its initial value. The field is given an address when one is
    /// asked for (<see cref="TryGetAddress"/>).
    /// </summary>
    /// <param name="field">
    /// A field the class was given while the instance lives (an edit made while the program waits, <see cref="ISessionSymbols.ExtendLiveInstances"/>); or
    /// a <c>Static</c> local of one of its members, which the first call of the member on this instance declares: as in <strong>MS-VBA</strong>, each
    /// object has its own, where <strong>MS-VBAL §5.4.3.1</strong> gives a <c>Static</c> local module extent. The fields the class declares when the
    /// instance is created are given it by <see cref="ISessionSymbols.CreateInstance"/>.
    /// </param>
    /// <param name="value">The initial value: the declared type's default.</param>
    /// <exception cref="InvalidOperationException"><paramref name="field"/> is already declared on this instance.</exception>
    void Push(Symbol field, VBTypedValue value);

    /// <summary>
    /// The <c>Static</c> locals of the members of the class that this instance holds: the ones of the members called on it so far, which go with it, and
    /// let go of the objects they hold when it does.
    /// </summary>
    IReadOnlyCollection<Symbol> StaticLocals { get; }

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> this instance holds <paramref name="field"/> in, as <see cref="TryResolve(Symbol, out IBindingHandle?)"/>
    /// does.
    /// </summary>
    /// <param name="field">The field, or a <c>Static</c> local this instance holds.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="KeyNotFoundException">This instance holds no such field.</exception>
    IBindingHandle GetValue(Symbol field);

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> this instance holds <paramref name="field"/> in. A field holds its initial value until its binding is
    /// asked for: the binding is made then, of that value, and is the same from then on.
    /// </summary>
    /// <param name="field">The field, or a <c>Static</c> local this instance holds.</param>
    /// <param name="value">The binding; <see langword="null"/> when this instance holds no such field.</param>
    /// <returns>Whether this instance holds <paramref name="field"/>.</returns>
    bool TryResolve(Symbol field, [NotNullWhen(true)][MaybeNullWhen(false)] out IBindingHandle? value);

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> of the field at index <paramref name="field"/> of <see cref="Fields"/>, as
    /// <see cref="TryResolve(Symbol, out IBindingHandle?)"/> does, without looking the field up by its symbol.
    /// </summary>
    /// <param name="field">The field's index in <see cref="Fields"/>.</param>
    /// <param name="value">The binding; <see langword="null"/> when <paramref name="field"/> is not an index in <see cref="Fields"/>.</param>
    /// <returns>Whether <paramref name="field"/> is an index in <see cref="Fields"/>.</returns>
    bool TryResolve(int field, [NotNullWhen(true)][MaybeNullWhen(false)] out IBindingHandle? value);

    /// <summary>
    /// Gets the <see cref="IBindingHandle"/> this instance holds <paramref name="field"/> in, when one was made: none for a field that holds its
    /// initial value, whose binding nothing has asked for (<see cref="TryResolve(Symbol, out IBindingHandle?)"/>, <see cref="GetValue"/>,
    /// <see cref="TryGetAddress"/>). A field with no binding holds no object: its initial value holds none.
    /// </summary>
    /// <param name="field">The field, or a <c>Static</c> local this instance holds.</param>
    /// <param name="value">The binding; <see langword="null"/> when none was made.</param>
    /// <returns>Whether a binding was made for <paramref name="field"/>.</returns>
    bool TryGetBinding(Symbol field, [NotNullWhen(true)][MaybeNullWhen(false)] out IBindingHandle? value);

    /// <summary>
    /// Gets the address of the specified instance field <see cref="Symbol"/>: given the first time one is asked for, and the same for as long as the
    /// instance lives. It is what <c>VarPtr</c> of the field is, and what a <c>ByRef</c> argument that names the field is aliased to.
    /// </summary>
    /// <param name="field">The field, or a <c>Static</c> local this instance holds.</param>
    /// <param name="address">The address.</param>
    /// <returns><see langword="false"/> when this instance holds no such field, or the session's memory is exhausted.</returns>
    bool TryGetAddress(Symbol field, out MemoryAddress address);
}
