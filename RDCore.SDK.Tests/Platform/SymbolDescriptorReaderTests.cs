using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using RDCore.SDK.Model;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Platform.Components;

namespace RDCore.Tests.Platform;

[TestClass]
public sealed class SymbolDescriptorReaderTests
{
    private static readonly Uri WorkspaceRoot = new("file:///c:/ws/");

    private static VBType? Intrinsic(string name) => IntrinsicVBTypes.TryResolve(name, out var type) ? type : null;

    private static Symbol[] Read(params SymbolDescriptor[] symbols) => [.. SymbolDescriptorReader.Read(
        new DefineSymbolsParams { WorkspaceRoot = WorkspaceRoot, ModuleName = "Mod1", Symbols = [.. symbols] },
        Intrinsic)];

    [TestMethod]
    public void Function_BindsIntrinsicReturnType_AndParameters()
    {
        var symbols = Read(new SymbolDescriptor
        {
            Name = "Add",
            Kind = SymbolDescriptorKind.Function,
            AccessModifier = AccessModifier.Public,
            DeclaredTypeName = "Long",
            Parameters =
            [
                new ParameterDescriptor { Name = "a", DeclaredTypeName = "Long" },
                new ParameterDescriptor { Name = "b", DeclaredTypeName = "Long" },
            ],
        });

        var function = (VBFunctionMemberSymbol)symbols.Single();
        Assert.AreEqual(VBTypeNames.VBLong, function.ResolvedType.Name);
        Assert.AreEqual(2, function.Parameters.Length);
        Assert.AreEqual(VBTypeNames.VBLong, function.Parameters[0].ResolvedType.Name);
        Assert.AreEqual(function.Uri, function.Parameters[0].ParentUri);
    }

    [TestMethod]
    public void UnresolvedDeclaredType_StaysUnknown()
    {
        var field = (VBModuleFieldVariableMemberSymbol)Read(new SymbolDescriptor
        {
            Name = "Widget",
            Kind = SymbolDescriptorKind.ModuleField,
            DeclaredTypeName = "CWidget",
        }).Single();

        Assert.IsInstanceOfType<VBUnknownType>(field.ResolvedType);
    }

    [TestMethod]
    public void Enum_YieldsEnumSymbolThenMembersParentedToIt()
    {
        var symbols = Read(new SymbolDescriptor
        {
            Name = "Direction",
            Kind = SymbolDescriptorKind.Enum,
            Members =
            [
                new SymbolDescriptor { Name = "North", Kind = SymbolDescriptorKind.EnumMember },
                new SymbolDescriptor { Name = "South", Kind = SymbolDescriptorKind.EnumMember },
            ],
        });

        Assert.AreEqual(3, symbols.Length);
        var enumSymbol = (VBEnumMemberSymbol)symbols[0];
        var members = symbols.OfType<VBEnumConstMemberSymbol>().ToArray();
        Assert.AreEqual(2, members.Length);
        Assert.IsTrue(members.All(m => m.ParentUri == enumSymbol.Uri));
    }

    [TestMethod]
    public void UserDefinedType_YieldsTypeThenFieldsParentedToIt()
    {
        var symbols = Read(new SymbolDescriptor
        {
            Name = "TPoint",
            Kind = SymbolDescriptorKind.UserDefinedType,
            Members =
            [
                new SymbolDescriptor { Name = "X", Kind = SymbolDescriptorKind.UserDefinedTypeField, DeclaredTypeName = "Long" },
                new SymbolDescriptor { Name = "Y", Kind = SymbolDescriptorKind.UserDefinedTypeField, DeclaredTypeName = "Long" },
            ],
        });

        Assert.AreEqual(3, symbols.Length);
        var udt = (VBUserDefinedTypeMemberSymbol)symbols[0];
        var fields = symbols.OfType<VBUserDefinedTypeFieldSymbol>().ToArray();
        Assert.AreEqual(2, fields.Length);
        Assert.IsTrue(fields.All(f => f.ParentUri == udt.Uri));
        Assert.IsTrue(fields.All(f => f.ResolvedType.Name == VBTypeNames.VBLong));
    }

    [TestMethod]
    public void Event_ReconstructsWithParametersParentedToTheEvent()
    {
        var evt = (VBEventMemberSymbol)Read(new SymbolDescriptor
        {
            Name = "Changed",
            Kind = SymbolDescriptorKind.Event,
            Parameters = [new ParameterDescriptor { Name = "NewValue", DeclaredTypeName = "Long" }],
        }).Single();

        Assert.AreEqual(1, evt.Parameters.Length);
        Assert.AreEqual("NewValue", evt.Parameters[0].Name);
        Assert.AreEqual(evt.Uri, evt.Parameters[0].ParentUri);
        Assert.AreEqual(VBTypeNames.VBLong, evt.Parameters[0].ResolvedType.Name);
    }

    [TestMethod]
    public void ExternalFunction_CarriesDeclareMetadata()
    {
        var external = (VBExternalFunctionMemberSymbol)Read(new SymbolDescriptor
        {
            Name = "GetTickCount",
            Kind = SymbolDescriptorKind.ExternalFunction,
            Scope = ScopeKind.External,
            DeclaredTypeName = "Long",
            External = new ExternalDescriptor { IsPtrSafe = true, Library = "kernel32", Alias = "GetTickCount64" },
        }).Single();

        Assert.IsTrue(external.IsPtrSafe);
        Assert.AreEqual("kernel32", external.Lib);
        Assert.AreEqual("GetTickCount64", external.Alias);
        Assert.AreEqual(VBTypeNames.VBLong, external.ResolvedType.Name);
    }

    [TestMethod]
    public void MultiBranchDescriptor_ReconstructsEveryDefinitionSite()
    {
        var field = (VBModuleFieldVariableMemberSymbol)Read(new SymbolDescriptor
        {
            Name = "Flags",
            Kind = SymbolDescriptorKind.ModuleField,
            DeclaredTypeName = "Long",
            Range = new SourceRange(1, 0, 1, 15),
            SelectionRange = new SourceRange(1, 4, 1, 9),
            Definitions =
            [
                new DefinitionDescriptor { Range = new SourceRange(1, 0, 1, 15), SelectionRange = new SourceRange(1, 4, 1, 9) },
                new DefinitionDescriptor { Range = new SourceRange(3, 0, 3, 17), SelectionRange = new SourceRange(3, 4, 3, 9) },
            ],
        }).Single();

        Assert.AreEqual(2, field.Definitions.Length);
        Assert.IsTrue(field.Definitions.All(d => d.State == DefinitionState.Unknown));
        // Range/SelectionRange stay the primary (first) site.
        Assert.AreEqual(new SourceRange(1, 0, 1, 15), field.Range);
        Assert.AreEqual(new SourceRange(3, 0, 3, 17), field.Definitions[1].Range);
    }

    private static readonly Uri Document = new("file:///c:/ws/Mod1.bas");

    // a module written in two files: a name it declares in a #If branch of the other file than the procedure's.
    private static readonly Uri OtherPart = new("file:///c:/ws/Mod1.Part2.bas");

    // what a descriptor declares is written in its document: its parameters, its locals and the fields of its type with it.
    [TestMethod]
    public void WhatADescriptorDeclares_IsWrittenInItsDocument()
    {
        var unlessLive = new SourceLocation(OtherPart, new SourceRange(2, 4, 2, 4));
        var symbols = Read(
            new SymbolDescriptor
            {
                Name = "Add",
                Kind = SymbolDescriptorKind.Function,
                DocumentUri = Document,
                Parameters = [new ParameterDescriptor { Name = "a", DeclaredTypeName = "Long" }],
                Locals = [new LocalDescriptor { Name = "total", DeclaredBy = LocalDeclarationKind.Implicit, UnlessLive = [unlessLive] }],
            },
            new SymbolDescriptor
            {
                Name = "TPoint",
                Kind = SymbolDescriptorKind.UserDefinedType,
                DocumentUri = Document,
                Members = [new SymbolDescriptor { Name = "X", Kind = SymbolDescriptorKind.UserDefinedTypeField, DeclaredTypeName = "Long" }],
            });

        var function = symbols.OfType<VBFunctionMemberSymbol>().Single();
        var type = symbols.OfType<VBUserDefinedTypeMemberSymbol>().Single();
        WorkspaceSymbol[] written =
            [function, .. function.Parameters, .. function.Locals, type, .. type.Members, .. symbols.OfType<VBUserDefinedTypeFieldSymbol>()];
        Assert.HasCount(6, written);
        Assert.IsTrue(written.All(symbol => symbol.DocumentUri == Document),
            string.Join(", ", written.Select(symbol => $"{symbol.Name}: {symbol.DocumentUri}")));
        Assert.AreEqual(new SourceLocation(Document, function.Range), function.Location);
        CollectionAssert.AreEqual(new[] { unlessLive }, function.Locals.Single().GetProperty(SymbolProperties.UnlessLive).ToArray());
    }

    // the other branches of a declaration are written where it is: a module's #If block is in one file.
    [TestMethod]
    public void ASymbolInADocument_HasItsAlternativesThere()
    {
        var field = (WorkspaceSymbol)Read(MultiBranchField(document: null)).Single();

        var written = (WorkspaceSymbol)(field with { Alternatives = [field] }).InDocument(Document);

        Assert.AreEqual(Document, written.DocumentUri);
        Assert.AreEqual(Document, written.Alternatives.Single().DocumentUri);
    }

    [TestMethod]
    public void ADescriptorOfNoDocument_DeclaresSymbolsOfNone()
    {
        var field = (WorkspaceSymbol)Read(new SymbolDescriptor { Name = "Total", Kind = SymbolDescriptorKind.ModuleField }).Single();

        Assert.IsNull(field.DocumentUri);
        Assert.IsNull(field.Location);
    }

    // the dead branches are keyed by document and range: the same range of another document is not dead, and a site of no document is in no branch.
    [TestMethod]
    [DataRow("file:///c:/ws/Mod1.bas", DefinitionState.Dead, DisplayName = "the second site's own document")]
    [DataRow("file:///c:/ws/Mod1.Part2.bas", DefinitionState.Live, DisplayName = "the same range of another document")]
    public void TheLiveDeclaration_IsDecidedWhereEachSiteIsWritten(string deadIn, DefinitionState second)
    {
        var dead = new SourceLocation(new Uri(deadIn), new SourceRange(3, 0, 3, 17));
        var field = (WorkspaceSymbol)Read(MultiBranchField(Document)).Single();

        var live = field.LiveDeclaration(location => location.Uri == dead.Uri && location.Range.Start == dead.Range.Start);

        Assert.IsNotNull(live);
        CollectionAssert.AreEqual(new[] { DefinitionState.Live, second }, live.Definitions.Select(definition => definition.State).ToArray());
    }

    [TestMethod]
    public void ASiteOfNoDocument_IsLive()
    {
        var field = (WorkspaceSymbol)Read(MultiBranchField(document: null)).Single();

        var live = field.LiveDeclaration(_ => true);

        Assert.IsNotNull(live, "a symbol of no document is in no #If branch");
        Assert.IsTrue(live.Definitions.All(definition => definition.State == DefinitionState.Live));
    }

    private static SymbolDescriptor MultiBranchField(Uri? document) => new()
    {
        Name = "Flags",
        Kind = SymbolDescriptorKind.ModuleField,
        DeclaredTypeName = "Long",
        DocumentUri = document,
        Range = new SourceRange(1, 0, 1, 15),
        SelectionRange = new SourceRange(1, 4, 1, 9),
        Definitions =
        [
            new DefinitionDescriptor { Range = new SourceRange(1, 0, 1, 15), SelectionRange = new SourceRange(1, 4, 1, 9) },
            new DefinitionDescriptor { Range = new SourceRange(3, 0, 3, 17), SelectionRange = new SourceRange(3, 4, 3, 9) },
        ],
    };

    [TestMethod]
    public void SingleBranchDescriptor_LeavesDefinitionsEmpty()
    {
        var field = (VBModuleFieldVariableMemberSymbol)Read(new SymbolDescriptor
        {
            Name = "Total",
            Kind = SymbolDescriptorKind.ModuleField,
            DeclaredTypeName = "Long",
        }).Single();

        Assert.IsTrue(field.Definitions.IsDefaultOrEmpty);
    }

    // what the language server sends the environment host (rdcore/host/symbols/define), as the platform's serializer writes and reads it, with the root of a
    // workspace of this machine: on Linux, a root written as its path came back relative, and the host defined nothing.
    [TestMethod]
    public void TheSymbolsOfAWorkspaceOfThisMachine_AreReadUnderItsRoot_OnceTheyHaveTravelled()
    {
        var root = SymbolUri.Root(Path.Combine(Path.GetTempPath(), "rdcore-define-ws"));
        var serializer = new LspSerializer();
        var sent = new DefineSymbolsParams
        {
            WorkspaceRoot = root,
            ModuleName = "Mod1",
            Symbols = [new SymbolDescriptor { Name = "Answer", Kind = SymbolDescriptorKind.Function, DeclaredTypeName = "Long" }],
        };

        var received = (DefineSymbolsParams)serializer.DeserializeObject(serializer.SerializeObject(sent), typeof(DefineSymbolsParams));

        Assert.AreEqual(SymbolUri.Member(SymbolUri.Module(root, "Mod1"), "Answer"), SymbolDescriptorReader.Read(received, Intrinsic).Single().Uri);
    }

    [TestMethod]
    public void MissingWorkspaceRoot_Throws()
        => Assert.ThrowsExactly<ArgumentException>(
            () => SymbolDescriptorReader.Read(new DefineSymbolsParams { ModuleName = "X" }, Intrinsic).ToArray());

    private static readonly Uri LibraryRoot = new("file:///c:/ws/NavalLib/");

    private static DefineSymbolsParams LibraryRequest(ModuleType kind, ModuleDirectives directives, params SymbolDescriptor[] symbols) => new()
    {
        WorkspaceRoot = WorkspaceRoot,
        ModuleUri = SymbolUri.Member(LibraryRoot, "Module1"),
        ModuleName = "Module1",
        Library = "NavalLib",
        LibraryPriority = 1,
        ModuleType = kind,
        Directives = directives,
        Symbols = [.. symbols],
    };

    private static SymbolDescriptor PublicProcedure(string name)
        => new() { Name = name, Kind = SymbolDescriptorKind.Procedure, Scope = ScopeKind.Module, AccessModifier = AccessModifier.Public };

    // a referenced project's Module1 and the session's own Module1 are two modules: each one's members are its own.
    [TestMethod]
    public void TheMembersOfAModuleOfAReferencedProject_AreAddressedUnderThatProjectsRoot()
    {
        var request = LibraryRequest(ModuleType.StdModule, ModuleDirectives.None, PublicProcedure("PlaceShip"));

        var library = SymbolDescriptorReader.Read(request, Intrinsic).Single();
        var own = SymbolDescriptorReader.Read(request with { Library = null }, Intrinsic).Single();

        Assert.AreEqual(SymbolDescriptorReader.LibraryModuleOf(request)!.Uri, library.ParentUri);
        Assert.AreEqual(request.ModuleUri, library.ParentUri);
        Assert.AreEqual(LibraryRoot, library.WorkspaceRoot);
        Assert.AreEqual(SymbolUri.Member(WorkspaceRoot, "Module1"), own.ParentUri);
        Assert.AreNotEqual(own.Uri, library.Uri);
    }

    [TestMethod]
    [DataRow(ModuleType.StdModule, typeof(VBStandardModuleSymbol))]
    [DataRow(ModuleType.ClassModule, typeof(VBClassModuleSymbol))]
    [DataRow(ModuleType.DocumentModule, typeof(VBClassModuleSymbol), DisplayName = "a document module is a class module")]
    public void TheModuleOfAReferencedProject_IsOfItsKind_UnderItsDirectives(ModuleType kind, Type expected)
    {
        var directives = new ModuleDirectives(PredeclaredId: true, Exposed: true);

        var module = SymbolDescriptorReader.LibraryModuleOf(LibraryRequest(kind, directives))!;

        Assert.IsInstanceOfType(module, expected);
        Assert.AreEqual(SymbolUri.Member(LibraryRoot, "Module1"), module.Uri);
        Assert.AreEqual(directives, module.Directives);
        Assert.IsNull(module.GetProperty(SymbolProperties.Library), "the host tags it");
        if (module is VBClassModuleSymbol)
        {
            Assert.IsTrue(module.GetProperty(SymbolProperties.PredeclaredId));
        }
    }

    [TestMethod]
    public void AModuleOfTheSessionsOwnProject_HasNoModuleMadeForIt()
        => Assert.IsNull(SymbolDescriptorReader.LibraryModuleOf(new DefineSymbolsParams { WorkspaceRoot = WorkspaceRoot, ModuleName = "Module1" }));

    [TestMethod]
    public void AModuleOfAReferencedProject_WithoutItsAddress_Throws()
    {
        var request = LibraryRequest(ModuleType.StdModule, ModuleDirectives.None, PublicProcedure("PlaceShip")) with { ModuleUri = null };

        Assert.ThrowsExactly<ArgumentException>(() => SymbolDescriptorReader.Read(request, Intrinsic));
        Assert.ThrowsExactly<ArgumentException>(() => SymbolDescriptorReader.LibraryModuleOf(request));
    }
}
