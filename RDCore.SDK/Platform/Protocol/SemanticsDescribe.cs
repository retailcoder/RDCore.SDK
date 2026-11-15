using MediatR;
using OmniSharp.Extensions.JsonRpc;
using RDCore.SDK.Client;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Values;
using RDCore.SDK.Semantics;
using RDCore.SDK.Semantics.Flags;
using System.Text.Json.Serialization;
using ExpressionClassification = RDCore.SDK.Semantics.ExpressionClassification;
using NewtonsoftConverter = Newtonsoft.Json.JsonConverterAttribute;

namespace RDCore.SDK.Platform.Protocol;

// rdcore/semantics/describe and /outline: what hover and the document outline know, as data. Positions count as LSP counts them (0-based); the assistant
// tools answer the same facts with 1-based positions (RDCore.SDK.Assistant). The answers name their enumerations by name, which only System.Text.Json
// writes: each answer is written through it (SystemTextJsonBridge).

/// <summary>
/// <see cref="SemanticsDescribeParams"/> as a request of <c>rdcore/semantics/describe</c>, answered with a <see cref="DescribeResult"/>.
/// </summary>
[Method(RDCorePlatformProtocol.SemanticsDescribe, Direction.ClientToServer)]
public sealed record class SemanticsDescribeRequest : SemanticsDescribeParams, IRequest, IRequest<DescribeResult>;

/// <summary>
/// <see cref="SemanticsOutlineParams"/> as a request of <c>rdcore/semantics/outline</c>, answered with a <see cref="ModuleOutline"/>.
/// </summary>
[Method(RDCorePlatformProtocol.SemanticsOutline, Direction.ClientToServer)]
public sealed record class SemanticsOutlineRequest : SemanticsOutlineParams, IRequest, IRequest<ModuleOutline>;

/// <summary>
/// What a symbol is: a declaration of the workspace or of a library.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DescribedSymbolKind>))]
public enum DescribedSymbolKind
{
    /// <summary>
    /// A standard module, or a document module.
    /// </summary>
    Module,

    /// <summary>
    /// A class: a class module, or a class of a library.
    /// </summary>
    Class,

    /// <summary>
    /// A <c>Sub</c> or <c>Function</c> procedure, a <c>Declare</c>d one included.
    /// </summary>
    Procedure,

    /// <summary>
    /// A property: its accessors are one symbol.
    /// </summary>
    Property,

    /// <summary>
    /// An event a class declares.
    /// </summary>
    Event,

    /// <summary>
    /// A variable, of a module or of a procedure.
    /// </summary>
    Variable,

    /// <summary>
    /// A parameter of a procedure, a property or an event.
    /// </summary>
    Parameter,

    /// <summary>
    /// A constant, of a module or of a procedure.
    /// </summary>
    Constant,

    /// <summary>
    /// A user-defined type (<c>Type</c>).
    /// </summary>
    Type,

    /// <summary>
    /// A member of a user-defined type.
    /// </summary>
    TypeMember,

    /// <summary>
    /// An enumeration (<c>Enum</c>).
    /// </summary>
    Enum,

    /// <summary>
    /// A member of an enumeration.
    /// </summary>
    EnumMember,

    /// <summary>
    /// A control of a form.
    /// </summary>
    Control,
}

/// <summary>
/// Where a symbol is declared, which says where its name can be written.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DescribedScope>))]
public enum DescribedScope
{
    /// <summary>
    /// In a procedure: a local variable or constant, or a parameter.
    /// </summary>
    Procedure,

    /// <summary>
    /// In a module: a member of the module, or the module itself. Its access says whether other modules see it.
    /// </summary>
    Module,

    /// <summary>
    /// In a library the project references.
    /// </summary>
    Library,
}

/// <summary>
/// The answer to <c>rdcore/semantics/describe</c>: the symbol the name at the position binds to, and the facts of the expression the position is in.
/// </summary>
/// <remarks>
/// Both are <see langword="null"/> where nothing is written at the position, or where what is written there is not known.
/// </remarks>
[NewtonsoftConverter(typeof(SystemTextJsonBridge<DescribeResult>))]
public sealed record class DescribeResult
{
    /// <summary>
    /// The symbol the name at the position binds to; <see langword="null"/> when no name is there, or it binds to nothing known.
    /// </summary>
    public SymbolDescription? Symbol { get; init; }

    /// <summary>
    /// The facts of the innermost expression whose location holds the position; <see langword="null"/> when the host has none.
    /// </summary>
    public ExpressionDescription? Expression { get; init; }

    /// <summary>
    /// The name or expression the position is in; <see langword="null"/> when nothing is described.
    /// </summary>
    public SourceRange? Range { get; init; }
}

/// <summary>
/// What is known of a symbol, wherever its declaration is written: the facts <see cref="SymbolDescription"/> and the assistant tools' description share.
/// </summary>
/// <remarks>
/// The facts of a declaration's use (<see cref="IsImplicit"/>, <see cref="Role"/>, <see cref="References"/>, <see cref="Hides"/>,
/// <see cref="AutoInstantiates"/>) are known where the environment host has the module: a <see langword="null"/> says they are not known, never that
/// there are none.
/// </remarks>
public abstract record class DescribedSymbol
{
    /// <summary>
    /// The address of the symbol (its <c>SemanticId</c>).
    /// </summary>
    public required Uri Id { get; init; }

    /// <summary>
    /// The name it is declared with.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What it is.
    /// </summary>
    public DescribedSymbolKind Kind { get; init; }

    /// <summary>
    /// Its declaration as hover writes it, such as <c>Public Function F(ByVal x As Long) As String</c>.
    /// </summary>
    public required string Declaration { get; init; }

    /// <summary>
    /// What declares it, such as <c>Module1</c> or <c>VBA.Interaction</c>; <see langword="null"/> for a module.
    /// </summary>
    public string? Container { get; init; }

    /// <summary>
    /// The library that declares it; <see langword="null"/> for a symbol of the workspace. For a <c>Declare</c>d procedure, the library of its module,
    /// not its <c>Lib</c>.
    /// </summary>
    public string? Library { get; init; }

    /// <summary>
    /// The name of its declared type; <see langword="null"/> when it has none, or one that did not resolve.
    /// </summary>
    public string? DeclaredType { get; init; }

    /// <summary>
    /// The kind of its declared type, or of its elements' type for an array (<see cref="DeclaredTypeKinds.Of"/>); <see langword="null"/> when it has no
    /// type, or one that did not resolve.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DeclaredTypeKind>))]
    public DeclaredTypeKind? TypeKind { get; init; }

    /// <summary>
    /// Whether its declared type is an array.
    /// </summary>
    public bool IsArray { get; init; }

    /// <summary>
    /// The access it is declared with.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AccessModifier>))]
    public AccessModifier Access { get; init; }

    /// <summary>
    /// Where it is declared.
    /// </summary>
    public DescribedScope Scope { get; init; }

    /// <summary>
    /// Whether it was never declared: it came into being because code referred to it (<see cref="DeclarationFact.IsImplicit"/>); <see langword="null"/>
    /// when not known.
    /// </summary>
    public bool? IsImplicit { get; init; }

    /// <summary>
    /// The part it plays in its module (<see cref="DeclarationFact.Role"/>); <see langword="null"/> when not known.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DeclarationRole>))]
    public DeclarationRole? Role { get; init; }

    /// <summary>
    /// How many times the project's code reads it, writes it and passes it as an argument (<see cref="DeclarationFact.References"/>);
    /// <see langword="null"/> when not known.
    /// </summary>
    public DeclarationReferences? References { get; init; }

    /// <summary>
    /// The qualified name of what it hides (<see cref="HiddenDeclaration.QualifiedName"/>); <see langword="null"/> when it hides nothing, or when not
    /// known.
    /// </summary>
    public string? Hides { get; init; }

    /// <summary>
    /// The name of the class whose object it is given where code first uses it, for a variable declared <c>As New</c>
    /// (<see cref="DeclarationFact.AutoInstantiates"/>); <see langword="null"/> for any other symbol, or when not known.
    /// </summary>
    public string? AutoInstantiates { get; init; }

    /// <summary>
    /// The text that documents it (a library member's help string); <see langword="null"/> when it has none.
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// The name of the control array it is an element of, for a control of a VB6 form; <see langword="null"/> otherwise.
    /// </summary>
    public string? ControlArrayOf { get; init; }
}

/// <summary>
/// The symbol a name binds to, as <c>rdcore/semantics/describe</c> answers it.
/// </summary>
public sealed record class SymbolDescription : DescribedSymbol
{
    /// <summary>
    /// The name its declaration declares; <see langword="null"/> for a symbol of a library, which no document declares.
    /// </summary>
    public SourceLocation? Location { get; init; }
}

/// <summary>
/// The compile error of an expression, as it is described.
/// </summary>
public sealed record class DescribedError
{
    /// <summary>
    /// The code of the error, such as <c>RDC00101</c>.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// What the error is.
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// What is known of an expression, wherever it is written: the facts <see cref="ExpressionDescription"/> and the assistant tools' description share.
/// </summary>
public abstract record class DescribedExpression
{
    /// <summary>
    /// What the expression names (<see cref="ExpressionFact.Classification"/>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ExpressionClassification>))]
    public ExpressionClassification Classification { get; init; }

    /// <summary>
    /// The name of its declared type; <see langword="null"/> when it is an error.
    /// </summary>
    public string? DeclaredType { get; init; }

    /// <summary>
    /// The kind of its declared type, or of its elements' type for an array (<see cref="DeclaredTypeKinds.Of"/>); <see langword="null"/> when it has no
    /// type a value can have, or one that did not resolve.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DeclaredTypeKind>))]
    public DeclaredTypeKind? TypeKind { get; init; }

    /// <summary>
    /// Whether its declared type is an array.
    /// </summary>
    public bool IsArray { get; init; }

    /// <summary>
    /// The address of the symbol it refers to; <see langword="null"/> when it resolved to none.
    /// </summary>
    public Uri? Binding { get; init; }

    /// <summary>
    /// The name of each <see cref="ValueExpressionSemanticFlags"/> flag that is set, in the order the enumeration declares them.
    /// </summary>
    public IReadOnlyList<string> Flags { get; init; } = [];

    /// <summary>
    /// The value of the constant it names (<see cref="ExpressionFact.ConstantValue"/>); <see langword="null"/> when it names none, or the value is not
    /// known.
    /// </summary>
    public RunValue? ConstantValue { get; init; }

    /// <summary>
    /// The name of each accessor of the property it names (<see cref="PropertyAccessors"/>: <c>Get</c>, <c>Let</c>, <c>Set</c>); empty when it names no
    /// property.
    /// </summary>
    public IReadOnlyList<string> Accessors { get; init; } = [];

    /// <summary>
    /// Whether its value is taken and Let-coerced to a declared type (<see cref="ExpressionFact.IsLetCoerced"/>).
    /// </summary>
    public bool IsLetCoerced { get; init; }

    /// <summary>
    /// The name of the default member of the class its declared type is (<see cref="DefaultMemberFact.Name"/>); <see langword="null"/> otherwise.
    /// </summary>
    public string? DefaultMember { get; init; }

    /// <summary>
    /// The name of the class of the object it creates by name (<see cref="ObjectCreationFact.ClassName"/>); <see langword="null"/> when it creates
    /// none, or the class is not known.
    /// </summary>
    public string? Creates { get; init; }

    /// <summary>
    /// Its compile error; <see langword="null"/> when it has none.
    /// </summary>
    public DescribedError? Error { get; init; }
}

/// <summary>
/// The facts of an expression, as <c>rdcore/semantics/describe</c> answers them.
/// </summary>
public sealed record class ExpressionDescription : DescribedExpression
{
    /// <summary>
    /// Where the expression is written.
    /// </summary>
    public SourceRange Range { get; init; }

    /// <summary>
    /// Describes the fact the environment host has of an expression.
    /// </summary>
    /// <param name="fact">The fact, as it travels from the host; its constant value is written as a <see cref="RunValue"/> (<see cref="RunValues.ToRunValue"/>).</param>
    public static ExpressionDescription Of(ExpressionFactDto fact) => new()
    {
        Range = fact.Location.Range,
        Classification = fact.Classification,
        DeclaredType = fact.DeclaredType,
        TypeKind = fact.TypeKind,
        IsArray = fact.IsArray,
        Binding = fact.Binding,
        Flags = FlagNames.Of(fact.Flags),
        ConstantValue = fact.ConstantValue?.ToRunValue(),
        Accessors = FlagNames.Of(fact.Accessors),
        IsLetCoerced = fact.IsLetCoerced,
        DefaultMember = fact.DefaultMember?.Name,
        Creates = fact.Creates?.ClassName,
        Error = fact.Error is { } error ? new DescribedError { Code = error.Code.ToString(), Description = error.Description } : null,
    };
}

/// <summary>
/// What is known of a declaration of a module's outline, wherever it is written: the facts <see cref="OutlineEntry"/> and the assistant tools' entry share.
/// </summary>
public abstract record class OutlinedDeclaration
{
    /// <summary>
    /// The name it is declared with.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What it is.
    /// </summary>
    public DescribedSymbolKind Kind { get; init; }

    /// <summary>
    /// The address of its symbol (its <c>SemanticId</c>).
    /// </summary>
    public required Uri Id { get; init; }

    /// <summary>
    /// Its declaration as hover writes it.
    /// </summary>
    public required string Declaration { get; init; }

    /// <summary>
    /// The access it is declared with.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AccessModifier>))]
    public AccessModifier Access { get; init; }

    /// <summary>
    /// The name of its declared type; <see langword="null"/> when it has none, or one that did not resolve.
    /// </summary>
    public string? DeclaredType { get; init; }

    /// <summary>
    /// The kind of its declared type, or of its elements' type for an array; <see langword="null"/> when it has no type, or one that did not resolve.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<DeclaredTypeKind>))]
    public DeclaredTypeKind? TypeKind { get; init; }

    /// <summary>
    /// Whether its declared type is an array.
    /// </summary>
    public bool IsArray { get; init; }

    /// <summary>
    /// The name of each accessor a property has (<c>Get</c>, <c>Let</c>, <c>Set</c>), which the outline lists as one entry; empty for any other
    /// declaration.
    /// </summary>
    public IReadOnlyList<string> Accessors { get; init; } = [];
}

/// <summary>
/// A declaration of a module's outline, as <c>rdcore/semantics/outline</c> answers it.
/// </summary>
public sealed record class OutlineEntry : OutlinedDeclaration
{
    /// <summary>
    /// The whole declaration.
    /// </summary>
    public SourceRange Range { get; init; }

    /// <summary>
    /// The name the declaration declares.
    /// </summary>
    public SourceRange SelectionRange { get; init; }

    /// <summary>
    /// The members it declares, in the order they are written: the members of a <c>Type</c> or an <c>Enum</c>.
    /// </summary>
    public IReadOnlyList<OutlineEntry> Children { get; init; } = [];
}

/// <summary>
/// What is known of a module, wherever it is written: the facts <see cref="ModuleOutline"/> and the assistant tools' outline share.
/// </summary>
public abstract record class OutlinedModule
{
    /// <summary>
    /// The name of the module.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What kind of module it is.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ModuleType>))]
    public ModuleType ModuleType { get; init; }

    /// <summary>
    /// Whether the module is the code of a form, which has a designer.
    /// </summary>
    public bool HasDesigner { get; init; }

    /// <summary>
    /// The identifier of the language the module is loaded as (<see cref="ModuleSemanticsDto.Language"/>); <see langword="null"/> when the host does not
    /// say.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Whether the module states <c>Option Explicit</c>; <see langword="null"/> when the host does not have the module, or its language has no such
    /// directive.
    /// </summary>
    public bool? OptionExplicit { get; init; }
}

/// <summary>
/// The answer to <c>rdcore/semantics/outline</c>: the declarations of a document's module, in the order they are written.
/// </summary>
[NewtonsoftConverter(typeof(SystemTextJsonBridge<ModuleOutline>))]
public sealed record class ModuleOutline : OutlinedModule
{
    /// <summary>
    /// The address of the document.
    /// </summary>
    public required Uri Uri { get; init; }

    /// <summary>
    /// The declarations of the module.
    /// </summary>
    public IReadOnlyList<OutlineEntry> Entries { get; init; } = [];
}

// the names of the flags of a value that are set, one bit at a time, in the order the enumeration declares them.
internal static class FlagNames
{
    public static IReadOnlyList<string> Of<TFlags>(TFlags value) where TFlags : struct, Enum
    {
        var bits = BitsOf(value);
        return [.. Enum.GetValues<TFlags>()
            .Select(flag => (Flag: flag, Bits: BitsOf(flag)))
            .Where(flag => flag.Bits != 0 && (flag.Bits & (flag.Bits - 1)) == 0 && (bits & flag.Bits) == flag.Bits)
            .Select(flag => flag.Flag.ToString())];
    }

    // the bits of a flag of any underlying type: a signed one's sign bit is a flag like the others.
    private static ulong BitsOf<TFlags>(TFlags value) where TFlags : struct, Enum
        => Type.GetTypeCode(typeof(TFlags)) == TypeCode.UInt64
            ? Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture)
            : unchecked((ulong)Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));
}
