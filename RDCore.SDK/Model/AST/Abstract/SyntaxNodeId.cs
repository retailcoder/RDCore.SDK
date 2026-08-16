using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Abstract;

/// <summary>
/// Represents an identifier that is unique for every node of an AST.
/// </summary>
/// <remarks>
/// Encodes the node's position within the AST.
/// </remarks>
/// <param name="DocumentUri">The URI of the document this syntax tree belongs to.</param>
/// <param name="Lineage">An array of successive child index values in the syntax tree.</param>
public readonly record struct SyntaxNodeId(string DocumentUri, ImmutableArray<int> Lineage) : IEquatable<SyntaxNodeId>, IComparable<SyntaxNodeId>
{
    public SyntaxNodeId Add(int position) => new(DocumentUri, Lineage.Add(position));
    public readonly override string ToString()
    {
        return $"{DocumentUri}#{string.Join('/', Lineage)}";
    }

    public readonly bool Equals(SyntaxNodeId other)
    {
        return (other.DocumentUri == DocumentUri)
            && other.Lineage.SequenceEqual(Lineage);
    }

    public readonly override int GetHashCode()
    {
        var hashcode = new HashCode();
        hashcode.Add(DocumentUri);

        for (var i = 0; i < Lineage.Length; i++)
        {
            hashcode.Add(Lineage[i]);
        }

        return hashcode.ToHashCode();
    }

    public int CompareTo(SyntaxNodeId other)
    {
        if (DocumentUri == other.DocumentUri)
        {
            for (var i = 0; i < Lineage.Length; i++)
            {
                if (i < other.Lineage.Length)
                {
                    if (Lineage[i] < other.Lineage[i])
                    {
                        return -1;
                    }
                    else if (Lineage[i] > other.Lineage[i])
                    {
                        return 1;
                    }
                }
                else
                {
                    // this node goes deeper, so necessarily compares greater than:
                    return 1;
                }
            }

            if (Lineage.Length < other.Lineage.Length)
            {
                // same lineage but other node goes deeper; necessarily compares smaller than:
                return -1;
            }
        }
        return DocumentUri.CompareTo(other.DocumentUri);
    }
}
