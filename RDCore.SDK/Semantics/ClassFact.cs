using RDCore.SDK.Model.Symbols.Abstract;

namespace RDCore.SDK.Semantics;

/// <summary>
/// A class whose objects a module creates, with a <c>New</c> expression (<strong>MS-VBAL §5.6.8</strong>) or a variable declared <c>As New</c>
/// (<strong>MS-VBAL §5.2.3.1.4</strong>): what is known of creating its objects, so that it can be read without the class's declaration, which may be in
/// another module or in a library.
/// </summary>
/// <param name="Class">The address of the class.</param>
/// <param name="Name">The name of the class.</param>
/// <param name="IsCreatable">
/// Whether <c>New</c> of the class is valid outside its project: the <c>VB_Creatable</c> attribute of a class module, or, for a library's class, what
/// its library declares (<see cref="SymbolProperties.Creatable"/>).
/// </param>
/// <param name="IsRegistered">
/// Whether anything creates the class's objects when <c>New</c> asks for one. <see langword="false"/> for a class its library declares creatable which
/// nothing implements (<see cref="SymbolProperties.Unregistered"/>): <strong>MS-VBA</strong> compiles <c>New ErrObject</c>, and creating the object raises
/// run-time error -2147221164 <em>Class not registered</em> (recorded in Excel). <see langword="true"/> for every class of the project.
/// </param>
public sealed record class ClassFact(Uri Class, string Name, bool IsCreatable, bool IsRegistered);
