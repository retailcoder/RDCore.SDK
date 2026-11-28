using RDCore.SDK.Model;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;
using System.Collections.Immutable;

namespace RDCore.SDK.Semantics;

/// <summary>
/// A class of the workspace that a declaration's type names, and the members of its public interface (<see cref="DeclarationFact.DeclaredClasses"/>).
/// </summary>
/// <param name="Class">The address of the class module.</param>
/// <param name="Name">The name of the class.</param>
/// <param name="Members">
/// The members of the class that code outside its project can name, in the order the class declares them: each <c>Public</c> variable, each procedure that
/// is <c>Public</c> or declared without an access modifier, and the accessors of a property as one member (<strong>MS-VBAL §5.2.3.1</strong>,
/// <strong>§5.3.1.1</strong>). Its events are not members of it here.
/// </param>
public sealed record class DeclaredClassFact(Uri Class, string Name, ImmutableArray<ClassMemberFact> Members)
{
    /// <summary>
    /// The class of the workspace a declared type names, then each class of the workspace that the type of one of their members names, at any depth, each
    /// class once.
    /// </summary>
    /// <param name="declaredType">The declared type of a declaration: a class, or an array whose elements are of a class.</param>
    /// <param name="classModuleOf">
    /// The class module of the workspace that declares a class type, with its members as the binding has them; <see langword="null"/> for a class that no
    /// module of the workspace declares.
    /// </param>
    /// <returns>
    /// The classes, the one the declared type names first; empty when the declared type is not a class of the workspace, nor an array of one. A class that
    /// a library declares is not one: <see cref="SymbolProperties.Library"/> is stated on its symbol.
    /// </returns>
    public static ImmutableArray<DeclaredClassFact> Of(VBType? declaredType, Func<VBClassType, VBClassModuleSymbol?> classModuleOf)
    {
        var classes = ImmutableArray.CreateBuilder<DeclaredClassFact>();
        // Uri.Equals ignores the fragment, which is where a module's address is: classes are told apart by the full string.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<VBClassModuleSymbol>();
        Enqueue(declaredType);
        while (pending.TryDequeue(out var classModule))
        {
            var members = MembersOf(classModule, classModuleOf).ToList();
            classes.Add(new DeclaredClassFact(classModule.Uri, classModule.Name, [.. members.Select(member => member.Fact)]));
            foreach (var (_, type) in members)
            {
                Enqueue(type);
            }
        }

        return classes.ToImmutable();

        void Enqueue(VBType? type)
        {
            if (WorkspaceClassOf(type, classModuleOf) is { } found && seen.Add(found.Uri.AbsoluteUri))
            {
                pending.Enqueue(found);
            }
        }
    }

    private static VBClassModuleSymbol? WorkspaceClassOf(VBType? type, Func<VBClassType, VBClassModuleSymbol?> classModuleOf)
        => ElementTypeOf(type) is VBClassType classType && classModuleOf(classType) is { } classModule
            && classModule.GetProperty(SymbolProperties.Library) is null
                ? classModule
                : null;

    private static VBType? ElementTypeOf(VBType? type) => type is VBArrayType array ? array.ItemType : type;

    // a variable is a field of its module, whichever symbol declares it.
    private static bool IsVariable(VBTypeMemberSymbol member) => member.Kind is SymbolKindExt.Field or SymbolKindExt.Variable;

    private static bool IsPublic(VBTypeMemberSymbol member)
        => IsVariable(member)
            ? member.AccessModifier is AccessModifier.Public or AccessModifier.Global
            : member.AccessModifier is AccessModifier.Public or AccessModifier.Implicit;

    private static bool IsMember(VBTypeMemberSymbol member)
        => IsVariable(member) || member is IVBPropertyMemberSymbol or VBFunctionMemberSymbol or VBProcedureMemberSymbol;

    // the parameters a call gives: all of them but the Me every member of a class has.
    private static VBParameterSymbol[] Given(VBTypeMemberSymbol member)
        => [.. (member switch
        {
            VBReturningMemberSymbol returning => returning.Parameters,
            VBProcedureMemberSymbol procedure => procedure.Parameters,
            _ => [],
        }).Where(parameter => !(parameter.Name == Tokens.Me && parameter.ParameterKind == ParameterKind.ImplicitByRef))];

    private static IEnumerable<IGrouping<string, VBTypeMemberSymbol>> PublicMembersOf(VBClassModuleSymbol classModule)
        => classModule.DefaultInterfaceMembers
            .Where(member => IsMember(member) && IsPublic(member))
            .OrderBy(member => member.Range.Start.Line).ThenBy(member => member.Range.Start.Character)
            .GroupBy(member => member.Name, StringComparer.OrdinalIgnoreCase);

    // MS-VBAL §5.3.1.7: what a property is read as is its Property Get's type; a property with no Get is given values of its Let's or Set's last parameter.
    private static VBType? TypeOf(IGrouping<string, VBTypeMemberSymbol> member)
        => member.OfType<VBPropertyGetMemberSymbol>().FirstOrDefault() is { } get ? get.ResolvedType
            : member.First() is IVBPropertyMemberSymbol ? Given(member.First()).LastOrDefault()?.ResolvedType
            : member.First().ResolvedType;

    private static IEnumerable<(ClassMemberFact Fact, VBType? Type)> MembersOf(
        VBClassModuleSymbol classModule, Func<VBClassType, VBClassModuleSymbol?> classModuleOf)
    {
        foreach (var member in PublicMembersOf(classModule))
        {
            var first = member.First();
            var accessors = member.Aggregate(PropertyAccessors.None, (all, accessor) => all | accessor switch
            {
                VBPropertyGetMemberSymbol => PropertyAccessors.Get,
                VBPropertyLetMemberSymbol => PropertyAccessors.Let,
                VBPropertySetMemberSymbol => PropertyAccessors.Set,
                _ => PropertyAccessors.None,
            });
            var classification = IsVariable(first) ? ExpressionClassification.Variable
                : accessors is not PropertyAccessors.None ? ExpressionClassification.Property
                : first is VBFunctionMemberSymbol ? ExpressionClassification.Function
                : ExpressionClassification.Subroutine;
            var get = member.OfType<VBPropertyGetMemberSymbol>().FirstOrDefault();
            // a Let or a Set is given the value besides its arguments: its last parameter.
            var parameters = get is not null || accessors is PropertyAccessors.None ? Given(get ?? first).Length : Math.Max(0, Given(first).Length - 1);
            var type = classification is ExpressionClassification.Subroutine ? null : TypeOf(member);
            var (typeKind, isArray) = DeclaredTypeKinds.Of(type);
            var element = ElementTypeOf(type);
            yield return (new ClassMemberFact(first.Name, classification, accessors, parameters, typeKind, isArray, typeKind is null ? null : element!.Name)
            {
                TypeClass = WorkspaceClassOf(type, classModuleOf)?.Uri,
                TypeLibrary = element is VBClassType { Symbol: var typeModule } && typeModule.GetProperty(SymbolProperties.Library) is { Length: > 0 } library
                    ? library
                    : null,
            }, type);
        }
    }
}

/// <summary>
/// A member of the public interface of a class of the workspace (<see cref="DeclaredClassFact.Members"/>): a variable, a property, or a method.
/// </summary>
/// <param name="Name">The name it is declared with: that of the first of its accessors the class declares, for a property.</param>
/// <param name="Classification">
/// What it is: a <see cref="ExpressionClassification.Variable"/> for a <c>Public</c> variable, a <see cref="ExpressionClassification.Property"/>, or a
/// method, a <see cref="ExpressionClassification.Function"/> or a <see cref="ExpressionClassification.Subroutine"/>.
/// </param>
/// <param name="Accessors">The accessors the class declares for it, for a property; <see cref="PropertyAccessors.None"/> for any other member.</param>
/// <param name="Parameters">
/// How many parameters it declares, besides the value a <c>Property Let</c> or <c>Property Set</c> is given (<strong>MS-VBAL §5.3.1.7</strong>): those of its
/// <c>Property Get</c>, for a property that has one. 0 for a variable.
/// </param>
/// <param name="TypeKind">
/// The kind of its declared type, or of its elements' type for an array (<see cref="DeclaredTypeKinds.Of"/>): a variable's, what a function returns, and
/// what a property is read as, or given for a property with no <c>Get</c>. <see langword="null"/> for a <c>Sub</c>, and for a type that did not resolve.
/// </param>
/// <param name="IsArray">Whether its declared type is an array.</param>
/// <param name="TypeName">
/// The name of its declared type, or of its elements' type for an array: <c>Long</c>, <c>Variant</c>, <c>Customer</c>, <c>Dictionary</c>.
/// <see langword="null"/> exactly when <paramref name="TypeKind"/> is.
/// </param>
public sealed record class ClassMemberFact(
    string Name, ExpressionClassification Classification, PropertyAccessors Accessors, int Parameters, DeclaredTypeKind? TypeKind, bool IsArray,
    string? TypeName)
{
    /// <summary>
    /// The address of the class module of the workspace that its declared type names, or its elements' type for an array: a
    /// <see cref="DeclaredClassFact"/> of the same declaration describes that class. <see langword="null"/> for any other type.
    /// </summary>
    public Uri? TypeClass { get; init; }

    /// <summary>
    /// The library that declares the class its declared type names, or its elements' type for an array: <c>Scripting</c> for a <c>Dictionary</c>.
    /// <see langword="null"/> for a class of the workspace, and for any type that is not a class.
    /// </summary>
    public string? TypeLibrary { get; init; }
}
