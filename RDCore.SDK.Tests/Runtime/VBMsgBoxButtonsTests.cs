using RDCore.SDK.Runtime.Abstract.StdLib;

namespace RDCore.Tests.Runtime;

/// <summary>
/// A <c>MsgBox</c>'s style, as whoever shows the dialog reads it: its buttons, its icon, the button that has the focus, how it closes without a button, and
/// how it is laid out.
/// </summary>
[TestClass]
public sealed class VBMsgBoxButtonsTests
{
    [TestMethod]
    [DataRow(VBMsgBoxStyle.VBOKOnly, (VBMsgBoxStyle)0)]
    [DataRow(VBMsgBoxStyle.VBCritical, VBMsgBoxStyle.VBCritical)]
    [DataRow(VBMsgBoxStyle.VBYesNo | VBMsgBoxStyle.VBQuestion | VBMsgBoxStyle.VBDefaultButton2, VBMsgBoxStyle.VBQuestion)]
    [DataRow(VBMsgBoxStyle.VBExclamation | VBMsgBoxStyle.VBMsgBoxHelpButton, VBMsgBoxStyle.VBExclamation)]
    [DataRow(VBMsgBoxStyle.VBInformation, VBMsgBoxStyle.VBInformation)]
    [DataRow((VBMsgBoxStyle)0x50, (VBMsgBoxStyle)0, DisplayName = "a value that names no icon")]
    public void TheIcon_IsTheOneTheStyleNames(VBMsgBoxStyle style, VBMsgBoxStyle icon) => Assert.AreEqual(icon, VBMsgBoxButtons.IconOf(style));

    [TestMethod]
    [DataRow(VBMsgBoxStyle.VBYesNoCancel, 0)]
    [DataRow(VBMsgBoxStyle.VBYesNoCancel | VBMsgBoxStyle.VBDefaultButton2, 1)]
    [DataRow(VBMsgBoxStyle.VBYesNoCancel | VBMsgBoxStyle.VBDefaultButton3, 2)]
    [DataRow(VBMsgBoxStyle.VBYesNo | VBMsgBoxStyle.VBDefaultButton3, 0, DisplayName = "a default button the dialog does not have is the first")]
    [DataRow(VBMsgBoxStyle.VBOKOnly | VBMsgBoxStyle.VBDefaultButton4, 0)]
    public void TheDefaultChoice_IsTheButtonTheStyleNames_OrTheFirst(VBMsgBoxStyle style, int index)
        => Assert.AreEqual(index, VBMsgBoxButtons.DefaultChoiceOf(style));

    [TestMethod]
    [DataRow(VBMsgBoxStyle.VBOKOnly, VBMsgBoxResult.VBOK)]
    [DataRow(VBMsgBoxStyle.VBOKCancel, VBMsgBoxResult.VBCancel)]
    [DataRow(VBMsgBoxStyle.VBYesNoCancel, VBMsgBoxResult.VBCancel)]
    [DataRow(VBMsgBoxStyle.VBRetryCancel, VBMsgBoxResult.VBCancel)]
    public void ADialogClosedWithoutAButton_AnswersCancel_OrOKWhenItIsItsOnlyButton(VBMsgBoxStyle style, VBMsgBoxResult answer)
        => Assert.AreEqual(answer, VBMsgBoxButtons.EscapeResultOf(style));

    [TestMethod]
    [DataRow(VBMsgBoxStyle.VBYesNo)]
    [DataRow(VBMsgBoxStyle.VBAbortRetryIgnore)]
    public void ADialogWithNoCancel_AndMoreThanOneButton_CannotBeClosedWithoutOne(VBMsgBoxStyle style)
        => Assert.IsNull(VBMsgBoxButtons.EscapeResultOf(style));

    [TestMethod]
    public void TheLayoutFlags_AreReadFromTheStyle()
    {
        var all = VBMsgBoxStyle.VBOKCancel | VBMsgBoxStyle.VBMsgBoxHelpButton | VBMsgBoxStyle.VBMsgBoxRight | VBMsgBoxStyle.VBMsgBoxRtlReading;

        Assert.IsTrue(VBMsgBoxButtons.HasHelpButton(all));
        Assert.IsTrue(VBMsgBoxButtons.IsRightAligned(all));
        Assert.IsTrue(VBMsgBoxButtons.IsRightToLeft(all));
        Assert.IsFalse(VBMsgBoxButtons.HasHelpButton(VBMsgBoxStyle.VBOKCancel));
        Assert.IsFalse(VBMsgBoxButtons.IsRightAligned(VBMsgBoxStyle.VBOKCancel));
        Assert.IsFalse(VBMsgBoxButtons.IsRightToLeft(VBMsgBoxStyle.VBOKCancel));
        CollectionAssert.AreEqual(new[] { VBMsgBoxResult.VBOK, VBMsgBoxResult.VBCancel }, VBMsgBoxButtons.ChoicesOf(all).ToArray(), "the Help button answers nothing");
    }
}
