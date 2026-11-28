using RDCore.SDK.Semantics.Flow;

namespace RDCore.Tests.Semantics;

/// <summary>
/// The subtypes a <c>Variant</c> may hold (<see cref="VariantSubtypes"/>): the same whatever order the code gives them in, and one subtype only when every
/// value is known to have it.
/// </summary>
[TestClass]
public sealed class VariantSubtypesTests
{
    [TestMethod]
    public void TheOrderTheCodeGivesTheSubtypesIn_DoesNotChangeThem()
    {
        // If c Then v = 1 Else v = "a", and the same with the branches swapped.
        var written = VariantSubtypes.Of(["Integer", "String"], isComplete: true);
        var swapped = VariantSubtypes.Of(["String", "Integer"], isComplete: true);

        Assert.AreEqual(written, swapped);
        Assert.AreEqual(written.GetHashCode(), swapped.GetHashCode());
    }

    [TestMethod]
    public void ASubtypeGivenTwice_IsOneSubtype()
        => CollectionAssert.AreEqual(new[] { "Integer" }, VariantSubtypes.Of(["Integer", "Integer"], isComplete: true).Subtypes.ToArray());

    [TestMethod]
    [DataRow("Integer", true, "Integer", DisplayName = "one known subtype")]
    [DataRow("Integer Integer", true, "Integer", DisplayName = "the same subtype from two writes")]
    [DataRow("Integer String", true, null, DisplayName = "two subtypes")]
    [DataRow("Integer Empty", true, null, DisplayName = "a write on one path, the initial value on the other")]
    [DataRow("Integer", false, null, DisplayName = "a value whose subtype is not known also reaches it")]
    public void OnlySubtype(string subtypes, bool isComplete, string? only)
        => Assert.AreEqual(only, VariantSubtypes.Of(subtypes.Split(' '), isComplete).OnlySubtype);

    [TestMethod]
    public void SubtypesThatAreComplete_AreNotTheSameAsTheSameSubtypesThatAreNot()
        => Assert.AreNotEqual(VariantSubtypes.Of(["Long"], isComplete: true), VariantSubtypes.Of(["Long"], isComplete: false));
}
