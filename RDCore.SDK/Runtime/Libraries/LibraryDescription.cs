using System.Collections.Immutable;

namespace RDCore.SDK.Runtime.Libraries;

/// <summary>
/// What a referenced library declares, as a file: the types a project that references the library can name, and the members of each.
/// </summary>
/// <remarks>
/// <para>
/// A library is known by its <see cref="Name"/>: a project references <c>Scripting</c>, and source code qualifies its types with that name, as if the library
/// were VBA code. <see cref="Origin"/> is the type library it was described from, with the identifier and version that the registry of that machine
/// gives it. When libraries of one name are several (<c>DAO</c>), a reference or a dependency that gives an identifier and a version
/// finds the description whose origin they are (<see cref="LibraryIdentity"/>); one that gives a name alone finds the library of the name.
/// </para>
/// <para>
/// The description is a file because what a library declares is needed where the library is not: a workspace is analyzed on a machine that does not have
/// Excel, and the symbols it needs are those of a description that ships with the platform. It is written by a tool that reads the library where it is, and by
/// no one else; it is plain JSON (<see cref="LibraryJson"/>) so that a change to one reads as a change in review.
/// </para>
/// <para>
/// What it holds is what a VBA project can name: enumerations, and classes with their members and events. A type is named in a description the way VBA source
/// names it (<see cref="ParameterDescription.Type"/>).
/// </para>
/// </remarks>
public sealed record class LibraryDescription
{
    /// <summary>
    /// The version of the format of the file, which a reader that does not know it refuses rather than misreads (<see cref="LibraryJson.CurrentFormatVersion"/>).
    /// </summary>
    public int FormatVersion { get; init; } = LibraryJson.CurrentFormatVersion;

    /// <summary>
    /// The name of the library: what a project references it by, and what qualifies the types it declares (<c>Excel.Worksheet</c>).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// What the library says it is, in a sentence.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Where the description was made.
    /// </summary>
    public LibraryOrigin? Origin { get; init; }

    /// <summary>
    /// The libraries whose types this one's members are declared as, each by its name and, when the description says, the identifier and version of the type
    /// library it was built against. They are loaded with it, whether or not a project references them: a project that references Excel can use what Excel
    /// returns. A dependency whose name a project references is the library the project references (<strong>RD-VBAL §2.3.1.2</strong>).
    /// </summary>
    public ImmutableArray<LibraryIdentity> DependsOn { get; init; } = [];

    /// <summary>
    /// The enumerations the library declares.
    /// </summary>
    public ImmutableArray<EnumDescription> Enums { get; init; } = [];

    /// <summary>
    /// The classes the library declares.
    /// </summary>
    public ImmutableArray<ClassDescription> Classes { get; init; } = [];

    /// <summary>
    /// The modules the library declares: standard modules, whose members are procedures called on no object, and names of a project that references the
    /// library, as the standard library's are (<c>LoadPicture</c> of the OLE Automation library's <c>StdFunctions</c>). A module is not a type.
    /// </summary>
    /// <remarks>
    /// Stated from format version 3 of the file (<see cref="LibraryJson.CurrentFormatVersion"/>).
    /// </remarks>
    public ImmutableArray<ModuleDescription> Modules { get; init; } = [];

    /// <summary>
    /// This description, with what <paramref name="replaced"/> states that a type library cannot: each member that is design-time only there
    /// (<see cref="MemberDescription.IsDesignTimeOnly"/>) is design-time only here, matched by its class's name, its name and its kind, ignoring case.
    /// </summary>
    /// <remarks>
    /// Call it on a description exported from a type library that replaces the file of an earlier export. The type library declares a property that is
    /// design-time only the same way as a read-only one, so the earlier file is the only place the fact is stated.
    /// </remarks>
    /// <param name="replaced">The description this one replaces; <see langword="null"/> for none, which leaves this one as it is.</param>
    /// <param name="lost">
    /// The members that are design-time only in <paramref name="replaced"/> and that this description does not declare, as <c>Class.Member</c>, sorted; empty
    /// when none.
    /// </param>
    /// <returns>The description, with the members that are design-time only in <paramref name="replaced"/> stated as such.</returns>
    public LibraryDescription KeepingDesignTimeOnly(LibraryDescription? replaced, out IReadOnlyList<string> lost)
    {
        if (replaced is null)
        {
            lost = [];
            return this;
        }

        var stated = replaced.Classes
            .SelectMany(owner => owner.Members.Where(member => member.IsDesignTimeOnly).Select(member => (Key: KeyOf(owner, member), Shown: $"{owner.Name}.{member.Name}")))
            .ToDictionary(entry => entry.Key, entry => entry.Shown, StringComparer.OrdinalIgnoreCase);
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ImmutableArray<ClassDescription> classes = [.. Classes.Select(owner => owner with
        {
            Members = [.. owner.Members.Select(member => stated.ContainsKey(KeyOf(owner, member)) && kept.Add(KeyOf(owner, member)) ? member with { IsDesignTimeOnly = true } : member)],
        })];

        lost = [.. stated.Where(entry => !kept.Contains(entry.Key)).Select(entry => entry.Value).Order(StringComparer.OrdinalIgnoreCase)];
        return this with { Classes = classes };
    }

    // the accessors of a property share its name: the kind tells them apart.
    private static string KeyOf(ClassDescription owner, MemberDescription member) => $"{owner.Name}.{member.Name}/{member.Kind}";
}

/// <summary>
/// Where a library description was made: the type library it describes, which a reference that gives an identifier and a version finds it by
/// (<see cref="LibraryIdentity.Of"/>). Source code resolves no name by it.
/// </summary>
public sealed record class LibraryOrigin
{
    /// <summary>
    /// The identifier of the type library, when it has one.
    /// </summary>
    public string? Guid { get; init; }

    /// <summary>
    /// The version of the type library the description was made from, as <c>major.minor</c>.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>The file the library was read from.</summary>
    public string? File { get; init; }

    /// <summary>What made the description.</summary>
    public string? ExportedBy { get; init; }
}

/// <summary>
/// An enumeration a library declares (<strong>MS-VBAL §5.2.3.4</strong>).
/// </summary>
public sealed record class EnumDescription
{
    /// <summary>The name of the enumeration.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>What the library says the enumeration is.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the library leaves it out of what it shows.</summary>
    public bool IsHidden { get; init; }

    /// <summary>The members of the enumeration, in declaration order.</summary>
    public ImmutableArray<EnumMemberDescription> Members { get; init; } = [];
}

/// <summary>
/// A member of an enumeration a library declares.
/// </summary>
public sealed record class EnumMemberDescription
{
    /// <summary>The name of the member.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Its value.</summary>
    public long Value { get; init; }

    /// <summary>What the library says it is.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the library leaves it out of what it shows.</summary>
    public bool IsHidden { get; init; }
}

/// <summary>
/// A class a library declares.
/// </summary>
public sealed record class ClassDescription
{
    /// <summary>The name of the class.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>What the library says the class is.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The name an object of the class is created by from a string - <c>CreateObject("Scripting.Dictionary")</c> - when it can be.
    /// </summary>
    public string? ProgId { get; init; }

    /// <summary>
    /// Whether the class can be created: with <c>New</c>, or by its <see cref="ProgId"/>. A class that is not is only ever obtained from another object.
    /// </summary>
    public bool IsCreatable { get; init; }

    /// <summary>Whether the library leaves it out of what it shows.</summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// Whether the class is the one the library's host is, which a host makes members of implicitly available (<c>Excel.Application</c>). Its members are never
    /// names of a project that references the library: a project says which object it is talking to.
    /// </summary>
    /// <remarks>
    /// The Visual Basic 6.0 library's <c>Global</c> is the exception: <see cref="LibrarySymbolReader"/> reads it as a standard module of the library, like
    /// VBA's own global module, so that its members are names of the project (<c>Forms</c>, <c>VB.Forms</c>, <c>VB.Global.Forms</c>).
    /// </remarks>
    public bool IsApplication { get; init; }

    /// <summary>
    /// The name of the interface the class's <see cref="Members"/> are declared by, when it is not the class's own: <c>IReturnBoolean</c> of Microsoft Forms'
    /// <c>ReturnBoolean</c>, <c>_Worksheet</c> of Excel's <c>Worksheet</c>. It is a type name of the library too, which a program can declare a variable as
    /// (<c>As MSForms.IReturnBoolean</c>), and which an object of the class is; <see langword="null"/> for a class whose interface is not described.
    /// </summary>
    public string? DefaultInterface { get; init; }

    /// <summary>Whether the library leaves the <see cref="DefaultInterface"/> out of what it shows.</summary>
    public bool IsDefaultInterfaceHidden { get; init; }

    /// <summary>The members of the class, in declaration order.</summary>
    public ImmutableArray<MemberDescription> Members { get; init; } = [];

    /// <summary>The events the class raises, in declaration order.</summary>
    public ImmutableArray<EventDescription> Events { get; init; } = [];
}

/// <summary>
/// A module a library declares: a type library's module, whose functions a project that references the library calls by name, or qualified by the library and
/// the module (<c>stdole.StdFunctions.LoadPicture</c>).
/// </summary>
public sealed record class ModuleDescription
{
    /// <summary>
    /// The name of the module.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// What the library says the module is.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether the library leaves it out of what it shows.
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// The procedures of the module, in declaration order: each is a <see cref="MemberKind.Method"/>, a <c>Function</c> when it has a
    /// <see cref="MemberDescription.Type"/>.
    /// </summary>
    public ImmutableArray<MemberDescription> Members { get; init; } = [];

    /// <summary>
    /// The constants of the module, in declaration order: names of a project that references the library, as a standard module's <c>Public Const</c> are
    /// (<c>dbLangGeneral</c> of DAO's <c>LanguageConstants</c>).
    /// </summary>
    public ImmutableArray<ConstantDescription> Constants { get; init; } = [];
}

/// <summary>
/// A constant of a module a library declares (<strong>MS-VBAL §5.2.3.1</strong>).
/// </summary>
public sealed record class ConstantDescription
{
    /// <summary>
    /// The name of the constant.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The declared type of the constant, written as VBA source writes it (<see cref="ParameterDescription.Type"/>): an intrinsic type other than an object.
    /// </summary>
    public string Type { get; init; } = "Variant";

    /// <summary>
    /// The value of the constant, written as a VBA literal: <c>True</c> or <c>False</c>, a number, or a string in double quotes. A constant whose value is not
    /// one of these is not read as a symbol.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// What the library says the constant is.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether the library leaves it out of what it shows.
    /// </summary>
    public bool IsHidden { get; init; }
}

/// <summary>
/// What a member of a class is.
/// </summary>
public enum MemberKind
{
    /// <summary>A <c>Sub</c>, or a <c>Function</c> when it has a <see cref="MemberDescription.Type"/>.</summary>
    Method = 0,

    /// <summary>A <c>Property Get</c>.</summary>
    PropertyGet = 1,

    /// <summary>A <c>Property Let</c>.</summary>
    PropertyLet = 2,

    /// <summary>A <c>Property Set</c>.</summary>
    PropertySet = 3,
}

/// <summary>
/// A member of a class a library declares.
/// </summary>
public sealed record class MemberDescription
{
    /// <summary>The name of the member.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>What the member is.</summary>
    public MemberKind Kind { get; init; }

    /// <summary>
    /// The type the member returns, or <see langword="null"/> for one that returns none: a <c>Sub</c>, a <c>Property Let</c>, a <c>Property Set</c>.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// The dispatch identifier the library gives the member, when it gives one. <c>0</c> marks the default member of a class and <c>-4</c> its enumerator.
    /// </summary>
    public int? DispId { get; init; }

    /// <summary>What the library says the member is.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the library leaves it out of what it shows.</summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// Whether the member is a property that a form's designer sets and that a program can only read: assigned while the program runs, late-bound, it raises
    /// run-time error 382, "'<em>name</em>' property cannot be set at run time". Stated on the <see cref="MemberKind.PropertyGet"/> of a property the library declares
    /// with no <see cref="MemberKind.PropertyLet"/> or <see cref="MemberKind.PropertySet"/>. Such a property that is not design-time only is read-only, and
    /// the same assignment raises run-time error 383, "'<em>name</em>' property is read-only".
    /// </summary>
    /// <remarks>
    /// A type library does not say which of the two a property is: both are a property get alone, with the same flags. So the fact is measured and stated in
    /// the library's file (<c>"isDesignTimeOnly": true</c>), which a description exported again keeps through
    /// <see cref="LibraryDescription.KeepingDesignTimeOnly"/>. The Visual Basic 6.0 library's is measured with VB6, by assigning each such property of its controls and its form while a program runs, through an <c>Object</c>, a
    /// <c>Control</c>, the form's <c>Controls</c> and a <c>Variant</c>: every one refuses the assignment, after the value is coerced to the property's type.
    /// </remarks>
    public bool IsDesignTimeOnly { get; init; }

    /// <summary>The parameters of the member, in declaration order.</summary>
    public ImmutableArray<ParameterDescription> Parameters { get; init; } = [];
}

/// <summary>
/// A parameter of a member or of an event.
/// </summary>
public sealed record class ParameterDescription
{
    /// <summary>The name of the parameter.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The type of the parameter, written as VBA source writes it: an intrinsic type (<c>Long</c>, <c>Variant</c>), a type of the library (<c>Worksheet</c>),
    /// a type of another library (<c>Office.CommandBar</c>), or any of them as an array (<c>String()</c>).
    /// </summary>
    public string Type { get; init; } = "Variant";

    /// <summary>Whether it is passed by value. A parameter is passed by reference unless it says otherwise.</summary>
    public bool IsByVal { get; init; }

    /// <summary>Whether the argument may be left out.</summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// The value an optional parameter takes when its argument is left out, written as a VBA literal, when the library states one.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>Whether it is a <c>ParamArray</c>.</summary>
    public bool IsParamArray { get; init; }
}

/// <summary>
/// An event a class raises.
/// </summary>
public sealed record class EventDescription
{
    /// <summary>The name of the event.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>What the library says the event is.</summary>
    public string? Description { get; init; }

    /// <summary>The parameters a handler of the event receives, in declaration order.</summary>
    public ImmutableArray<ParameterDescription> Parameters { get; init; } = [];
}
