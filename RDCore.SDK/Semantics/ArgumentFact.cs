using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;

namespace RDCore.SDK.Semantics;

/// <summary>
/// An argument of a call, and the parameter it is mapped to (<strong>MS-VBAL §5.3.1.11</strong>): what the procedure called declares of the parameter, so
/// that what the argument is given to can be read without the procedure's declaration, which may be in another module or in a library.
/// </summary>
/// <remarks>
/// A fact is stated for each argument the static pass maps to a parameter of a procedure it knows: a call of a procedure of the project, of a library or of a
/// <c>Declare</c>, by its name, qualified, or as a member of an object of a known class. A call bound late, an index of an array, and an argument that maps to
/// no parameter have none.
/// </remarks>
/// <param name="Argument">
/// The argument, as the argument list writes it: a <c>ByVal</c> argument is the <c>ByVal</c> expression, and a named argument the value it names, not the
/// <c>name:=</c> around it.
/// </param>
/// <param name="Parameter">The address of the parameter.</param>
/// <param name="Mechanism">
/// How the parameter is declared to take its argument (<strong>MS-VBAL §5.3.1.5</strong>): <c>ByVal</c> (<see cref="ParameterKind.ExplicitByVal"/>),
/// <c>ByRef</c> (<see cref="ParameterKind.ExplicitByRef"/>), or neither. A parameter declared with neither is passed by reference
/// (<see cref="ParameterKind.ImplicitByRef"/>), except the value parameter of a <c>Property Let</c> or <c>Property Set</c>, which is passed by value
/// (<see cref="ParameterKind.ImplicitByVal"/>). A parameter of a library's procedure is <see cref="ParameterKind.ExplicitByRef"/> where the type library
/// declares it by reference, and <see cref="ParameterKind.ExplicitByVal"/> otherwise.
/// </param>
/// <param name="ParameterType">
/// The name of the parameter's declared type, as <see cref="Platform.Protocol.ExpressionFactDto.DeclaredType"/> names a type; the name of the unknown type for a parameter declared
/// <c>As Any</c>, and for one whose type did not resolve.
/// </param>
/// <param name="IsOptional">Whether the parameter is declared <c>Optional</c>.</param>
/// <param name="HasDefaultValue">Whether the parameter is declared with a default value (<c>Optional n As Long = 1</c>).</param>
/// <param name="IsParamArray">Whether the parameter is a <c>ParamArray</c>, which takes this argument and every one after it.</param>
/// <param name="IsExternal">Whether the procedure called is declared by a <c>Declare</c> statement (<strong>MS-VBAL §5.2.3.5</strong>).</param>
/// <param name="IsLibrary">
/// Whether the procedure called is a member of a library: the standard library, or a library the project references. A <c>Declare</c>d procedure is
/// declared in the project, and is not one.
/// </param>
public sealed record class ArgumentFact(
    SyntaxNodeId Argument,
    Uri Parameter,
    ParameterKind Mechanism,
    string ParameterType,
    bool IsOptional,
    bool HasDefaultValue,
    bool IsParamArray,
    bool IsExternal,
    bool IsLibrary);
