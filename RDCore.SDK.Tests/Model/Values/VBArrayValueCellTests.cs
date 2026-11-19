using RDCore.SDK.Model.Types;
using RDCore.SDK.Model.Values.Bindings;
using RDCore.SDK.Model.Values.Intrinsic;

namespace RDCore.Tests.Model.Values;

/// <summary>
/// An element's cell is made the first time the element is used, and not when the array is: these pin when cells are made, and that an element whose
/// cell is not made yet reads, copies, is let and is forgotten as the cell the array's constructor would have made.
/// </summary>
[TestClass]
[TestCategory("RD-VBAL §2.5 Runtime Values")]
public sealed class VBArrayValueCellTests
{
    private int _made;

    private ValueBindingHandle MakeCell()
    {
        _made++;
        return new ValueBindingHandle(VBIntegerType.TypeInfo.DefaultValue.RuntimeValue);
    }

    private VBFixedSizeArrayValue NewArray(int uBound) => new([(0, uBound)], VBIntegerType.TypeInfo, elementCell: MakeCell);

    private static object? Read(VBArrayValue array, int index) => array.ElementAt(index)!.RuntimeValue.BoxedValue;

    private static bool IsKnown(VBArrayValue array, int index) => ((IKnowledgeBinding)array.CellAt(index)!).IsKnown;

    [TestMethod]
    public void AnArrayOfTheMostElements_MakesNoCell()
    {
        var array = new VBFixedSizeArrayValue([(1, (int)VBArrayValue.MaximumLength)], VBByteType.TypeInfo, elementCell: MakeCell);

        Assert.AreEqual((int)VBArrayValue.MaximumLength, array.Length);
        Assert.AreEqual(0, _made);
    }

    [TestMethod]
    public void ReadingAnElement_MakesItsCellOnce()
    {
        var array = NewArray(9);

        Assert.AreEqual((short)0, Read(array, 3));
        Assert.AreEqual((short)0, array[3]!.RuntimeValue.BoxedValue);
        Assert.AreSame(array.CellAt(3), array.GetElementHandle(3));
        Assert.AreEqual(1, _made);
    }

    [TestMethod]
    public void AnElementWritten_IsReadBack_AndMakesOneCell()
    {
        var array = NewArray(9);

        Assert.IsTrue(array.TryLetElementAt(5, new VBIntegerValue(42)));

        Assert.AreEqual((short)42, Read(array, 5));
        Assert.AreEqual((short)0, Read(array, 4));
        Assert.AreEqual(2, _made);
    }

    [TestMethod]
    public void TheCellsMade_AreTheElementsUsed_AndTheirWalkMakesNone()
    {
        var array = NewArray(99);
        Assert.IsEmpty(array.MadeCells);

        _ = array.ElementAt(30);
        Assert.IsTrue(array.TryLetElementAt(5, new VBIntegerValue(1)));
        var view = array with { };
        var cell = new ValueBindingHandle(new VBIntegerValue(9).RuntimeValue);
        Assert.IsTrue(view.TrySetElementAt(70, cell));

        CollectionAssert.AreEqual(new[] { 5, 30 }, array.MadeCells.Select(made => made.FlatIndex).ToArray());
        CollectionAssert.AreEqual(new[] { 5, 30, 70 }, view.MadeCells.Select(made => made.FlatIndex).ToArray());
        Assert.AreSame(cell, view.MadeCells.Last().Cell);
        Assert.AreEqual(2, _made);
    }

    [TestMethod]
    public void AView_SharesTheCellsMadeAfterIt()
    {
        var array = NewArray(9);
        var view = array with { };

        Assert.IsTrue(view.TryLetElementAt(2, new VBIntegerValue(7)));

        Assert.AreSame(view.CellAt(2), array.CellAt(2));
        Assert.AreEqual((short)7, Read(array, 2));
        Assert.AreEqual(1, _made);
    }

    [TestMethod]
    public void AViewsElementRebound_IsNotRebound_InTheArray()
    {
        var array = NewArray(9);
        var view = array with { };
        var cell = new ValueBindingHandle(new VBIntegerValue(9).RuntimeValue);

        Assert.IsTrue(view.TrySetElementAt(1, cell));

        Assert.AreSame(cell, view.CellAt(1));
        Assert.AreNotSame(cell, array.CellAt(1));
        Assert.AreEqual((short)0, Read(array, 1));
    }

    [TestMethod]
    public void ACopy_MakesACellOnlyForTheElementsMade_AndSharesNone()
    {
        var array = NewArray(999);
        Assert.IsTrue(array.TryLetElementAt(10, new VBIntegerValue(3)));

        var copy = array.DataCopy();
        Assert.IsTrue(copy.TryLetElementAt(10, new VBIntegerValue(4)));
        Assert.IsTrue(copy.TryLetElementAt(11, new VBIntegerValue(5)));

        Assert.AreEqual((short)3, Read(array, 10));
        Assert.AreEqual((short)0, Read(array, 11));
        Assert.AreEqual((short)4, Read(copy, 10));
        Assert.AreEqual((short)5, Read(copy, 11));
        Assert.AreNotSame(array.CellAt(10), copy.CellAt(10));
        Assert.AreEqual(3, _made);
    }

    [TestMethod]
    public void ForgettingTheElements_ForgetsTheCellsMadeAfterwards()
    {
        var array = NewArray(9);
        _ = array.CellAt(0);

        array.ForgetElements();

        Assert.IsFalse(IsKnown(array, 0));
        Assert.IsFalse(IsKnown(array, 9));
        Assert.AreEqual((short)0, Read(array, 9));
    }

    [TestMethod]
    public void ForgettingTheElements_ForTheDefaultAssigned_KeepsThemKnown()
    {
        var array = NewArray(9);

        array.ForgetElements(VBIntegerType.TypeInfo.DefaultValue.RuntimeValue);
        array.ForgetElements(VBIntegerType.TypeInfo.DefaultValue.RuntimeValue);

        Assert.AreEqual(0, _made);
        Assert.IsTrue(IsKnown(array, 9));
    }

    [TestMethod]
    public void ForgettingTheElements_ForTheDefault_ThenForAnotherValue_ForgetsThem()
    {
        var array = NewArray(9);

        array.ForgetElements(VBIntegerType.TypeInfo.DefaultValue.RuntimeValue);
        array.ForgetElements(new VBIntegerValue(2).RuntimeValue);

        Assert.IsFalse(IsKnown(array, 9));
    }

    [TestMethod]
    public void ForgettingTheElements_ForAnotherValue_KeepsTheElementsThatHoldIt()
    {
        var array = NewArray(9);
        var two = new VBIntegerValue(2);
        Assert.IsTrue(array.TryLetElementAt(1, two));

        array.ForgetElements(two.RuntimeValue);

        Assert.IsTrue(IsKnown(array, 1));
        Assert.IsFalse(IsKnown(array, 2));
        Assert.AreEqual(2, _made);
    }

    [TestMethod]
    public void ResettingTheElements_MakesThemKnown()
    {
        var array = NewArray(9);
        _ = array.CellAt(0);
        array.ForgetElements();

        array.ResetElements();

        Assert.IsTrue(IsKnown(array, 0));
        Assert.IsTrue(IsKnown(array, 9));
    }

    [TestMethod]
    public void LettingAnArray_LetsWhatIsKnownOfItsElements()
    {
        var array = NewArray(9);
        var forgotten = NewArray(9);
        forgotten.ForgetElements();
        Assert.IsTrue(forgotten.TryLetElementAt(4, new VBIntegerValue(8)));

        Assert.IsTrue(array.TryLetFrom(forgotten.DataCopy()));

        Assert.AreEqual((short)8, Read(array, 4));
        Assert.IsTrue(IsKnown(array, 4));
        Assert.IsFalse(IsKnown(array, 5));
    }

    [TestMethod]
    public void ACopyOfForgottenElements_IsNotKnown()
    {
        var array = NewArray(9);
        array.ForgetElements();

        var copy = array.DataCopy();

        Assert.IsFalse(IsKnown(copy, 3));
    }
}
