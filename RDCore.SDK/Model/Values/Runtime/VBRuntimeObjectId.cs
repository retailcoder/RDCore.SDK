namespace RDCore.SDK.Model.Values.Runtime;

/// <summary>
/// Represents a <em>runtime object</em>, an <em>instance</em> of a class definition.
/// </summary>
public readonly record struct VBRuntimeObjectId() : IEquatable<VBRuntimeObjectId>
{
    private readonly Guid _guid = Guid.NewGuid();

    public bool Equals(VBRuntimeObjectId other) => _guid.Equals(other._guid);
    public override int GetHashCode() => _guid.GetHashCode();
    public override string ToString() => _guid.ToString();
}
