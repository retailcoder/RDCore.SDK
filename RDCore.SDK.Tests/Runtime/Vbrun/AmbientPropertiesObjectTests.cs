using RDCore.SDK.Runtime.Stdole;
using RDCore.SDK.Runtime.Vbrun;

namespace RDCore.Tests.Runtime.Vbrun;

/// <summary>
/// The <c>VBRUN.AmbientProperties</c> of a placed UserControl (<see cref="AmbientPropertiesObject"/>), as VB6 reads it on a form while the program
/// runs.
/// </summary>
[TestClass]
public sealed class AmbientPropertiesObjectTests
{
    private sealed class Form : IAmbientContainer
    {
        public uint BackColor { get; set; } = 0x8000000F;
        public uint ForeColor { get; set; } = 0x80000012;
        public StdFontObject Font { get; } = new();
        public bool RightToLeft { get; set; }
        public string ScaleUnits { get; set; } = "Twip";
        public string DisplayName { get; set; } = "Gauge1";
        public bool DisplayAsDefault { get; set; }
        public int LocaleID { get; set; } = 1033;
    }

    [TestMethod]
    public void TheContainersMembers_AreReadFromIt_EachTimeTheyAreRead()
    {
        var form = new Form();
        var ambient = new AmbientPropertiesObject(form);

        Assert.AreEqual((0x8000000Fu, 0x80000012u, false, "Twip", "Gauge1", false, 1033),
            (ambient.BackColor, ambient.ForeColor, ambient.RightToLeft, ambient.ScaleUnits, ambient.DisplayName, ambient.DisplayAsDefault, ambient.LocaleID));
        (form.BackColor, form.ScaleUnits, form.DisplayAsDefault) = (0xFF, "Pixel", true);
        Assert.AreEqual((0xFFu, "Pixel", true), (ambient.BackColor, ambient.ScaleUnits, ambient.DisplayAsDefault));
    }

    [TestMethod]
    public void TheFont_IsACopyOfTheContainers()
    {
        var form = new Form();
        Assert.AreEqual(StdFontFailure.None, form.Font.TrySetName("Arial"));
        var ambient = new AmbientPropertiesObject(form);

        var font = ambient.Font;

        Assert.AreNotSame(form.Font, font);
        Assert.AreEqual("Arial", font.Name);
        Assert.AreEqual(StdFontFailure.None, font.TrySetName("Tahoma"));
        Assert.AreEqual("Arial", form.Font.Name);
    }

    [TestMethod]
    public void TheOtherMembers_HaveVB6sRunTimeValues()
    {
        var ambient = new AmbientPropertiesObject(new Form());

        Assert.AreEqual((true, false, false, true, (short)0, true, false),
            (ambient.MessageReflect, ambient.ShowGrabHandles, ambient.ShowHatching, ambient.SupportsMnemonics, ambient.TextAlign, ambient.UserMode, ambient.UIDead));
    }

    [TestMethod]
    public void NoContainer_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new AmbientPropertiesObject(null!));
    }
}
