namespace RDCore.SDK.Model.Source;

/// <summary>
/// A position in a source document, expressed as a zero-based line number and a zero-based character offset.
/// </summary>
/// <remarks>
/// 👉 Character offsets count <em>UTF-16 code units</em>, consistent with both the LSP default position encoding
/// and the native encoding of VBA (BSTR) strings.
/// </remarks>
/// <param name="Line">The zero-based line number.</param>
/// <param name="Character">The zero-based character offset within the line.</param>
public readonly record struct SourcePosition(int Line, int Character) : 
    IComparable<SourcePosition>, 
    IEquatable<SourcePosition>
{
    /// <summary>
    /// Position L0C0.
    /// </summary>
    public static SourcePosition Zero { get; } = new(0, 0);

    public int CompareTo(SourcePosition other) =>
        Line != other.Line ? Line.CompareTo(other.Line) : Character.CompareTo(other.Character);

    public static bool operator <(SourcePosition left, SourcePosition right) => left.CompareTo(right) < 0;
    public static bool operator >(SourcePosition left, SourcePosition right) => left.CompareTo(right) > 0;
    public static bool operator <=(SourcePosition left, SourcePosition right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SourcePosition left, SourcePosition right) => left.CompareTo(right) >= 0;

    public static SourcePosition operator +(SourcePosition left, SourcePosition right) => new(left.Line + right.Line, left.Character + right.Character);
    public static SourcePosition operator -(SourcePosition left, SourcePosition right) => new(left.Line - right.Line, left.Character - right.Character);

    /// <summary>
    /// A string representation of this source position.
    /// </summary>
    /// <remarks>
    /// For internal/debugging purposes only: this representation is not intended to be surfaced.
    /// </remarks>
    /// <returns>Line/Character position in a <c>L0C0</c> notation</returns>
    public override string ToString() => $"L{Line}C{Character}";
    public bool Equals(SourcePosition other) => other.Line == Line && other.Character == Character;
    public override int GetHashCode() => HashCode.Combine(Line, Character);
}
