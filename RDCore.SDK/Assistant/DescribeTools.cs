using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Server.Assistant;
using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Assistant;

// the results of describe_at and outline: the facts rdcore/semantics/describe and /outline answer, with 1-based positions and paths from the workspace
// root, as every tool says them.

/// <summary>
/// The structured result of <see cref="CoreAssistantTools.DescribeAt"/>: a <see cref="DescribeResult"/>, with 1-based positions and paths from the
/// workspace root.
/// </summary>
public sealed record class DescribeToolResult
{
    /// <summary>
    /// The symbol the name at the position binds to; <see langword="null"/> when no name is there, or it binds to nothing known.
    /// </summary>
    public ToolSymbolDescription? Symbol { get; init; }

    /// <summary>
    /// The facts of the innermost expression whose location holds the position; <see langword="null"/> when the host has none.
    /// </summary>
    public ToolExpressionDescription? Expression { get; init; }

    /// <summary>
    /// The name or expression the position is in; <see langword="null"/> when nothing is described.
    /// </summary>
    public TextRange? Range { get; init; }

    /// <summary>
    /// The result of the tool for an answer of <c>rdcore/semantics/describe</c>.
    /// </summary>
    /// <param name="workspaceRoot">The root of the workspace, which the paths are said from.</param>
    /// <param name="described">The answer.</param>
    public static DescribeToolResult Of(string workspaceRoot, DescribeResult described) => new()
    {
        Symbol = described.Symbol is { } symbol ? ToolSymbolDescription.Of(workspaceRoot, symbol) : null,
        Expression = described.Expression is { } expression ? ToolExpressionDescription.Of(expression) : null,
        Range = described.Range is { } range ? TextRange.Of(range) : null,
    };
}

/// <summary>
/// A <see cref="SymbolDescription"/>, with a 1-based location and a path from the workspace root.
/// </summary>
public sealed record class ToolSymbolDescription : DescribedSymbol
{
    /// <summary>
    /// A description with no facts set: the one a reader fills.
    /// </summary>
    public ToolSymbolDescription() { }

    [SetsRequiredMembers]
    private ToolSymbolDescription(DescribedSymbol facts) : base(facts) { }

    /// <summary>
    /// The name its declaration declares; <see langword="null"/> for a symbol of a library, which no document declares.
    /// </summary>
    public TextLocation? Location { get; init; }

    /// <summary>
    /// The facts of a symbol, with its location said from the workspace root.
    /// </summary>
    /// <param name="workspaceRoot">The root of the workspace, which the path is said from.</param>
    /// <param name="symbol">The symbol, as <c>rdcore/semantics/describe</c> describes it.</param>
    public static ToolSymbolDescription Of(string workspaceRoot, SymbolDescription symbol) => new(symbol)
    {
        Location = symbol.Location is { } location ? TextLocation.Of(workspaceRoot, location.Uri, location.Range) : null,
    };
}

/// <summary>
/// An <see cref="ExpressionDescription"/>, with a 1-based range.
/// </summary>
public sealed record class ToolExpressionDescription : DescribedExpression
{
    /// <summary>
    /// A description with no facts set: the one a reader fills.
    /// </summary>
    public ToolExpressionDescription() { }

    private ToolExpressionDescription(DescribedExpression facts) : base(facts) { }

    /// <summary>
    /// Where the expression is written.
    /// </summary>
    public TextRange Range { get; init; }

    /// <summary>
    /// The facts of an expression, with its range 1-based.
    /// </summary>
    /// <param name="expression">The expression, as <c>rdcore/semantics/describe</c> describes it.</param>
    public static ToolExpressionDescription Of(ExpressionDescription expression) => new(expression) { Range = TextRange.Of(expression.Range) };
}

/// <summary>
/// The structured result of <see cref="CoreAssistantTools.Outline"/>: a <see cref="ModuleOutline"/>, with 1-based ranges and the path of the document from
/// the workspace root.
/// </summary>
public sealed record class OutlineToolResult : OutlinedModule
{
    /// <summary>
    /// An outline with no facts set: the one a reader fills.
    /// </summary>
    public OutlineToolResult() { }

    [SetsRequiredMembers]
    private OutlineToolResult(OutlinedModule facts) : base(facts) { }

    /// <summary>
    /// The document: a path relative to the workspace root, or absolute when it is outside it.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The declarations of the module, at most <see cref="ToolResultLimits.OutlineEntries"/> of them.
    /// </summary>
    public IReadOnlyList<ToolOutlineEntry> Entries { get; init; } = [];

    /// <summary>
    /// How many declarations the module has, their members not counted.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Whether <see cref="Entries"/> is cut at its cap.
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// The result of the tool for an answer of <c>rdcore/semantics/outline</c>, its declarations cut at <see cref="ToolResultLimits.OutlineEntries"/>.
    /// </summary>
    /// <param name="workspaceRoot">The root of the workspace, which the path is said from.</param>
    /// <param name="outline">The answer.</param>
    public static OutlineToolResult Of(string workspaceRoot, ModuleOutline outline)
    {
        var capped = ToolResultLimits.Cap(outline.Entries.Select(ToolOutlineEntry.Of), ToolResultLimits.OutlineEntries);
        return new(outline)
        {
            Path = TextLocation.PathOf(workspaceRoot, outline.Uri),
            Entries = capped.Items,
            Total = capped.Total,
            Truncated = capped.Truncated,
        };
    }
}

/// <summary>
/// An <see cref="OutlineEntry"/>, with 1-based ranges.
/// </summary>
public sealed record class ToolOutlineEntry : OutlinedDeclaration
{
    /// <summary>
    /// An entry with no facts set: the one a reader fills.
    /// </summary>
    public ToolOutlineEntry() { }

    [SetsRequiredMembers]
    private ToolOutlineEntry(OutlinedDeclaration facts) : base(facts) { }

    /// <summary>
    /// The whole declaration.
    /// </summary>
    public TextRange Range { get; init; }

    /// <summary>
    /// The name the declaration declares.
    /// </summary>
    public TextRange SelectionRange { get; init; }

    /// <summary>
    /// The members it declares, in the order they are written: the members of a <c>Type</c> or an <c>Enum</c>.
    /// </summary>
    public IReadOnlyList<ToolOutlineEntry> Children { get; init; } = [];

    /// <summary>
    /// The facts of a declaration and of its members, with their ranges 1-based.
    /// </summary>
    /// <param name="entry">The declaration, as <c>rdcore/semantics/outline</c> outlines it.</param>
    public static ToolOutlineEntry Of(OutlineEntry entry) => new(entry)
    {
        Range = TextRange.Of(entry.Range),
        SelectionRange = TextRange.Of(entry.SelectionRange),
        Children = [.. entry.Children.Select(Of)],
    };
}
