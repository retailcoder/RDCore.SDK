using RDCore.SDK.Model.Symbols.Abstract;

namespace RDCore.SDK.Semantics;

/// <summary>
/// What a member of a class module is bound to by its role: the event it handles and the object that raises it
/// (<strong>MS-VBAL §5.3.1.8</strong> Event Handler Declarations, <strong>§5.3.1.10</strong> Lifecycle Handler Declarations), or the interface member it
/// implements and the interface (<strong>§5.3.1.9</strong> Implemented Name Declarations).
/// </summary>
/// <remarks>
/// The name of a bound member is the name of its <paramref name="Source"/>'s part, an underscore, and the name of <paramref name="Bound"/>: the source and
/// the event or interface member name it, so renaming either renames the member's part of that name, and nothing that is not bound to them. A member whose
/// name only looks like one (<c>Form_Foo</c> in a class that is not a form, <c>watched_Spun</c> where the class of <c>watched</c> has no <c>Spun</c> event) has
/// no binding.
/// </remarks>
/// <param name="Member">The identity of the member: the handler, or the implementation.</param>
/// <param name="Role">
/// Its role: <see cref="DeclarationRole.EventHandler"/>, <see cref="DeclarationRole.UnmatchedEventHandler"/> or
/// <see cref="DeclarationRole.InterfaceImplementation"/>.
/// </param>
/// <param name="Source">
/// The identity of what the part of its name before the underscore names: the <c>WithEvents</c> variable or the form's control whose event it handles, the
/// class module itself for its own <c>Initialize</c> and <c>Terminate</c>, the class a form's designer hosts for the form's own events (<c>UserForm</c>,
/// <c>Form</c>), the host class of a document module, or the interface the class implements.
/// </param>
/// <param name="Bound">
/// The identity of what the part of its name after the underscore names: the event it handles, or the member of the interface it implements.
/// <see langword="null"/> for an <see cref="DeclarationRole.UnmatchedEventHandler"/>, which is named for an event of its source without matching it (its
/// parameter list is not the event's), so that the event never calls it and it is bound to none.
/// </param>
public sealed record class DeclarationBinding(SemanticId Member, DeclarationRole Role, SemanticId Source, SemanticId? Bound);
