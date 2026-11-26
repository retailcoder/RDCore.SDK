using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using System.Collections.Immutable;

namespace RDCore.SDK.Semantics;

/// <summary>
/// The shape of the <c>#If</c> blocks of a module (<strong>MS-VBAL §3.4</strong>): where the branches of each block are written, whatever they evaluate to.
/// </summary>
/// <remarks>
/// A name can be declared in each branch of a <c>#If</c> block and be one declaration, whichever branch compiles: the branches are alternatives of one another.
/// A name declared twice in the same branch - or twice with no <c>#If</c> at all - is not an alternative of itself, but a duplicate. Telling them apart takes no
/// constant to be evaluated, only where each branch begins and ends; it is the structure that the evaluation of the constants (<c>PrecompilerLiveBranchEvaluator</c>)
/// then chooses a branch of.
/// <para>
/// A branch is where its file and its range say, as a <see cref="SourceLocation"/>: a module written in several files repeats positions, and what is written
/// at a position of one file is in no branch of another. A block is written in one file, with all its branches.
/// </para>
/// </remarks>
public sealed class ConditionalCompilationBlocks
{
    private readonly ImmutableArray<ImmutableArray<SourceLocation>> _blocks;
    private readonly ImmutableArray<(int Branches, bool HasElse)> _shapes;

    // of each block, which of its branches each body is: the #If is 0, each #ElseIf the next, then the #Else. A branch that holds nothing has no body.
    private readonly ImmutableArray<ImmutableArray<int>> _bodied;

    private ConditionalCompilationBlocks(
        ImmutableArray<ImmutableArray<SourceLocation>> blocks, ImmutableArray<(int Branches, bool HasElse)> shapes, ImmutableArray<ImmutableArray<int>> bodied)
        => (_blocks, _shapes, _bodied) = (blocks, shapes, bodied);

    /// <summary>
    /// A module with no <c>#If</c> block: nothing is an alternative of anything.
    /// </summary>
    public static ConditionalCompilationBlocks None { get; } = new([], [], []);

    /// <summary>
    /// The blocks of a module, read off its precompiler trivia, nested ones included, each where its nodes' <see cref="SyntaxNode.SourceLocation"/> say.
    /// </summary>
    /// <param name="precompilerTrivia">The module's <c>ModuleParseResult.PrecompilerTrivia</c>, or the trivia of each file of a module written in
    /// several.</param>
    public static ConditionalCompilationBlocks Of(ImmutableArray<SyntaxNode> precompilerTrivia)
    {
        if (precompilerTrivia.IsDefaultOrEmpty)
        {
            return None;
        }

        var blocks = ImmutableArray.CreateBuilder<ImmutableArray<SourceLocation>>();
        var shapes = ImmutableArray.CreateBuilder<(int Branches, bool HasElse)>();
        var bodied = ImmutableArray.CreateBuilder<ImmutableArray<int>>();
        foreach (var node in precompilerTrivia)
        {
            Collect(node, blocks, shapes, bodied);
        }

        return new(blocks.ToImmutable(), shapes.ToImmutable(), bodied.ToImmutable());
    }

    /// <summary>
    /// Whether a block has an <c>#Else</c> branch, so that exactly one of its branches is compiled, whatever the constants are (<strong>MS-VBAL §3.4</strong>).
    /// A block without one compiles none of its branches when no condition is true.
    /// </summary>
    /// <param name="block">The ordinal of the block, as <see cref="BranchesAt"/> gives it with the same <paramref name="counted"/>.</param>
    /// <param name="counted">Which blocks are counted, as <see cref="BranchesAt"/> counts them.</param>
    /// <exception cref="ArgumentOutOfRangeException">No counted block has that ordinal.</exception>
    public bool HasElse(int block, Func<SourceLocation, bool> counted) => ShapeOf(block, counted).HasElse;

    /// <summary>
    /// How many branches a block has: its <c>#If</c>, each <c>#ElseIf</c>, and its <c>#Else</c>, whatever they hold, an empty one included. A branch that holds
    /// nothing holds no position: <see cref="BranchesAt"/> gives no ordinal of it.
    /// </summary>
    /// <param name="block">The ordinal of the block, as <see cref="BranchesAt"/> gives it with the same <paramref name="counted"/>.</param>
    /// <param name="counted">Which blocks are counted, as <see cref="BranchesAt"/> counts them.</param>
    /// <exception cref="ArgumentOutOfRangeException">No counted block has that ordinal.</exception>
    public int BranchCount(int block, Func<SourceLocation, bool> counted) => ShapeOf(block, counted).Branches;

    private (int Branches, bool HasElse) ShapeOf(int block, Func<SourceLocation, bool> counted)
    {
        var ordinal = 0;
        for (var index = 0; index < _blocks.Length; index++)
        {
            var branches = _blocks[index];
            if (branches.IsDefaultOrEmpty || !counted(Extent(branches)))
            {
                continue;
            }

            if (ordinal++ == block)
            {
                return _shapes[index];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(block), block, null);
    }

    /// <summary>
    /// Whether the declarations at <paramref name="first"/> and <paramref name="second"/> are in different branches of one block, so that at most one of them is ever compiled.
    /// Declarations in two files are never alternatives: no block is written in both.
    /// </summary>
    /// <param name="first">Where the first is declared: its file, and the start of its range.</param>
    /// <param name="second">Where the second is declared: its file, and the start of its range.</param>
    public bool AreAlternatives(SourceLocation first, SourceLocation second)
    {
        foreach (var branches in _blocks)
        {
            var inFirst = BranchAt(branches, first);
            var inSecond = BranchAt(branches, second);
            if (inFirst >= 0 && inSecond >= 0 && inFirst != inSecond)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether what is written at <paramref name="location"/> is in a branch of a block, and so compiled only when that branch is live.
    /// </summary>
    /// <param name="location">Where it is written: its file, and the start of its range.</param>
    public bool IsConditional(SourceLocation location) => _blocks.Any(branches => BranchAt(branches, location) >= 0);

    /// <summary>
    /// The branches that hold what is written at <paramref name="location"/>, the outermost first: for each block that holds it, which block it is and which
    /// of its branches. Neither says where the block is written, only how many blocks come before it and how many of its branches that hold something come
    /// before the one that holds it.
    /// </summary>
    /// <param name="location">Where it is written: its file, and the start of its range.</param>
    /// <param name="counted">Which blocks are counted, by their file and the range from the start of the first branch to the end of the last: a block that
    /// is not counted holds nothing, and is not counted in the ordinal of those after it.</param>
    /// <returns>For each block that holds the position, its ordinal among the blocks counted, in the order they are written, and the ordinal of the branch
    /// among the block's branches that hold something, in the order they are written (the <c>#If</c>, each <c>#ElseIf</c>, then the <c>#Else</c>): a branch
    /// that holds nothing is not counted. Nothing when no counted block holds the position.</returns>
    /// <remarks>
    /// Two declarations with the same branches are compiled together, whatever the constants are; a declaration moved into a branch, out of one or to another
    /// is compiled when other constants say so (<strong>MS-VBAL §3.4</strong>).
    /// </remarks>
    public IReadOnlyList<(int Block, int Branch)> BranchesAt(SourceLocation location, Func<SourceLocation, bool> counted)
    {
        var branchesAt = new List<(int Block, int Branch)>();
        var ordinal = 0;
        foreach (var branches in _blocks)
        {
            if (branches.IsDefaultOrEmpty || !counted(Extent(branches)))
            {
                continue;
            }

            if (BranchAt(branches, location) is var branch and >= 0)
            {
                branchesAt.Add((ordinal, branch));
            }

            ordinal++;
        }

        return branchesAt;
    }

    /// <summary>
    /// The branch each dead location is, as <see cref="BranchesAt"/> numbers the blocks and their branches with every block counted: the branch that holds
    /// all of the location, the innermost one when branches are nested (<strong>MS-VBAL §3.4</strong>). A location that no branch holds gives none.
    /// </summary>
    /// <param name="dead">Where the dead branches are, each in the file it is written in, as the environment host says them
    /// (<see cref="Platform.Components.DefineSymbolsResult.DeadBranches"/>) for the parse these blocks were read off.</param>
    /// <returns>Each branch once, in the order of <paramref name="dead"/>.</returns>
    /// <remarks>
    /// A branch is said by its ordinals, not where it is written, so it is the same branch in an edited parse of the module, whose positions are not the
    /// ones evaluated, as long as the edit leaves the blocks their shape (<see cref="HasShapeOf"/>). <see cref="DeadTest"/> reads them back.
    /// </remarks>
    public IReadOnlyList<(int Block, int Branch)> DeadOrdinals(IEnumerable<SourceLocation> dead)
    {
        var ordinals = new List<(int Block, int Branch)>();
        foreach (var location in dead)
        {
            (int Block, int Branch)? innermost = null;
            var ordinal = 0;

            // a block nested in a branch comes after the block of that branch: the last that holds the location is the innermost.
            foreach (var branches in _blocks)
            {
                if (branches.IsDefaultOrEmpty)
                {
                    continue;
                }

                if (BranchHolding(branches, location) is var branch and >= 0)
                {
                    innermost = (ordinal, branch);
                }

                ordinal++;
            }

            if (innermost is { } found && !ordinals.Contains(found))
            {
                ordinals.Add(found);
            }
        }

        return ordinals;
    }

    /// <summary>
    /// Whether what is written at a location is in a dead branch of these blocks: in one of the <paramref name="dead"/> branches, or in a branch nested in
    /// one, whatever the nested branch is said to be (<strong>MS-VBAL §3.4</strong>: a dead branch is not part of the module, and nothing in it is).
    /// </summary>
    /// <param name="dead">The dead branches, as <see cref="DeadOrdinals"/> gives them for a parse whose blocks have the shape of these
    /// (<see cref="HasShapeOf"/>).</param>
    /// <returns>A test of where something is written, its file and the start of its range: false everywhere when no branch is dead.</returns>
    public Func<SourceLocation, bool> DeadTest(IReadOnlyCollection<(int Block, int Branch)> dead)
    {
        if (dead.Count == 0)
        {
            return static _ => false;
        }

        var branches = dead.ToHashSet();
        return location => BranchesAt(location, static _ => true).Any(branches.Contains);
    }

    /// <summary>
    /// Whether two parses of a module have blocks of the same shape (<strong>MS-VBAL §3.4</strong>): as many blocks, in the same order, each in the same
    /// file, nested in the same branch, with the same branches, and each branch holding something in both or in neither. The branches of one are then the
    /// branches of the other, ordinal for ordinal, wherever an edit moved them in their file.
    /// </summary>
    /// <param name="other">The blocks of the other parse.</param>
    public bool HasShapeOf(ConditionalCompilationBlocks other)
    {
        if (_blocks.Length != other._blocks.Length)
        {
            return false;
        }

        for (var index = 0; index < _blocks.Length; index++)
        {
            var (mine, theirs) = (_blocks[index], other._blocks[index]);
            if (_shapes[index] != other._shapes[index] || !_bodied[index].SequenceEqual(other._bodied[index]))
            {
                return false;
            }

            if (mine.IsDefaultOrEmpty)
            {
                continue;
            }

            if (mine[0].Uri != theirs[0].Uri || ParentOf(index) != other.ParentOf(index))
            {
                return false;
            }
        }

        return true;
    }

    // the branch a block is nested in, by the index of its block: the nearest block before it that holds it is the innermost.
    private (int Block, int Branch)? ParentOf(int index)
    {
        var extent = Extent(_blocks[index]);
        for (var before = index - 1; before >= 0; before--)
        {
            if (!_blocks[before].IsDefaultOrEmpty && BranchHolding(_blocks[before], extent) is var branch and >= 0)
            {
                return (before, branch);
            }
        }

        return null;
    }

    // the branch that holds all of a location, in its file; none when no branch does.
    private static int BranchHolding(ImmutableArray<SourceLocation> branches, SourceLocation location)
    {
        for (var index = 0; index < branches.Length; index++)
        {
            if (branches[index].Uri == location.Uri && location.Range.Start >= branches[index].Range.Start && location.Range.End <= branches[index].Range.End)
            {
                return index;
            }
        }

        return -1;
    }

    // a block, from the start of its first branch to the end of its last, in the file it is written in.
    private static SourceLocation Extent(ImmutableArray<SourceLocation> branches)
        => branches[0] with { Range = new SourceRange(branches[0].Range.Start, branches[^1].Range.End) };

    // the range of a branch ends at the start of its last token, and is inclusive of it; a branch of another file holds nothing of this one.
    private static int BranchAt(ImmutableArray<SourceLocation> branches, SourceLocation location)
    {
        var position = location.Range.Start;
        for (var index = 0; index < branches.Length; index++)
        {
            if (branches[index].Uri == location.Uri && position >= branches[index].Range.Start && position <= branches[index].Range.End)
            {
                return index;
            }
        }

        return -1;
    }

    private static void Collect(
        SyntaxNode node, ImmutableArray<ImmutableArray<SourceLocation>>.Builder blocks, ImmutableArray<(int Branches, bool HasElse)>.Builder shapes,
        ImmutableArray<ImmutableArray<int>>.Builder bodied)
    {
        switch (node)
        {
            case PrecompilerIfBlockStatementNode ifBlock:
                var bodies = BranchBodies(ifBlock);
                blocks.Add([.. bodies.Select(each => each.Body.SourceLocation)]);
                bodied.Add([.. bodies.Select(each => each.Branch)]);

                // the #If, each #ElseIf and the #Else, whether they hold anything or not.
                var hasElse = ifBlock.Children.OfType<PrecompilerElseBlockStatementNode>().Any();
                shapes.Add((1 + ifBlock.Children.OfType<PrecompilerElseIfBlockStatementNode>().Count() + (hasElse ? 1 : 0), hasElse));

                // a branch may itself contain a block.
                foreach (var (_, body) in bodies)
                {
                    foreach (var child in body.Children)
                    {
                        Collect(child, blocks, shapes, bodied);
                    }
                }

                break;
            case PrecompilerTriviaNode trivia:
                foreach (var child in trivia.Children)
                {
                    Collect(child, blocks, shapes, bodied);
                }

                break;
        }
    }

    // one body per #If/#ElseIf/#Else branch that holds something, in source order, with which branch of the block it is.
    private static List<(int Branch, PrecompilerTriviaNode Body)> BranchBodies(PrecompilerIfBlockStatementNode ifBlock)
    {
        var bodies = new List<(int Branch, PrecompilerTriviaNode Body)>();
        if (ifBlock.Children is [PrecompilerInlineIfStatementNode, PrecompilerTriviaNode body, ..])
        {
            bodies.Add((0, body));
        }

        var elseIfs = ifBlock.Children.OfType<PrecompilerElseIfBlockStatementNode>().ToList();
        for (var index = 0; index < elseIfs.Count; index++)
        {
            if (elseIfs[index].Children.OfType<PrecompilerTriviaNode>().FirstOrDefault() is { } elseIfBody)
            {
                bodies.Add((1 + index, elseIfBody));
            }
        }

        if (ifBlock.Children.OfType<PrecompilerElseBlockStatementNode>().FirstOrDefault()?.Children.OfType<PrecompilerTriviaNode>().FirstOrDefault() is { } elseBody)
        {
            bodies.Add((1 + elseIfs.Count, elseBody));
        }

        return bodies;
    }
}
