using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Abstract;

/// <summary>
/// Represents an identifier that is unique for every node of an AST.
/// </summary>
/// <remarks>
/// Encodes the node's position within the AST.
/// </remarks>
/// <param name="Lineage">An array of successive child index values in the syntax tree.</param>
public readonly record struct SyntaxNodeId : IEquatable<SyntaxNodeId>, IComparable<SyntaxNodeId>
{
    private readonly string _documentUri;
    private readonly ImmutableArray<int> _lineage;

    public SyntaxNodeId Add(int position) => new(_documentUri, _lineage.Add(position));

    public SyntaxNodeId(string DocumentUri, ImmutableArray<int> Lineage)
    {
        _documentUri = DocumentUri;
        _lineage = Lineage;
    }
    public readonly override string ToString()
    {
        return $"{_documentUri}#{string.Join('/', _lineage)}";
    }

    public readonly bool Equals(SyntaxNodeId other)
    {
        return (other._documentUri == _documentUri)
            && other._lineage.SequenceEqual(_lineage);
    }

    public readonly override int GetHashCode()
    {
        var hashcode = new HashCode();
        hashcode.Add(_documentUri);

        for (var i = 0; i < _lineage.Length; i++)
        {
            hashcode.Add(_lineage[i]);
        }

        return hashcode.ToHashCode();
    }

    public int CompareTo(SyntaxNodeId other)
    {
        if (_lineage.Length < other._lineage.Length)
        {
            return -1;
        }
        else if (_lineage.Length > other._lineage.Length)
        {
            return 1;
        }
        else
        {
            for (var i = 0; i < _lineage.Length; i++)
            {
                if (_lineage[i] < other._lineage[i])
                {
                    return -1;
                }
                else if (_lineage[i] > other._lineage[i])
                {
                    return 1;
                }
            }
            return 0;
        }
    }
}
