namespace RDCore.SDK.Semantics.Flags;

/// <summary>
/// The semantic flags that can be attached to a range clause of a <c>Case</c> (<strong>MS-VBAL 5.4.2.10</strong>): which form it takes, and how its value is
/// compared with the selector.
/// </summary>
[Flags]
public enum CaseClauseSemanticFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,
    /// <summary>
    /// The clause is a single value (<c>Case 5</c>), which MS-VBAL says "is compared with the value of select-expression", without saying how.
    /// </summary>
    ValueForm = 1 << 0,
    /// <summary>
    /// The clause is a comparison (<c>Case Is &gt; 5</c>), for which MS-VBAL states that the expression <c>select-expression &gt; 5</c> is evaluated.
    /// </summary>
    ComparisonForm = 1 << 1,
    /// <summary>
    /// The clause is a range (<c>Case 1 To 10</c>), for which MS-VBAL states that the expression
    /// <c>((select-expression) &gt;= (1)) And ((select-expression) &lt;= (10))</c> is evaluated.
    /// </summary>
    RangeForm = 1 << 2,
}
