using RDCore.SDK.Model.Values.Intrinsic;
using RDCore.SDK.Model.Values.Runtime;

namespace RDCore.Tests.Model.Values;

/// <summary>
/// Coverage for <see cref="VBRuntimeObjectId"/>: each object made has an identity no other object of the process has, and <c>Nothing</c>'s is 0.
/// </summary>
[TestClass]
public sealed class VBRuntimeObjectIdTests
{
    [TestMethod]
    public void EachIdentityMade_IsAnotherOne()
    {
        var first = new VBRuntimeObjectId();
        var second = new VBRuntimeObjectId();

        Assert.AreNotEqual(first, second);
        Assert.AreNotEqual(first.Identity, second.Identity);
        Assert.AreNotEqual(first.ToString(), second.ToString());
    }

    [TestMethod]
    public void AnIdentity_IsEqualToItsCopy()
    {
        var made = new VBRuntimeObjectId();
        var copy = made;

        Assert.AreEqual(made, copy);
        Assert.AreEqual(made.GetHashCode(), copy.GetHashCode());
    }

    [TestMethod]
    public void NothingsIdentity_IsZero()
    {
        Assert.AreEqual(0L, default(VBRuntimeObjectId).Identity);
        Assert.AreEqual(default, VBObjectValue.Nothing.Value);
    }

    [TestMethod]
    public void AnObjectsIdentity_IsNotZero()
        => Assert.AreNotEqual(0L, new VBRuntimeObjectId().Identity);

    [TestMethod]
    public void TheIdentity_IsWrittenAsItsNumber()
    {
        var made = new VBRuntimeObjectId();

        Assert.AreEqual(made.Identity.ToString(System.Globalization.CultureInfo.InvariantCulture), made.ToString());
    }

    [TestMethod]
    public void IdentitiesMadeOnManyThreads_AreAllDistinct()
    {
        var made = new VBRuntimeObjectId[8][];
        Parallel.For(0, made.Length, thread =>
        {
            made[thread] = new VBRuntimeObjectId[1000];
            for (var i = 0; i < made[thread].Length; i++)
            {
                made[thread][i] = new VBRuntimeObjectId();
            }
        });

        Assert.AreEqual(8000, made.SelectMany(ids => ids).Select(id => id.Identity).Distinct().Count());
    }
}
