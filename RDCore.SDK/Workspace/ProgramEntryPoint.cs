using RDCore.SDK.Model;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Symbols.VBProject;

namespace RDCore.SDK.Workspace;

/// <summary>
/// Where a program of a project starts: the procedure a run of the project runs (<see cref="Of"/>).
/// </summary>
/// <remarks>
/// <para>
/// MS-VBAL says nothing of a program's start: a VBA project is code a host calls into. A project that is a program has one entry point, as a VB6 project's
/// startup object is <c>Sub Main</c>: a <c>Sub</c> named <see cref="Name"/>, in a standard module, that the rest of the project can call - <c>Public</c> or
/// <c>Global</c>, or written with no scope, which is <c>Public</c> (<strong>MS-VBAL §5.3.1.1</strong>) - and that takes no argument, or takes what it is
/// given as one <c>ParamArray</c>: <c>Sub Main()</c>, or <c>Sub Main(ParamArray Args())</c>. A class or a document has no entry point: running one runs no
/// program.
/// </para>
/// <para>
/// The procedure is judged as the runtime session defined it, which is the procedure a run runs: of a declaration whose header is written once in each
/// branch of a <c>#If</c> block, the header of the live branch under the project's constants (<strong>MS-VBAL §3.4</strong>), and of a declaration written
/// only in dead branches, nothing, since it is not defined.
/// </para>
/// </remarks>
public static class ProgramEntryPoint
{
    /// <summary>The name of the procedure a program starts with when nothing names another.</summary>
    public const string Name = BasicProgramText.EntryPointName;

    /// <summary>
    /// Whether a module declares a procedure a program can start with.
    /// </summary>
    /// <param name="module">The module.</param>
    /// <param name="members">The members the runtime session defined for the module.</param>
    /// <param name="name">The name of the procedure; <see cref="Name"/> unless the run names another. VBA's names are compared without regard to case.</param>
    public static bool IsDeclaredBy(VBModuleSymbol module, IEnumerable<VBTypeMemberSymbol> members, string name = Name)
        => module is VBStandardModuleSymbol && members.Any(member => IsEntryPoint(member, name));

    /// <summary>
    /// Whether a member is a procedure a program can start with: a <c>Sub</c> of that name that the project can call, and that takes no argument or one
    /// <c>ParamArray</c>.
    /// </summary>
    /// <remarks>
    /// A <c>Declare Sub</c> is not one: it is a procedure of a library, not of the module's code.
    /// </remarks>
    /// <param name="member">The member, as the runtime session defined it.</param>
    /// <param name="name">The name of the procedure; <see cref="Name"/> unless the run names another. VBA's names are compared without regard to case.</param>
    public static bool IsEntryPoint(VBTypeMemberSymbol member, string name = Name)
        => member is VBProcedureMemberSymbol { Parameters: [] or [ParamArrayParameterSymbol] } and not VBExternalSubMemberSymbol
           && member.AccessModifier is AccessModifier.Implicit or AccessModifier.Public or AccessModifier.Global
           && string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The entry point of a project: the procedure its <c>build</c> section names (<see cref="ProjectBuild.EntryPoint"/>), or else the <see cref="Name"/>
    /// procedure of the one standard module that declares one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry point named <c>Module.Procedure</c> is looked for in that module only, and one named <c>Procedure</c> in every standard module. Either way, a
    /// procedure is one only when <see cref="IsEntryPoint"/> says so, in a standard module (<see cref="IsDeclaredBy"/>); names are compared without regard to
    /// case. A module that is not a standard module is passed over.
    /// </para>
    /// <para>
    /// A library has no entry point (<see cref="ProjectOutputType.Library"/>): it is refused whatever its modules declare.
    /// </para>
    /// </remarks>
    /// <param name="build">The project's <c>build</c> section (<see cref="RDCoreProject.Build"/>); <see langword="null"/> for a project file that has none.</param>
    /// <param name="modules">The project's modules, in the order a refusal names them, each with the members the runtime session defined for it.</param>
    /// <returns>The module and procedure the program starts at, or why the project has none (<see cref="ProgramEntryPointResolution.Refusal"/>).</returns>
    public static ProgramEntryPointResolution Of(ProjectBuild? build, IEnumerable<(VBModuleSymbol Module, IEnumerable<VBTypeMemberSymbol> Members)> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (build is { IsLibrary: true })
        {
            return new ProgramEntryPointResolution { Refusal = ProgramEntryPointRefusal.Library };
        }

        var (named, inModule) = build?.EntryPoint?.Split('.', 2) switch
        {
            [var module, var procedure] => (procedure, module),
            [var procedure] => (procedure, (string?)null),
            _ => (Name, null),
        };

        var found = new List<(VBModuleSymbol Module, VBTypeMemberSymbol Procedure)>();
        foreach (var (module, members) in modules)
        {
            if (module is not VBStandardModuleSymbol || (inModule is not null && !string.Equals(module.Name, inModule, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (members.FirstOrDefault(member => IsEntryPoint(member, named)) is { } procedure)
            {
                found.Add((module, procedure));
            }
        }

        return found switch
        {
            [var (module, procedure)] => new ProgramEntryPointResolution { Module = module, Procedure = procedure },
            [] => new ProgramEntryPointResolution { Refusal = ProgramEntryPointRefusal.NotFound },
            _ => new ProgramEntryPointResolution { Refusal = ProgramEntryPointRefusal.Ambiguous, Candidates = [.. found.Select(entry => entry.Module)] },
        };
    }
}

/// <summary>
/// Why a project has no entry point (<see cref="ProgramEntryPoint.Of"/>).
/// </summary>
public enum ProgramEntryPointRefusal
{
    /// <summary>
    /// The project is a library (<see cref="ProjectOutputType.Library"/>), which is not run.
    /// </summary>
    Library,

    /// <summary>
    /// No standard module declares the procedure the project names, or a <c>Sub Main</c> when it names none.
    /// </summary>
    NotFound,

    /// <summary>
    /// More than one standard module declares it (<see cref="ProgramEntryPointResolution.Candidates"/>).
    /// </summary>
    Ambiguous,
}

/// <summary>
/// The entry point of a project (<see cref="ProgramEntryPoint.Of"/>): the module and procedure its program starts at, or why it has none.
/// </summary>
public sealed record class ProgramEntryPointResolution
{
    /// <summary>
    /// The standard module the entry point is in; <see langword="null"/> when the project has none.
    /// </summary>
    public VBModuleSymbol? Module { get; init; }

    /// <summary>
    /// The procedure the program starts at; <see langword="null"/> when the project has none.
    /// </summary>
    public VBTypeMemberSymbol? Procedure { get; init; }

    /// <summary>
    /// Why the project has no entry point; <see langword="null"/> when it has one.
    /// </summary>
    public ProgramEntryPointRefusal? Refusal { get; init; }

    /// <summary>
    /// The standard modules that each declare the entry point, in the order they were given, when more than one does
    /// (<see cref="ProgramEntryPointRefusal.Ambiguous"/>); empty otherwise.
    /// </summary>
    public IReadOnlyList<VBModuleSymbol> Candidates { get; init; } = [];

    /// <summary>
    /// Whether the project has an entry point: <see cref="Module"/> and <see cref="Procedure"/> are not <see langword="null"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(Module), nameof(Procedure))]
    public bool IsFound => Module is not null && Procedure is not null;
}
