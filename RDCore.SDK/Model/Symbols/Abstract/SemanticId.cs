namespace RDCore.SDK.Model.Symbols.Abstract;

/// <summary>
/// Uniquely identifies a <see cref="Symbol"/>, safe for use as a dictionary key or set member.
/// </summary>
/// <remarks>
/// <para>
/// Wraps a symbol's declaring <see cref="Symbol.Uri"/>, comparing <see cref="System.Uri.AbsoluteUri"/>
/// ordinally instead of deferring to <see cref="System.Uri"/>'s own <c>Equals</c>/<c>GetHashCode</c>:
/// those deliberately ignore <see cref="System.Uri.Fragment"/>, but a symbol's discriminating name and
/// location (module, member, nesting) is encoded entirely in the fragment: two distinct symbols
/// sharing a workspace root would otherwise compare equal. No two declarations of a module share an address,
/// namesakes included (<see cref="SymbolUri"/>).
/// </para>
/// <para>
/// The hash of the address is computed once, when the identity is made, so a lookup in a dictionary or a set keyed by identities hashes no
/// string. Two identities of the same <see cref="System.Uri"/> instance are equal without comparing their addresses; two of different
/// instances compare their hashes, then their addresses.
/// </para>
/// </remarks>
public readonly record struct SemanticId : IEquatable<SemanticId>
{
    private readonly Uri _uri;
    private readonly int _hash;

    /// <summary>
    /// The identity of the symbol declared at <paramref name="Uri"/>.
    /// </summary>
    /// <param name="Uri">The identified symbol's <see cref="Symbol.Uri"/>.</param>
    public SemanticId(Uri Uri)
    {
        _uri = Uri;
        _hash = HashOf(Uri);
    }

    /// <summary>
    /// The identified symbol's <see cref="Symbol.Uri"/>.
    /// </summary>
    public Uri Uri
    {
        get => _uri;
        init
        {
            _uri = value;
            _hash = HashOf(value);
        }
    }

    /// <summary>
    /// Gives the identified symbol's <see cref="Symbol.Uri"/>.
    /// </summary>
    /// <param name="Uri">The identified symbol's <see cref="Symbol.Uri"/>.</param>
    public void Deconstruct(out Uri Uri) => Uri = _uri;

    public bool Equals(SemanticId other)
        => ReferenceEquals(_uri, other._uri)
            || (_hash == other._hash && string.Equals(_uri?.AbsoluteUri, other._uri?.AbsoluteUri, StringComparison.Ordinal));

    public override int GetHashCode() => _hash;

    public override string ToString() => _uri?.AbsoluteUri ?? string.Empty;

    private static int HashOf(Uri? uri) => uri is null ? 0 : StringComparer.Ordinal.GetHashCode(uri.AbsoluteUri);
}
