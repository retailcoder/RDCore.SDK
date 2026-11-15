namespace RDCore.SDK.Platform.Protocol;

/// <summary>
/// A constant on the wire: the value of a data row's argument, of a fake's answer, or of an argument a faked call was given.
/// </summary>
/// <remarks>
/// The value is text in the invariant culture, written and read the same way on both ends:
/// <list type="table">
/// <listheader><term><see cref="TypeName"/></term><description><see cref="Value"/></description></listheader>
/// <item><term><c>Empty</c>, <c>Null</c></term><description><see langword="null"/>.</description></item>
/// <item><term><c>Boolean</c></term><description><c>True</c> or <c>False</c>.</description></item>
/// <item><term><c>Byte</c>, <c>Integer</c>, <c>Long</c>, <c>LongLong</c></term><description>Invariant integer text.</description></item>
/// <item><term><c>Single</c>, <c>Double</c></term><description>Round-trip (<c>R</c>) text.</description></item>
/// <item><term><c>Currency</c>, <c>Decimal</c></term><description>Invariant decimal text, with no exponent.</description></item>
/// <item><term><c>Date</c></term><description>
/// ISO 8601 <c>yyyy-MM-ddTHH:mm:ss</c>. The date part alone is midnight; the time part alone (<c>HH:mm:ss</c>) is on 1899-12-30.
/// </description></item>
/// <item><term><c>String</c></term><description>The string itself.</description></item>
/// <item><term><c>Error</c></term><description>The error number, as <c>CVErr</c> takes it (<strong>MS-VBAL §6.1.2.3.1.14</strong>).</description></item>
/// <item><term><c>Object</c></term><description>
/// In an <see cref="FakeInvocationRecord"/> only: the object's <c>TypeName</c>, or <see langword="null"/> for <c>Nothing</c>.
/// </description></item>
/// </list>
/// <para>
/// Arrays and user-defined types do not travel: a data row or an answer that needs one is refused with a reason, and an argument of either in a record is
/// written with its <see cref="TypeName"/> and a <see langword="null"/> value.
/// </para>
/// </remarks>
/// <param name="TypeName">The name of the value's type, such as <c>Long</c>.</param>
/// <param name="Value">The value as text, as the table in the remarks says for <paramref name="TypeName"/>.</param>
public sealed record class RunValue(string TypeName, string? Value);

/// <summary>
/// The fakes of a run: which external calls are answered in place of running, and how.
/// </summary>
/// <remarks>
/// A call is matched against <see cref="Rows"/> in order, and the first row whose target, arguments and invocation match it acts. A call of a target no row
/// names runs as it would without fakes, and is not recorded.
/// </remarks>
/// <param name="Rows">The rows, in the order they are tried.</param>
public sealed record class FakesTable(IReadOnlyList<FakeRow> Rows)
{
    /// <summary>
    /// The table with no rows: every call runs.
    /// </summary>
    public static FakesTable Empty { get; } = new([]);
}

/// <summary>
/// One fake: which calls of a target it matches, and what it does with them.
/// </summary>
/// <param name="Target">
/// The member, qualified by its library and module: <c>VBA.Interaction.MsgBox</c>. A call of it is recorded (<see cref="FakeInvocationRecord"/>) whatever the
/// row does with it.
/// </param>
/// <param name="Arguments">
/// What the arguments must be, by parameter index. A parameter no matcher names matches any argument, and so does a row with no matchers.
/// </param>
/// <param name="When">Which calls of <paramref name="Target"/> the row matches: every one, or the nth.</param>
/// <param name="Action">What a call the row matches does.</param>
public sealed record class FakeRow(string Target, IReadOnlyList<FakeArgumentMatcher> Arguments, FakeInvocation When, FakeAction Action);

/// <summary>
/// How a <see cref="FakeArgumentMatcher"/> compares an argument.
/// </summary>
public enum FakeMatch
{
    /// <summary>
    /// Any argument matches, a missing optional one included.
    /// </summary>
    Any,

    /// <summary>
    /// The argument is equal to the matcher's value, as the <c>=</c> operator compares them (<strong>MS-VBAL §5.6.9.5</strong>), with
    /// <c>Option Compare Binary</c>.
    /// </summary>
    Equals,

    /// <summary>
    /// The argument, as a string, matches the matcher's value as a pattern, as the <c>Like</c> operator matches it (<strong>MS-VBAL §5.6.9.6</strong>), with
    /// <c>Option Compare Binary</c>.
    /// </summary>
    Like,

    /// <summary>
    /// The argument lies between the matcher's value and its upper bound, both included, as the <c>&gt;=</c> and <c>&lt;=</c> operators compare them
    /// (<strong>MS-VBAL §5.6.9.5</strong>).
    /// </summary>
    Range,
}

/// <summary>
/// What one argument of a call must be for a <see cref="FakeRow"/> to match it.
/// </summary>
/// <param name="Index">The parameter's zero-based index in the member's declared parameter list.</param>
/// <param name="Match">How the argument is compared.</param>
/// <param name="Value">
/// The value compared against: the value for <see cref="FakeMatch.Equals"/>, the pattern for <see cref="FakeMatch.Like"/>, the lower bound for
/// <see cref="FakeMatch.Range"/>. <see langword="null"/> for <see cref="FakeMatch.Any"/>.
/// </param>
/// <param name="Upper">The upper bound for <see cref="FakeMatch.Range"/>; <see langword="null"/> otherwise.</param>
public sealed record class FakeArgumentMatcher(int Index, FakeMatch Match, RunValue? Value = null, RunValue? Upper = null);

/// <summary>
/// The kind of a <see cref="FakeInvocation"/>: every call of the target, or the nth.
/// </summary>
public enum FakeInvocationKind
{
    /// <summary>
    /// Every call whose arguments match.
    /// </summary>
    Every,

    /// <summary>
    /// The nth call of the target, counted over every call of it in the run, whose arguments match.
    /// </summary>
    Nth,
}

/// <summary>
/// Which calls of its target a <see cref="FakeRow"/> matches.
/// </summary>
/// <param name="Kind">Every call, or the nth.</param>
/// <param name="N">The one-based ordinal of the call, for <see cref="FakeInvocationKind.Nth"/>; <c>0</c> otherwise.</param>
public sealed record class FakeInvocation(FakeInvocationKind Kind, int N = 0)
{
    /// <summary>
    /// Every call of the target.
    /// </summary>
    public static FakeInvocation Every { get; } = new(FakeInvocationKind.Every);
}

/// <summary>
/// What a matched call does.
/// </summary>
public enum FakeActionKind
{
    /// <summary>
    /// The call is not run, and returns the action's value.
    /// </summary>
    Return,

    /// <summary>
    /// The call is not run, and raises the action's error at its call site.
    /// </summary>
    Raise,

    /// <summary>
    /// The call runs as it would without fakes. It is still recorded.
    /// </summary>
    PassThrough,
}

/// <summary>
/// What a call a <see cref="FakeRow"/> matches does.
/// </summary>
/// <param name="Kind">Whether the call returns, raises, or runs.</param>
/// <param name="Value">
/// For <see cref="FakeActionKind.Return"/>, the value returned, Let-coerced to the member's declared type (<strong>MS-VBAL §5.5.1.2</strong>):
/// <see langword="null"/> for a <c>Sub</c>, or for the member type's default value. <see langword="null"/> otherwise.
/// </param>
/// <param name="ErrorNumber">For <see cref="FakeActionKind.Raise"/>, the number of the error raised; <c>0</c> otherwise.</param>
/// <param name="ErrorDescription">
/// For <see cref="FakeActionKind.Raise"/>, the error's description: <see langword="null"/> for the description of <paramref name="ErrorNumber"/>.
/// </param>
/// <param name="Assignments">
/// For <see cref="FakeActionKind.Return"/> and <see cref="FakeActionKind.Raise"/>, the <c>ByRef</c> arguments the call writes before it returns or raises.
/// </param>
public sealed record class FakeAction(
    FakeActionKind Kind,
    RunValue? Value = null,
    int ErrorNumber = 0,
    string? ErrorDescription = null,
    IReadOnlyList<FakeAssignment>? Assignments = null)
{
    /// <summary>
    /// The <c>ByRef</c> arguments the call writes, in order. Empty when it writes none.
    /// </summary>
    public IReadOnlyList<FakeAssignment> Assignments { get; init; } = Assignments ?? [];
}

/// <summary>
/// A <c>ByRef</c> argument that a faked call writes.
/// </summary>
/// <param name="Index">The parameter's zero-based index in the member's declared parameter list.</param>
/// <param name="Value">The value written, Let-assigned to the argument (<strong>MS-VBAL §5.5.1.2</strong>).</param>
public sealed record class FakeAssignment(int Index, RunValue Value);

/// <summary>
/// A change to a run's fakes while it runs: what a test written in VBA sets up (<c>Fakes.MsgBox.Returns vbYes</c>) reaches the table this way, with the
/// answer of the call that set it up.
/// </summary>
/// <param name="Reset">Whether every row is removed before <paramref name="Add"/> is applied.</param>
/// <param name="Add">The rows added, after the rows the table keeps.</param>
/// <param name="Remove">The targets whose rows are removed, before <paramref name="Add"/> is applied.</param>
public sealed record class FakesTableDelta(bool Reset, IReadOnlyList<FakeRow> Add, IReadOnlyList<string> Remove);

/// <summary>
/// One call of a target a row of the fakes names: what a test verifies a call against.
/// </summary>
/// <param name="Target">The member called, as <see cref="FakeRow.Target"/> names it.</param>
/// <param name="Ordinal">The one-based ordinal of the call among the run's calls of <paramref name="Target"/>.</param>
/// <param name="Arguments">The arguments, in the member's declared parameter order; a missing optional argument is <c>Empty</c> with a null value.</param>
/// <param name="Module">The programmatic name of the module the call is written in.</param>
/// <param name="Procedure">The name of the procedure the call is written in.</param>
/// <param name="Line">The zero-based line of the call in the source of <paramref name="Module"/>, or <c>-1</c> when it has no location.</param>
/// <param name="Character">The zero-based character of the call within <paramref name="Line"/>, or <c>-1</c>.</param>
/// <param name="Answered">
/// Whether a row answered the call (<see cref="FakeActionKind.Return"/> or <see cref="FakeActionKind.Raise"/>); <see langword="false"/> when it ran.
/// </param>
public sealed record class FakeInvocationRecord(
    string Target,
    int Ordinal,
    IReadOnlyList<RunValue> Arguments,
    string Module,
    string Procedure,
    int Line,
    int Character,
    bool Answered);
