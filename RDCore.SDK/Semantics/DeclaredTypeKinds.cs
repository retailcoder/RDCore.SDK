using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;

namespace RDCore.SDK.Semantics;

/// <summary>
/// The <see cref="DeclaredTypeKind"/> of a declared type.
/// </summary>
public static class DeclaredTypeKinds
{
    /// <summary>
    /// The kind of a declared type, and whether it is an array: for an array, the kind of its elements' type (<strong>MS-VBAL §2.1</strong>).
    /// </summary>
    /// <param name="type">The declared type of a declaration or of an expression.</param>
    /// <returns>
    /// The kind, and whether the type is an array. The kind is <see langword="null"/> for no type, the unknown type, and a type no value has: that of a
    /// module or a project named as a qualifier, or of a call of a <c>Sub</c>. The <c>Empty</c> and <c>Null</c> literals are
    /// <see cref="DeclaredTypeKind.Variant"/>, their declared type (<strong>MS-VBAL §3.3.5.2</strong>).
    /// </returns>
    public static (DeclaredTypeKind? Kind, bool IsArray) Of(VBType? type) => type switch
    {
        VBArrayType array => (Of(array.ItemType).Kind, true),
        null or VBUnknownType or VBStdModuleType => (null, false),
        VBVariantType or VBEmptyType or VBNullType => (DeclaredTypeKind.Variant, false),
        VBClassType => (DeclaredTypeKind.Class, false),
        VBObjectType => (DeclaredTypeKind.Object, false),
        VBUserDefinedType => (DeclaredTypeKind.UserDefinedType, false),
        VBEnumType => (DeclaredTypeKind.Enum, false),
        VBIntrinsicType => (DeclaredTypeKind.Intrinsic, false),
        _ => (null, false),
    };
}
