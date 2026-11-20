using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Model.Values.Abstract;
using RDCore.SDK.Model.Values.Intrinsic;
using System.Collections.Immutable;
using System.Globalization;

namespace RDCore.SDK.Runtime.Libraries;

/// <summary>
/// Reads the symbols that libraries declare off their descriptions (<see cref="LibraryDescription"/>), as <c>StdLibSymbolReader</c> reads those of the
/// standard library off its declarations.
/// </summary>
/// <remarks>
/// <para>
/// A library is a project of its own, named for the library, and everything it declares says which (<see cref="SymbolProperties.Library"/>): that is what lets
/// <c>Excel.Worksheet</c> name the library's class and nothing else of the name, and what keeps one library's types from standing in for another's. What it
/// declares is a class module for each class, with its members and events, and for each enumeration a symbol and a constant for each member. The enumerations
/// hang off a synthetic module of the library that no source can name (<c>_Excel</c>), which is what makes both the enumeration and its constants names of
/// the project (<strong>MS-VBAL §5.2.3.4</strong>), as the standard library's are.
/// </para>
/// <para>
/// A member's code is not the workspace's, so none of it has an instruction list: each carries the key of what it stands for
/// (<see cref="SymbolProperties.ExternalTarget"/>), <c>Library.Class.Member/kind</c> with the kind one of <c>method</c>, <c>get</c>, <c>let</c> and <c>set</c>,
/// and a call to it is dispatched to whatever can reach the library.
/// </para>
/// <para>
/// The types of a library name one another - a worksheet has ranges, and a range has a worksheet - so a class cannot always be read after every class it names.
/// A class is read after those it names whenever it can be, and where two name each other the one that is read second names the first as a type that is
/// resolved by its name when it is used (<see cref="VBUnresolvedType"/>), as a workspace's classes that name one another are.
/// </para>
/// <para>
/// An object of a class is of the class's default interface (<see cref="ClassDescription.DefaultInterface"/>) and of each class its description names
/// (<see cref="ClassDescription.Implements"/>): each is one of the class symbol's <see cref="VBClassModuleSymbol.ImplementedInterfaces"/>, the default
/// interface first, and the class's type has the type of each among its <see cref="VBClassType.Supertypes"/>. They are given to the symbol when it is
/// created, as its default interface is: the classes of a library are read after those of the libraries it depends on or names a class of, whatever the
/// order of the list, and a class after the classes of its own library it names.
/// </para>
/// <para>
/// Classes that name one another as classes their objects are as well cannot each be created after the other. The one created first is created again once
/// the others are, with the classes it names, and until then a type that names it is resolved by its name when it is used (<see cref="VBUnresolvedType"/>).
/// The class that closes such a cycle holds the symbols the others had before it was created, which lack it: a walk of the interfaces that starts at it
/// reaches every class of the cycle all the same, since it is where the walk starts.
/// </para>
/// <para>
/// An application object (<see cref="ClassDescription.IsApplication"/>) is a class like any other, except the Visual Basic 6.0 library's (<c>VB.Global</c>,
/// known by the library's <see cref="LibraryOrigin.Guid"/>). That one is a standard module of the library, like VBA's own global module: its members are
/// procedures of the module, with no <c>Me</c>, and names of the project at the library's rank, and it has neither a type nor events.
/// </para>
/// </remarks>
/// <param name="workspaceRoot">
/// The workspace the symbols are addressed under. Library symbols are not <em>of</em> the workspace, but every <see cref="Symbol"/> is addressed relative to
/// one, so they share its root.
/// </param>
/// <param name="is64Bit">The pointer width of the environment: <c>LongPtr</c> is a different type in each.</param>
public sealed class LibrarySymbolReader(Uri workspaceRoot, bool is64Bit = true)
{
    /// <summary>
    /// Reads the symbols of libraries.
    /// </summary>
    /// <param name="libraries">
    /// The libraries (<see cref="ReferencedLibraries.Libraries"/>), in the order the symbols are returned in. Each is read after those of the list it depends
    /// on or names a class of (<see cref="ClassDescription.Implements"/>), wherever it is listed.
    /// </param>
    /// <param name="priorities">
    /// The reference precedence of each library (<see cref="ReferencedLibraries.Priorities"/>), which every symbol that is a name of the project carries
    /// (<see cref="SymbolProperties.LibraryPriority"/>). The position of a library among the others when omitted, from <c>1</c>: the first ranks highest.
    /// </param>
    public ImmutableArray<Symbol> Read(IReadOnlyList<LibraryDescription> libraries, IReadOnlyDictionary<string, int>? priorities = null)
    {
        var symbols = ImmutableArray.CreateBuilder<Symbol>();
        var enums = new Dictionary<(string Library, string Name), VBEnumType>(TypeKeyComparer.Instance);
        var classes = new Dictionary<(string Library, string Name), VBClassType>(TypeKeyComparer.Instance);
        var declared = libraries.ToDictionary(library => library.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var library in libraries)
        {
            symbols.Add(new VBProjectSymbol(workspaceRoot, library.Name).With(SymbolProperties.Library, library.Name));

            var owner = new VBStandardModuleSymbol(workspaceRoot, workspaceRoot, "_" + library.Name).With(SymbolProperties.Library, library.Name);
            symbols.Add(owner);

            foreach (var description in library.Enums)
            {
                var symbol = ReadEnum(library, description, owner.Uri);
                enums[(library.Name, description.Name)] = (VBEnumType)symbol.ResolvedType;
                symbols.Add(symbol);
                symbols.AddRange(((VBEnumType)symbol.ResolvedType).Members.Select(member => member.With(SymbolProperties.Library, library.Name)));
            }
        }

        // the classes and modules of each library, read in the order of their dependencies and returned in the order of the list.
        var read = libraries.ToDictionary(library => library.Name, _ => ImmutableArray.CreateBuilder<Symbol>(), StringComparer.OrdinalIgnoreCase);
        var implemented = new ImplementedClasses(libraries, classes, read);
        var types = new TypeNames(declared, enums, classes, implemented, is64Bit);
        foreach (var library in InDependencyOrder(libraries, declared))
        {
            var own = read[library.Name];
            var reading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var byName = library.Classes
                .GroupBy(description => description.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            // a class is read after the classes of the library it names, as types and as classes its objects are as well; a class that is being read already
            // is a cycle, and names no type until it is used.
            void ReadAfterDependencies(ClassDescription description)
            {
                if (classes.ContainsKey((library.Name, description.Name)) || !reading.Add(description.Name))
                {
                    return;
                }

                var namedClasses = ClassesNamedBy(description).Concat(ImplementedClasses.NamesOf(library, description)
                    .Where(key => key.Library.Equals(library.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(key => key.Name));
                foreach (var named in namedClasses.Where(name => byName.ContainsKey(name)))
                {
                    ReadAfterDependencies(byName[named]);
                }

                // the Visual Basic 6.0 library's application object is a module of the library: its members are names of the project, and it is not a type.
                if (description.IsApplication && IsVisualBasic(library))
                {
                    var module = new VBStandardModuleSymbol(workspaceRoot, new VBProjectSymbol(workspaceRoot, library.Name).Uri, description.Name)
                        .With(SymbolProperties.Library, library.Name);
                    own.Add(module);
                    own.AddRange(description.Members.Select(member => ReadMember(library, description.Name, member, module.Uri, types, ScopeKind.Module)));
                    return;
                }

                var symbol = ReadClass(library, description, types);

                // the class's default interface is a type name too (`As MSForms.IReturnBoolean`), with the class's members, and an object of the class is one.
                if (description.DefaultInterface is { Length: > 0 } interfaceName && !byName.ContainsKey(interfaceName)
                    && !classes.ContainsKey((library.Name, interfaceName)))
                {
                    var defaultInterface = InterfaceOf(library, symbol, interfaceName);
                    classes[(library.Name, interfaceName)] = VBClassType.FromClassModule(defaultInterface);
                    own.Add(defaultInterface);
                    symbol = symbol with { ImplementedInterfaces = [defaultInterface] };
                    implemented.Created((library.Name, interfaceName));
                }

                symbol = implemented.Give(library, description, symbol);
                classes[(library.Name, description.Name)] = VBClassType.FromClassModule(symbol);
                own.Add(symbol);
                implemented.Created((library.Name, description.Name));
            }

            foreach (var description in library.Classes)
            {
                ReadAfterDependencies(description);
            }

            // a module of the library is a standard module of its project, read after the classes its procedures are declared as: the procedures and the
            // constants are names of the project at the library's rank, and a procedure has no Me.
            foreach (var description in library.Modules)
            {
                var module = new VBStandardModuleSymbol(workspaceRoot, new VBProjectSymbol(workspaceRoot, library.Name).Uri, description.Name)
                    .With(SymbolProperties.Library, library.Name);
                own.Add(module);
                own.AddRange(description.Members.Select(member => ReadMember(library, description.Name, member, module.Uri, types, ScopeKind.Module)));
                own.AddRange(description.Constants.Select(constant => ReadConstant(library, constant, module.Uri, types)).OfType<Symbol>());
            }
        }

        foreach (var library in libraries)
        {
            symbols.AddRange(read[library.Name]);
        }

        // what shadows what is read off the symbols that are names of the project - the library's project, its module of enumerations, the enumerations and their
        // constants, the classes, the library's modules and their procedures, and the Visual Basic 6.0 library's Global module and its members. The members of a class are reached through an object, and no
        // name of the project is one of them.
        var rankOf = priorities ?? libraries.Select((library, index) => (library.Name, Rank: index + 1)).ToDictionary(entry => entry.Name, entry => entry.Rank, StringComparer.OrdinalIgnoreCase);
        return [.. symbols.Select(symbol => symbol.GetProperty(SymbolProperties.Library) is { } library && rankOf.TryGetValue(library, out var rank)
            ? symbol.With(SymbolProperties.LibraryPriority, rank)
            : symbol)];
    }

    // the libraries, each after those of the list it depends on or names a class of (ClassDescription.Implements), and in the order of the list otherwise.
    // Where libraries depend on one another, the one listed first is read after those it depends on.
    private static List<LibraryDescription> InDependencyOrder(IReadOnlyList<LibraryDescription> libraries, Dictionary<string, LibraryDescription> declared)
    {
        var order = new List<LibraryDescription>(libraries.Count);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(LibraryDescription library)
        {
            if (!visited.Add(library.Name))
            {
                return;
            }

            var dependencies = library.DependsOn.Select(dependency => dependency.Name)
                .Concat(library.Classes.SelectMany(description => ImplementedClasses.NamesOf(library, description)).Select(key => key.Library));
            foreach (var name in dependencies)
            {
                if (declared.TryGetValue(name, out var dependency))
                {
                    Visit(dependency);
                }
            }

            order.Add(library);
        }

        foreach (var library in libraries)
        {
            Visit(library);
        }

        return order;
    }

    // the classes an object of a class is as well (ClassDescription.Implements), given to the class's symbol when it is created: each is one of its
    // implemented interfaces, after its default interface. A class named that is not created yet, but is a class of the libraries read, is one the class
    // waits for: classes that name one another, or classes of libraries that depend on one another. The class is created again, with the classes it names,
    // once every class it waits for is created, and until then a type that names it is resolved by its name when it is used.
    private sealed class ImplementedClasses(
        IReadOnlyList<LibraryDescription> libraries,
        Dictionary<(string Library, string Name), VBClassType> classes,
        Dictionary<string, ImmutableArray<Symbol>.Builder> read)
    {
        // what each library creates a class symbol of: its classes, except the Visual Basic 6.0 library's application object, and their default interfaces.
        private readonly HashSet<(string Library, string Name)> _declared = new(
            libraries.SelectMany(library => library.Classes
                .Where(description => !(description.IsApplication && IsVisualBasic(library)))
                .SelectMany(description => description.DefaultInterface is { Length: > 0 } interfaceName
                    ? [(library.Name, description.Name), (library.Name, interfaceName)]
                    : new[] { (library.Name, description.Name) })),
            TypeKeyComparer.Instance);

        private readonly List<Waiting> _waiting = [];

        private sealed class Waiting(
            (string Library, string Name) key,
            VBClassModuleSymbol created,
            (string Library, string Name)[] named,
            HashSet<(string Library, string Name)> awaited)
        {
            public (string Library, string Name) Key { get; } = key;
            public VBClassModuleSymbol Created { get; } = created;
            public (string Library, string Name)[] Named { get; } = named;
            public HashSet<(string Library, string Name)> Awaited { get; } = awaited;
        }

        // a qualified name is a class of the library it names, and one that is not is a class of this library; the class itself is left out.
        public static IEnumerable<(string Library, string Name)> NamesOf(LibraryDescription library, ClassDescription description)
            => description.Implements
                .Select(name => name.Trim())
                .Select(name => name.IndexOf('.') is var split and > 0 ? (name[..split], name[(split + 1)..]) : (library.Name, name))
                .Where(key => !TypeKeyComparer.Instance.Equals(key, (library.Name, description.Name)));

        public bool IsWaiting((string Library, string Name) key) => _waiting.Any(waiting => TypeKeyComparer.Instance.Equals(waiting.Key, key));

        public VBClassModuleSymbol Give(LibraryDescription library, ClassDescription description, VBClassModuleSymbol symbol)
        {
            var key = (library.Name, description.Name);
            var named = NamesOf(library, description).ToArray();
            if (named.Length == 0)
            {
                return symbol;
            }

            // a class named that is created already, and waits for others, has an earlier symbol of theirs, so this one waits for them too.
            var awaited = new HashSet<(string Library, string Name)>(TypeKeyComparer.Instance);
            foreach (var name in named)
            {
                if (!classes.ContainsKey(name))
                {
                    if (_declared.Contains(name))
                    {
                        awaited.Add(name);
                    }
                }
                else if (_waiting.FirstOrDefault(waiting => TypeKeyComparer.Instance.Equals(waiting.Key, name)) is { } other)
                {
                    awaited.UnionWith(other.Awaited);
                }
            }

            awaited.Remove(key);
            if (awaited.Count > 0)
            {
                _waiting.Add(new Waiting(key, symbol, named, awaited));
            }

            return With(symbol, named);
        }

        // each class that waits for no other once this one is created is created again, in the order they were first created, so that one that names another
        // that waited has the symbol it is created again as.
        public void Created((string Library, string Name) created)
        {
            if (_waiting.Count == 0)
            {
                return;
            }

            foreach (var waiting in _waiting)
            {
                waiting.Awaited.Remove(created);
            }

            var ready = _waiting.Where(waiting => waiting.Awaited.Count == 0).ToArray();
            _waiting.RemoveAll(waiting => waiting.Awaited.Count == 0);
            foreach (var waiting in ready)
            {
                var earlier = classes[waiting.Key].Symbol;
                var again = With(waiting.Created, waiting.Named);
                classes[waiting.Key] = VBClassType.FromClassModule(again);

                var own = read[waiting.Key.Library];
                for (var index = 0; index < own.Count; index++)
                {
                    if (ReferenceEquals(own[index], earlier))
                    {
                        own[index] = again;
                        break;
                    }
                }
            }
        }

        // a name that names none of the classes created (an unknown class, or a class of a library that is not in the list) is left out, as a workspace
        // class's Implements of a name that resolves to no class is; a class named twice is there once.
        private VBClassModuleSymbol With(VBClassModuleSymbol symbol, (string Library, string Name)[] named)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { symbol.Uri.AbsoluteUri };
            ImmutableArray<VBClassModuleSymbol> interfaces =
            [
                .. symbol.ImplementedInterfaces.Where(implemented => seen.Add(implemented.Uri.AbsoluteUri)),
                .. named.Where(classes.ContainsKey).Select(name => classes[name].Symbol).Where(implemented => seen.Add(implemented.Uri.AbsoluteUri)),
            ];
            return interfaces.Length == symbol.ImplementedInterfaces.Length ? symbol : symbol with { ImplementedInterfaces = interfaces };
        }
    }

    // the names, without the library, of the types of this library that a class declares its members and events as.
    private static IEnumerable<string> ClassesNamedBy(ClassDescription description)
        => description.Members.SelectMany(member => member.Parameters.Select(parameter => parameter.Type).Append(member.Type))
            .Concat(description.Events.SelectMany(declaredEvent => declaredEvent.Parameters.Select(parameter => parameter.Type)))
            .OfType<string>()
            .Select(type => type.EndsWith("()", StringComparison.Ordinal) ? type[..^2] : type)
            .Where(type => type.Length > 0 && !type.Contains('.'))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    // the Visual Basic 6.0 library (VB6.OLB), known by its type library's GUID, whatever its version.
    private static readonly Guid VisualBasicLibraryGuid = new("FCFB3D2E-A0FA-1068-A738-08002B3371B5");

    private static bool IsVisualBasic(LibraryDescription library)
        => library.Origin?.Guid is { } guid && Guid.TryParse(guid, out var parsed) && parsed == VisualBasicLibraryGuid;

    private VBEnumMemberSymbol ReadEnum(LibraryDescription library, EnumDescription description, Uri ownerUri)
    {
        var symbol = new VBEnumMemberSymbol(
            workspaceRoot, ownerUri, description.Name, ScopeKind.Module, SymbolKindExt.Enum,
            VBUnknownType.TypeInfo, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public);

        var constants = description.Members
            .Select(member => Added((VBEnumConstMemberSymbol)new VBEnumConstMemberSymbol(
                workspaceRoot, symbol.Uri, member.Name, ScopeKind.Module, SymbolKindExt.EnumMember, SourceRange.Empty, SourceRange.Empty)
                .With(SymbolProperties.EnumValue, member.Value), member.IsAddition))
            .ToArray();

        return Added((VBEnumMemberSymbol)(symbol with { ResolvedType = new VBEnumType(symbol, constants, description.IsHidden) }).With(SymbolProperties.Library, library.Name),
            description.IsAddition);
    }

    // what RD-VBA adds to a library, which hover and completion can show as such.
    private static T Added<T>(T symbol, bool isAddition) where T : Symbol
        => isAddition ? (T)symbol.With(SymbolProperties.LibraryAddition, true) : symbol;

    private VBClassModuleSymbol ReadClass(LibraryDescription library, ClassDescription description, TypeNames types)
    {
        // a class belongs to the project of its library, and is identified as that project's: two libraries can declare a class of one name (Excel's Application
        // and the editor's), and a symbol is known by its Uri alone, so they could not be told apart as the workspace's own are.
        var symbol = (VBClassModuleSymbol)new VBClassModuleSymbol(workspaceRoot, new VBProjectSymbol(workspaceRoot, library.Name).Uri, description.Name)
            .With(SymbolProperties.Library, library.Name)
            .With(SymbolProperties.Creatable, description.IsCreatable)
            // TODO RDC11028: should a host's document class (Workbook, Worksheet; recorded in Excel) be Unregistered?
            // a library's classes are the host's, and a host adds members to its objects that the library does not declare.
            .With(SymbolProperties.Extensible, true);
        if (description.ProgId is { Length: > 0 } progId)
        {
            symbol = (VBClassModuleSymbol)symbol.With(SymbolProperties.ProgId, progId);
        }

        if (description.TypeName is { Length: > 0 } typeName)
        {
            symbol = (VBClassModuleSymbol)symbol.With(SymbolProperties.ReportedTypeName, typeName);
        }

        symbol = Added(symbol, description.IsAddition);

        ImmutableArray<VBTypeMemberSymbol> members =
        [
            .. description.Members.Select(member => ReadMember(library, description.Name, member, symbol.Uri, types)),
            .. description.Events.Select(declaredEvent => ReadEvent(library, declaredEvent, symbol.Uri, types)),
        ];

        symbol = symbol with { Members = members };
        return symbol with { DefaultInterfaceMembers = VBClassType.FromClassModule(symbol).Members };
    }

    // the default interface of a class: no class of its own, and none that is created; its members are the class's, called on the object as the class's are.
    private VBClassModuleSymbol InterfaceOf(LibraryDescription library, VBClassModuleSymbol coclass, string name)
    {
        var symbol = (VBClassModuleSymbol)new VBClassModuleSymbol(workspaceRoot, new VBProjectSymbol(workspaceRoot, library.Name).Uri, name)
            .With(SymbolProperties.Library, library.Name)
            .With(SymbolProperties.Creatable, false)
            .With(SymbolProperties.Extensible, true)
            .With(SymbolProperties.DefaultInterfaceOf, coclass.Name);

        return symbol with
        {
            Members = [.. coclass.Members.OfType<VBTypeMemberSymbol>().Where(member => member is not VBEventMemberSymbol)],
            DefaultInterfaceMembers = coclass.DefaultInterfaceMembers,
        };
    }

    private VBTypeMemberSymbol ReadMember(
        LibraryDescription library, string owner, MemberDescription description, Uri ownerUri, TypeNames types,
        ScopeKind scope = ScopeKind.Instance)
    {
        var returnType = description.Type is { Length: > 0 } type ? types.Resolve(type, library) : VBVoidType.TypeInfo;

        Symbol member = description.Kind switch
        {
            MemberKind.PropertyGet => new VBPropertyGetMemberSymbol(
                workspaceRoot, ownerUri, scope, description.Name, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public) { ResolvedType = returnType },
            MemberKind.PropertyLet => new VBPropertyLetMemberSymbol(
                workspaceRoot, ownerUri, description.Name, scope, SymbolKindExt.Property, VBVoidType.TypeInfo,
                SourceRange.Empty, SourceRange.Empty, AccessModifier.Public),
            MemberKind.PropertySet => new VBPropertySetMemberSymbol(
                workspaceRoot, ownerUri, description.Name, scope, SymbolKindExt.Property, VBVoidType.TypeInfo,
                SourceRange.Empty, SourceRange.Empty, AccessModifier.Public),
            _ when returnType is VBVoidType => new VBProcedureMemberSymbol(
                workspaceRoot, ownerUri, description.Name, scope, SymbolKindExt.Procedure, VBVoidType.TypeInfo,
                SourceRange.Empty, SourceRange.Empty, AccessModifier.Public),
            _ => new VBFunctionMemberSymbol(
                workspaceRoot, ownerUri, description.Name, scope, SymbolKindExt.Function, returnType,
                SourceRange.Empty, SourceRange.Empty, AccessModifier.Public),
        };

        var parameters = ReadParameters(library, description.Parameters, member.Uri, withMe: scope == ScopeKind.Instance, types);
        member = member switch
        {
            VBReturningMemberSymbol returning => returning with { Parameters = parameters },
            VBProcedureMemberSymbol procedure => procedure with { Parameters = parameters },
            _ => member,
        };

        member = member.With(SymbolProperties.Library, library.Name)
            .With(SymbolProperties.ExternalTarget, ExternalTargetOf(library.Name, owner, description));

        // what the library says the member does: what a completion list and a signature help show of it.
        if (description.Description is { Length: > 0 } docString)
        {
            member = member.With(SymbolProperties.DocString, docString);
        }

        // the default member of a class and its enumerator are found by the id they carry, as those of a workspace class are by their attributes.
        if (description.DispId is 0 or -4)
        {
            member = member.With(SymbolProperties.UserMemId, description.DispId.Value);
        }

        // a property the designer sets and the program only reads: an assignment to it at run time raises 382, where a read-only property's raises 383.
        if (description.IsDesignTimeOnly)
        {
            member = member.With(SymbolProperties.DesignTimeOnly, true);
        }

        // the members a hidden default member stands for, which a code fix names when it writes an access of it out in full.
        if (description.WrittenAs is { } writtenAs)
        {
            member = member.With(SymbolProperties.WrittenAs, writtenAs);
        }

        member = Added(member, description.IsAddition);
        return (VBTypeMemberSymbol)(description.IsHidden
            ? member.With(SymbolProperties.MemberFlags, member.GetProperty(SymbolProperties.MemberFlags) | SymbolProperties.HiddenMemberFlag)
            : member);
    }

    // a constant of a library module is a Public Const of the module (MS-VBAL §5.2.3.2), whose expression is a literal of its declared type: `Const x As
    // Integer = 3` is an Integer wherever it is used. One whose value is not a literal of that type is not one a program can use, and is not read.
    private VBConstantMemberSymbol? ReadConstant(LibraryDescription library, ConstantDescription description, Uri moduleUri, TypeNames types)
    {
        var type = types.Resolve(description.Type, library);
        if (description.Value is not { Length: > 0 } text || ConstantOf(text, type) is not { } value)
        {
            return null;
        }

        Symbol constant = new VBConstantMemberSymbol(
            workspaceRoot, moduleUri, description.Name, ScopeKind.Module, value.TypeInfo, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public,
            new LiteralExpressionNode(new SyntaxNodeId(moduleUri.AbsolutePath, []), new SourceLocation(moduleUri, SourceRange.Empty), value))
            .With(SymbolProperties.Library, library.Name);

        if (description.Description is { Length: > 0 } docString)
        {
            constant = constant.With(SymbolProperties.DocString, docString);
        }

        constant = Added(constant, description.IsAddition);
        return (VBConstantMemberSymbol)(description.IsHidden
            ? constant.With(SymbolProperties.MemberFlags, constant.GetProperty(SymbolProperties.MemberFlags) | SymbolProperties.HiddenMemberFlag)
            : constant);
    }

    // the literal as a value of the constant's declared type; an enumeration's constant is a Long, as an enumeration's members are (MS-VBAL §5.2.3.4).
    private static VBTypedValue? ConstantOf(string text, VBType type)
    {
        var literal = LiteralOf(text);
        var number = literal switch
        {
            VBLongValue whole => (double?)whole.Value,
            VBDoubleValue real => real.Value,
            _ => null,
        };

        return (type, literal, number) switch
        {
            (VBStringType, VBStringValue, _) or (VBBooleanType, VBBooleanValue, _) => literal,
            (VBVariantType, not null, _) => literal,
            (VBByteType, _, >= byte.MinValue and <= byte.MaxValue) when number % 1 == 0 => new VBByteValue((byte)number.Value),
            (VBIntegerType, _, >= short.MinValue and <= short.MaxValue) when number % 1 == 0 => new VBIntegerValue((short)number.Value),
            (VBLongType or VBEnumType, _, >= int.MinValue and <= int.MaxValue) when number % 1 == 0 => new VBLongValue((int)number.Value),
            (VBLongLongType, _, not null) when long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var big) => new VBLongLongValue(big),
            (VBSingleType, _, not null) => new VBSingleValue((float)number.Value),
            (VBDoubleType, _, not null) => new VBDoubleValue(number.Value),
            (VBCurrencyType, _, not null) when decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var money) => new VBCurrencyValue(money),
            _ => null,
        };
    }

    private VBTypeMemberSymbol ReadEvent(LibraryDescription library, EventDescription description, Uri ownerUri, TypeNames types)
    {
        var declaredEvent = new VBEventMemberSymbol(
            workspaceRoot, ownerUri, description.Name, ScopeKind.Instance, SourceRange.Empty, SourceRange.Empty, AccessModifier.Public);

        return Added((VBTypeMemberSymbol)(declaredEvent with { Parameters = ReadParameters(library, description.Parameters, declaredEvent.Uri, withMe: false, types) })
            .With(SymbolProperties.Library, library.Name), description.IsAddition);
    }

    /// <summary>
    /// The key a member of a library class carries as <see cref="SymbolProperties.ExternalTarget"/>: <c>Library.Class.Member/kind</c>, with the kind one of
    /// <c>method</c>, <c>get</c>, <c>let</c> and <c>set</c>.
    /// </summary>
    /// <remarks>
    /// The reader defines the format because the reader stamps it, as the standard library's does: a dispatcher that built its own key could disagree with
    /// this one and nothing would say so until a call went to the wrong member. The kind is part of it because the accessors of a property share a name.
    /// </remarks>
    /// <param name="library">The library.</param>
    /// <param name="owner">The class.</param>
    /// <param name="member">The member.</param>
    public static string ExternalTargetOf(LibraryDescription library, ClassDescription owner, MemberDescription member)
        => ExternalTargetOf(library.Name, owner.Name, member);

    /// <summary>
    /// The key a procedure of a library module carries as <see cref="SymbolProperties.ExternalTarget"/>: <c>Library.Module.Procedure/method</c>, in the format
    /// of a class member's (<see cref="ExternalTargetOf(LibraryDescription, ClassDescription, MemberDescription)"/>).
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="owner">The module.</param>
    /// <param name="member">The procedure.</param>
    public static string ExternalTargetOf(LibraryDescription library, ModuleDescription owner, MemberDescription member)
        => ExternalTargetOf(library.Name, owner.Name, member);

    private static string ExternalTargetOf(string library, string owner, MemberDescription member)
        => $"{library}.{owner}.{member.Name}/{member.Kind switch
        {
            MemberKind.PropertyGet => "get",
            MemberKind.PropertyLet => "let",
            MemberKind.PropertySet => "set",
            _ => "method",
        }}";

    private ImmutableArray<VBParameterSymbol> ReadParameters(
        LibraryDescription library, ImmutableArray<ParameterDescription> descriptions, Uri memberUri, bool withMe, TypeNames types)
    {
        var builder = ImmutableArray.CreateBuilder<VBParameterSymbol>(descriptions.Length + 1);

        // an instance member's implicit parameter 0, bound to the object the call is dispatched on.
        if (withMe)
        {
            builder.Add(new VBParameterSymbol(
                workspaceRoot, memberUri, "Me", SourceRange.Empty, SourceRange.Empty, ParameterKind.ImplicitByRef, VBObjectType.TypeInfo));
        }

        foreach (var description in descriptions)
        {
            if (description.IsParamArray)
            {
                // MS-VBAL §5.3.1.5: a param array is declared with no parameter mechanism, and its elements are ByRef.
                builder.Add(new ParamArrayParameterSymbol(
                    workspaceRoot, memberUri, description.Name, SourceRange.Empty, SourceRange.Empty, ParameterKind.ImplicitByRef));
                continue;
            }

            builder.Add(new VBParameterSymbol(
                workspaceRoot, memberUri, description.Name, SourceRange.Empty, SourceRange.Empty,
                description.IsByVal ? ParameterKind.ExplicitByVal : ParameterKind.ExplicitByRef,
                types.Resolve(description.Type, library), description.IsOptional, DefaultValueOf(description, memberUri)));
        }

        return builder.ToImmutable();
    }

    // MS-VBAL 5.3.1.5 defines a default as a constant expression, which is what the symbol carries; a library's is written as a literal in its description,
    // and a literal node over the value says exactly that - with the member's own address for a location, there being no source to point at. A default that
    // is not a literal this reads is no clause at all: an argument that is left out then takes the declared type's own default.
    private static LiteralExpressionNode? DefaultValueOf(ParameterDescription description, Uri memberUri)
        => description is { IsOptional: true, DefaultValue: { Length: > 0 } text } && LiteralOf(text) is { } value
            ? new LiteralExpressionNode(new SyntaxNodeId(memberUri.AbsolutePath, []), new SourceLocation(memberUri, SourceRange.Empty), value)
            : null;

    private static VBTypedValue? LiteralOf(string text)
    {
        if (text.Equals("True", StringComparison.OrdinalIgnoreCase))
        {
            return VBBooleanValue.True;
        }

        if (text.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            return VBBooleanValue.False;
        }

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            return new VBStringValue(text[1..^1].Replace("\"\"", "\""));
        }

        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
        {
            return new VBLongValue(whole);
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? new VBDoubleValue(number) : null;
    }

    // a type is named in a description the way VBA source names it, and meant the way the library means it: its own types first, then the libraries it
    // depends on and qualified by the library they are of.
    private sealed class TypeNames(
        Dictionary<string, LibraryDescription> libraries,
        Dictionary<(string Library, string Name), VBEnumType> enums,
        Dictionary<(string Library, string Name), VBClassType> classes,
        ImplementedClasses implemented,
        bool is64Bit)
    {
        public VBType Resolve(string name, LibraryDescription from)
        {
            var text = name.Trim();
            if (text.EndsWith("()", StringComparison.Ordinal))
            {
                var element = Resolve(text[..^2], from);
                return element is VBByteType ? VBResizableByteArrayType.TypeInfo : new VBResizableArrayType(element);
            }

            if (text.Equals("LongPtr", StringComparison.OrdinalIgnoreCase))
            {
                return is64Bit ? VBLongPtrType_x64.TypeInfo : VBLongPtrType_x86.TypeInfo;
            }

            if (IntrinsicVBTypes.TryResolve(text, out var intrinsic))
            {
                return intrinsic;
            }

            var split = text.IndexOf('.');
            if (split > 0)
            {
                var qualifier = text[..split];
                return libraries.TryGetValue(qualifier, out var named) && Find(named.Name, text[(split + 1)..]) is { } qualified
                    ? qualified
                    : new VBUnresolvedType(text);
            }

            foreach (var library in new[] { from }.Concat(from.DependsOn.Select(dependency => dependency.Name).Where(libraries.ContainsKey).Select(dependency => libraries[dependency])))
            {
                if (Find(library.Name, text) is { } found)
                {
                    return found;
                }
            }

            // a class of this library that has not been read yet - or is being read, when two name each other - is named by the way the library names it.
            return new VBUnresolvedType(libraries.ContainsKey(from.Name) && from.Classes.Any(candidate => candidate.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
                ? $"{from.Name}.{text}"
                : text);
        }

        // a class that waits for the classes its objects are as well is created again once they are, so a type that names it now is resolved by its name.
        private VBType? Find(string library, string name)
            => enums.TryGetValue((library, name), out var enumType) ? enumType
                : implemented.IsWaiting((library, name)) ? new VBUnresolvedType($"{library}.{name}")
                : classes.TryGetValue((library, name), out var classType) ? classType
                : null;
    }

    private sealed class TypeKeyComparer : IEqualityComparer<(string Library, string Name)>
    {
        public static TypeKeyComparer Instance { get; } = new();

        public bool Equals((string Library, string Name) x, (string Library, string Name) y)
            => string.Equals(x.Library, y.Library, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Library, string Name) key)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Library), StringComparer.OrdinalIgnoreCase.GetHashCode(key.Name));
    }
}
