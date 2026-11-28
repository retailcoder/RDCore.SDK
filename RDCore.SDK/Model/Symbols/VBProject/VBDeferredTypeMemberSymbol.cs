using RDCore.SDK.Model.Symbols.Abstract;
using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Types.Abstract;
using RDCore.SDK.Model.Types.Complex;
using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.Symbols.VBProject;

/// <summary>
/// A member that no statement declares, of a module or class whose members are inferred from how the code uses them
/// (<see cref="VBDeferredType.Members"/>): its type is not known (<see cref="VBUnknownType"/>), and its candidates are the types the code gives its value.
/// </summary>
/// <remarks>
/// <strong>RD-VBA addition.</strong> It is never a declaration (<see cref="IVBInferableType"/>).
/// </remarks>
/// <param name="WorkspaceRoot">The root of the workspace the member's module is in.</param>
/// <param name="ParentUri">The location of the module the member would be declared in.</param>
/// <param name="Name">The name the code uses for the member.</param>
/// <param name="Kind">The kind of member the code uses it as.</param>
public record class VBDeferredTypeMemberSymbol(Uri WorkspaceRoot, Uri ParentUri, string Name, SymbolKindExt Kind)
    : UnboundTypedSymbol(WorkspaceRoot, ParentUri, Name, ScopeKind.Module, Kind, VBUnknownType.TypeInfo), IVBDeferrableTypeMember
{
    /// <inheritdoc/>
    public ImmutableHashSet<VBType> CandidateTypes { get; init; } = [];

    /// <inheritdoc/>
    public IVBInferableType WithCandidateType(VBType vbType) => this with { CandidateTypes = CandidateTypes.MergeCandidate(vbType) };

    /// <inheritdoc/>
    public VBType? DeferredVBType { get; init; }

    /// <inheritdoc/>
    public VBDeferredTypeMemberSymbol WithDeferredVBType(VBType vbType) => this with { DeferredVBType = vbType, CandidateTypes = [vbType] };
}
