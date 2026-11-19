using RDCore.SDK.Assistant;
using System.Buffers.Text;
using System.Text;

namespace RDCore.Tests.Assistant;

/// <summary>
/// A fix's id says what the fix is, and is read back as what it says: the language server keeps nothing between the call that lists a fix and the call that
/// previews or applies it. A text that is not an id is read as no fix.
/// </summary>
[TestClass]
public sealed class FixIdsTests
{
    private static readonly TextRange Range = new(new(3, 9), new(3, 13));

    [TestMethod]
    [DataRow(7, DisplayName = "a document a client has open")]
    [DataRow(null, DisplayName = "a file no client has open")]
    public void AFixId_IsReadBackAsTheFixItWasWrittenFor(int? version)
    {
        var fix = new FixIdParts("src/Module1.bas", version, Range, "quickfix", "Remove the variable « Total »");

        Assert.IsTrue(FixIds.TryRead(FixIds.Of(fix), out var read));
        Assert.AreEqual(fix, read);
    }

    [TestMethod]
    public void AFixId_IsBase64Url_WhichAModelCopiesAsItIs()
    {
        var id = FixIds.Of(new FixIdParts(@"C:\work\Module1.bas", 1, Range, "refactor.extract", "?>?>?>"));

        Assert.IsFalse(id.Any(character => character is '+' or '/' or '=' or ' '), id);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no id")]
    [DataRow("", DisplayName = "an empty id")]
    [DataRow("not an id!", DisplayName = "not base64url")]
    [DataRow("{}", DisplayName = "no part")]
    [DataRow("""{"p":"Module1.bas","v":1,"r":[3,9],"k":"quickfix","t":"t"}""", DisplayName = "a range with no end")]
    [DataRow("""{"v":1,"r":[3,9,3,13],"k":"quickfix","t":"t"}""", DisplayName = "no path")]
    [DataRow("[1,2]", DisplayName = "not an object")]
    [DataRow("nope", DisplayName = "not JSON")]
    public void AText_ThatIsNoFixId_IsReadAsNoFix(string? text)
    {
        var id = text is null or "" or "not an id!" ? text : Base64Url.EncodeToString(Encoding.UTF8.GetBytes(text));

        Assert.IsFalse(FixIds.TryRead(id, out var read));
        Assert.IsNull(read);
    }
}
