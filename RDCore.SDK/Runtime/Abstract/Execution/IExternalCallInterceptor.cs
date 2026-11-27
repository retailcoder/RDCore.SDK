using System.Collections.Immutable;
using RDCore.SDK.Model.Errors.Abstract;
using RDCore.SDK.Model.Values.Abstract;

namespace RDCore.SDK.Runtime.Abstract.Execution;

/// <summary>
/// What an <see cref="IExternalCallInterceptor"/> decided about a call: let it through, refuse it, or answer it.
/// </summary>
/// <param name="Reason">
/// Why the call was refused, in words a person reading a diagnostic needs: <see langword="null"/> when it
/// was not refused.
/// </param>
/// <param name="Answer">
/// What the call yields in place of running it: <see langword="null"/> when it was not answered.
/// </param>
public readonly record struct ExternalCallDecision(string? Reason, ExternalCallAnswer? Answer = null)
{
    /// <summary>
    /// Let the call through.
    /// </summary>
    public static ExternalCallDecision Allow { get; } = new((string?)null);

    /// <summary>
    /// Answer the call: it is not run, and the call site gets <paramref name="answer"/> as though the member had
    /// returned it.
    /// </summary>
    /// <param name="answer">What the call yields: a result or an error, and the <c>ByRef</c> arguments it writes.</param>
    public static ExternalCallDecision Answered(ExternalCallAnswer answer) => new(null, answer);

    /// <summary>
    /// Refuse the call.
    /// </summary>
    /// <param name="reason">
    /// Why: it reaches the workspace as the verbose half of a <c>Permission denied</c> run-time error, so
    /// it should say what was refused and on what grounds.
    /// </param>
    public static ExternalCallDecision Block(string reason) => new(reason);

    /// <summary>
    /// Refuse the call and stop the program: no error handler runs (<see cref="ExecutionPolicyMode.Halt"/>).
    /// </summary>
    /// <param name="reason">Why: it is the verbose half of the error the program stops with, so it should say what was refused and on what grounds.</param>
    public static ExternalCallDecision Halt(string reason) => new(reason) { IsHalted = true };

    /// <summary>
    /// Whether the call was refused, halted or not.
    /// </summary>
    public bool IsBlocked => Reason is not null;

    /// <summary>
    /// Whether the call was refused with the program stopped (<see cref="Halt"/>): <see cref="IsBlocked"/> is then <see langword="true"/> too.
    /// </summary>
    public bool IsHalted { get; init; }

    /// <summary>
    /// Whether the call was answered (<see cref="Answer"/>), and is not to be run.
    /// </summary>
    public bool IsAnswered => Answer is not null;
}

/// <summary>
/// What an answered call yields, in place of running it: the answer of a fake.
/// </summary>
/// <param name="Result">
/// The value the call returns: <see langword="null"/> for a <c>Sub</c>, and when <paramref name="Error"/> is set.
/// </param>
/// <param name="Error">
/// The error the call raises at its call site, as the member would raise its own: <see langword="null"/> when it
/// returns.
/// </param>
/// <param name="Assignments">
/// The <c>ByRef</c> arguments the call writes before it returns or raises. Empty when it writes none.
/// </param>
public sealed record class ExternalCallAnswer(
    VBTypedValue? Result,
    IVBRaisableError? Error = null,
    ImmutableArray<ExternalCallAssignment> Assignments = default)
{
    /// <summary>
    /// The <c>ByRef</c> arguments the call writes, in order. Empty when it writes none.
    /// </summary>
    public ImmutableArray<ExternalCallAssignment> Assignments { get; init; } = Assignments.IsDefault ? [] : Assignments;

    /// <summary>
    /// The answer of a call that returns <paramref name="result"/>.
    /// </summary>
    /// <param name="result">The value returned: <see langword="null"/> for a <c>Sub</c>.</param>
    public static ExternalCallAnswer Returning(VBTypedValue? result) => new(result);

    /// <summary>
    /// The answer of a call that raises <paramref name="error"/>.
    /// </summary>
    /// <param name="error">The error raised at the call site.</param>
    public static ExternalCallAnswer Raising(IVBRaisableError error) => new(null, error);
}

/// <summary>
/// A <c>ByRef</c> argument that an answered call writes.
/// </summary>
/// <param name="ArgumentIndex">
/// The argument's index in <see cref="ExternalCallRequest.Arguments"/>: the receiver of a class member's call is at
/// <c>0</c>.
/// </param>
/// <param name="Value">The value written, Let-assigned to the argument as an assignment to the parameter would be.</param>
public readonly record struct ExternalCallAssignment(int ArgumentIndex, VBTypedValue Value);

/// <summary>
/// Sees every call to something outside the workspace before it happens, and may refuse it or answer it.
/// </summary>
/// <remarks>
/// 🎯 The reason this exists: a VBA environment that can call into native libraries is a way to run
/// arbitrary code, and the historical answer to that has been for administrators to disable the whole
/// language because nothing could see what a macro actually did. This is the seam that makes the calls
/// visible — every one of them, with its arguments, before it runs — and refusable one at a time.
/// <para>
/// Interceptors are ordered, and each sees the call until one refuses or answers it: that one stops it, and
/// the interceptors after it do not see it. An interceptor is expected to be cheap and to have no opinion about
/// most calls: it runs in front of <em>every</em> external call, not only the dangerous ones.
/// </para>
/// <para>
/// An interceptor allows, refuses or answers a call (<see cref="ExternalCallDecision"/>). An answered call is not
/// run: the call site gets the <see cref="ExternalCallAnswer"/>, which is how a test fakes <c>MsgBox</c>.
/// </para>
/// </remarks>
public interface IExternalCallInterceptor
{
    /// <summary>
    /// Decides whether <paramref name="request"/> may proceed, or answers it.
    /// </summary>
    /// <remarks>
    /// This is also the point at which a call can be <em>logged</em>: the request describes itself
    /// (<see cref="ExternalCallRequest.Describe"/>), arguments included, and an interceptor that only wants a
    /// record of what a workspace did returns <see cref="ExternalCallDecision.Allow"/> having written one.
    /// </remarks>
    /// <param name="request">The call about to be made.</param>
    /// <param name="session">The session making it.</param>
    ExternalCallDecision Intercept(ExternalCallRequest request, IRuntimeSession session);
}

/// <summary>
/// Runs a call to one kind of external target.
/// </summary>
/// <remarks>
/// A provider says what it can reach and reaches it. Which providers exist is what makes a platform able to
/// do a thing at all: no COM provider on a machine means a workspace's Excel automation reports that it
/// cannot be run here, rather than failing in some way particular to how it was attempted.
/// </remarks>
public interface IExternalCallProvider
{
    /// <summary>
    /// Whether this provider is the one that runs <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The call to consider.</param>
    bool CanDispatch(ExternalCallRequest request);

    /// <summary>
    /// What a call this provider runs reaches outside the program, which the execution policy decides by: <see cref="ExecutionCategory.Workspace"/>, which
    /// the policy never refuses, unless the provider says otherwise.
    /// </summary>
    /// <remarks>
    /// The platform decides it once for a member and keeps it, from a request without arguments, unless <see cref="CategoryDependsOnArguments"/> says the
    /// member's calls are decided one by one. It is the provider's to say, never a library description's: a workspace can bring a description.
    /// </remarks>
    /// <param name="request">A call that <see cref="CanDispatch"/> accepts.</param>
    ExecutionCategory CategoryOf(ExternalCallRequest request) => ExecutionCategory.Workspace;

    /// <summary>
    /// Whether the category of a call of <paramref name="member"/> depends on its arguments, as a file opened for reading or for writing does, so that
    /// <see cref="CategoryOf"/> is asked of each call rather than once for the member. <see langword="false"/> unless the provider says otherwise.
    /// </summary>
    /// <param name="member">A member whose calls this provider runs.</param>
    bool CategoryDependsOnArguments(Model.Symbols.Abstract.VBTypeMemberSymbol member) => false;

    /// <summary>
    /// Runs it, and returns what it yielded: an error in the result rather than thrown, as everywhere else
    /// in the semantics layer.
    /// </summary>
    /// <param name="request">The call to run. <see cref="CanDispatch"/> has already accepted it.</param>
    /// <param name="resolver">The running session's symbols and their storage.</param>
    Shared.RuntimeSemanticsEvaluationResult Dispatch(ExternalCallRequest request, IRuntimeSymbolResolver resolver);
}
