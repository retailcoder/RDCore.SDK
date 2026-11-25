using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.Symbols.Abstract;

/// <summary>
/// A <c>Symbol</c> that is bound to a workspace document <c>Location</c>.
/// </summary>
/// <param name="WorkspaceRoot">A <c>Uri</c> representing the absolute path to the library or project workspace.</param>
/// <param name="ParentUri">The <c>Uri</c> of the parent symbol.</param>
/// <param name="Name">The name of the symbol.</param>
/// <param name="Scope">The allocation scope of this symbol.</param>
/// <param name="Kind">A <c>SymbolKind</c> (extensible) metadata value describing the kind of symbol.</param>
/// <param name="Range">The entire document <c>Range</c> belonging to this symbol.</param>
/// <param name="SelectionRange">The specific document <c>Range</c> to highlight when this symbol is selected, usually the symbol's <em>identifier</em> name if applicable.</param>
public abstract record class WorkspaceSymbol(Uri WorkspaceRoot, Uri ParentUri, string Name, ScopeKind Scope, SymbolKindExt Kind,
    SourceRange Range, SourceRange SelectionRange) : Symbol(WorkspaceRoot, ParentUri, Name, Scope, Kind)
{
    /// <summary>
    /// Every source site where this symbol is declared, in source order: populated only when the
    /// same name is declared in more than one conditional-compilation branch. Empty for the common
    /// single-declaration case, in which <see cref="WorkspaceSymbol.Range"/> and
    /// <see cref="WorkspaceSymbol.SelectionRange"/> are the sole site.
    /// </summary>
    public ImmutableArray<SymbolDefinition> Definitions { get; init; } = [];

    /// <summary>
    /// The symbol as each other conditional-compilation branch declares it, in source order: a declaration written once per branch of a <c>#If</c> block may
    /// declare a different type, other parameters, another kind of procedure in each (<strong>MS-VBAL §3.4</strong>). Empty for a symbol declared once.
    /// </summary>
    /// <remarks>
    /// The symbol itself is what the first branch declares, whatever the constants evaluate to. Whoever knows the constants defines the one of the live branch
    /// in its place (<see cref="LiveDeclaration"/>).
    /// </remarks>
    public ImmutableArray<WorkspaceSymbol> Alternatives { get; init; } = [];

    /// <summary>
    /// The document the symbol is written in: the <see cref="SourceLocation.Uri"/> of its declaration's nodes, which is the file's and not the module's
    /// identity (<see cref="Symbol.Uri"/>). Each symbol of a module written in several files has the file it is written in. <see langword="null"/> for a
    /// symbol that was not read from a document, such as a referenced library's.
    /// </summary>
    /// <remarks>
    /// <see cref="Definitions"/> are in the same document: they are branches of one <c>#If</c> block, which is written in one file. The locals, parameters
    /// and constants of a procedure are in the procedure's document.
    /// </remarks>
    public Uri? DocumentUri { get; init; }

    /// <summary>
    /// Where the symbol is written: its <see cref="DocumentUri"/> and its <see cref="Range"/>; <see langword="null"/> when it has no document.
    /// </summary>
    public SourceLocation? Location => DocumentUri is { } document ? new SourceLocation(document, Range) : null;

    /// <summary>
    /// The declaration of the live branch, of this symbol and its <see cref="Alternatives"/>: the first whose site is in no dead range of the module, with
    /// every site of <see cref="Definitions"/> marked live or dead; <see langword="null"/> when every site is in a dead range, and the symbol is not declared.
    /// A site with no <see cref="DocumentUri"/> is in no branch, and is live.
    /// </summary>
    /// <param name="isDead">Whether what is written at a location (its document, and the start of its range) is in a branch the constants make dead.</param>
    public WorkspaceSymbol? LiveDeclaration(Func<SourceLocation, bool> isDead)
    {
        ArgumentNullException.ThrowIfNull(isDead);
        bool IsDead(Uri? document, SourceRange range) => document is not null && isDead(new SourceLocation(document, range));

        var live = (Alternatives.IsDefaultOrEmpty ? [this] : Alternatives.Prepend(this)).FirstOrDefault(site => !IsDead(site.DocumentUri, site.Range));
        if (live is null || Definitions.IsDefaultOrEmpty)
        {
            return live;
        }

        return live with
        {
            Definitions = [.. Definitions.Select(definition
                => definition with { State = IsDead(DocumentUri, definition.Range) ? DefinitionState.Dead : DefinitionState.Live })],
            Alternatives = [],
        };
    }

    /// <summary>
    /// Every declaration site of this symbol: <see cref="Definitions"/> when it is populated,
    /// otherwise a single synthesized <see cref="DefinitionState.Live"/> site over
    /// <see cref="WorkspaceSymbol.Range"/>. Use this when a caller needs to treat single- and
    /// multi-branch symbols uniformly.
    /// </summary>
    public IEnumerable<SymbolDefinition> AllDefinitions => Definitions.IsDefaultOrEmpty
        ? [new SymbolDefinition(Range, SelectionRange, DefinitionState.Live)]
        : Definitions;

    /// <summary>
    /// The primary declaration span: the first <see cref="DefinitionState.Live"/> site, else the
    /// first site, else <see cref="WorkspaceSymbol.Range"/>. LSP navigation targets the primary; when no
    /// precompiler-evaluation pass has run yet this is simply the first branch.
    /// </summary>
    public SourceRange PrimaryRange => PrimaryDefinition?.Range ?? Range;

    /// <summary>
    /// The primary selection span, chosen the same way as <see cref="PrimaryRange"/>.
    /// </summary>
    public SourceRange PrimarySelectionRange => PrimaryDefinition?.SelectionRange ?? SelectionRange;

    private SymbolDefinition? PrimaryDefinition => Definitions.IsDefaultOrEmpty
        ? null
        : Definitions.FirstOrDefault(definition => definition.State == DefinitionState.Live) ?? Definitions[0];
}

/// <summary>
/// A <c>Symbol</c> that is <strong>not bound</strong> to a workspace document <c>Location</c>.
/// </summary>
/// <param name="WorkspaceRoot">A <c>Uri</c> representing the absolute path to the library or project workspace.</param>
/// <param name="ParentUri">The <c>Uri</c> of the parent symbol.</param>
/// <param name="Name">The name of the symbol.</param>
/// <param name="Scope">The allocation scope of this symbol.</param>
/// <param name="Kind">A <c>SymbolKind</c> (extensible) metadata value describing the kind of symbol.</param>
public abstract record class UnboundSymbol(Uri WorkspaceRoot, Uri ParentUri, string Name, ScopeKind Scope, SymbolKindExt Kind) 
    : Symbol(WorkspaceRoot, ParentUri, Name, Scope, Kind)
{ }
