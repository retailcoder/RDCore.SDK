using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Server.Configuration;

/// <summary>
/// The inlay hint settings of the platform, under <c>Configuration:InlayHints</c>: one for each place a hint is shown (<see cref="InlayHintSite"/>), each on
/// unless it is turned off.
/// </summary>
/// <remarks>
/// A hint is text an editor shows in a line that is not part of it: what the code means there without saying it, in square brackets, at the place it
/// describes (<see cref="SourceInlayHints"/>). A change takes effect with the next hints a client asks for, while the platform runs.
/// </remarks>
public record class SdkInlayHintOptions
{
    /// <summary>
    /// Whether a positional argument shows the name of the parameter it is given to, before it: <c>Wheel.MoveTo [X:]10, [Y:]20</c>
    /// (<see cref="InlayHintSite.Arguments"/>).
    /// </summary>
    public bool Arguments { get; set; } = true;

    /// <summary>
    /// Whether the statement that closes a procedure shows the procedure's name, after it: <c>End Sub [DoSomething]</c> (<see cref="InlayHintSite.BlockEnds"/>).
    /// </summary>
    public bool BlockEnds { get; set; } = true;

    /// <summary>
    /// Whether a <c>Next</c> that names no variable shows the loop's, after it: <c>Next [i]</c> (<see cref="InlayHintSite.NextVariables"/>).
    /// </summary>
    public bool NextVariables { get; set; } = true;

    /// <summary>
    /// Whether a procedure declared without an access modifier shows the one it has, and a parameter declared without <c>ByVal</c> or <c>ByRef</c> how it is
    /// passed: <c>[Public] Sub Work([ByRef]Total As Long)</c> (<see cref="InlayHintSite.ImplicitModifiers"/>).
    /// </summary>
    public bool ImplicitModifiers { get; set; } = true;

    /// <summary>
    /// Whether a variable or a parameter declared without a type shows the one it has, after its name: <c>Dim First [As Variant], Last As String</c>
    /// (<see cref="InlayHintSite.ImplicitTypes"/>).
    /// </summary>
    public bool ImplicitTypes { get; set; } = true;

    /// <summary>
    /// Whether hints are shown at a place.
    /// </summary>
    /// <param name="site">The place.</param>
    public bool Shows(InlayHintSite site) => site switch
    {
        InlayHintSite.Arguments => Arguments,
        InlayHintSite.BlockEnds => BlockEnds,
        InlayHintSite.NextVariables => NextVariables,
        InlayHintSite.ImplicitModifiers => ImplicitModifiers,
        InlayHintSite.ImplicitTypes => ImplicitTypes,
        _ => false,
    };
}
