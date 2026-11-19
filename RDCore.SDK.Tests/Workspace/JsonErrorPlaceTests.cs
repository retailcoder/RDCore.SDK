using System.Text.Json;
using RDCore.SDK.Workspace;

namespace RDCore.Tests.Workspace;

[TestClass]
public sealed class JsonErrorPlaceTests
{
    [TestMethod]
    public void PlaceIn_AnExceptionThatPlacesNothing_IsNull()
        => Assert.IsNull(new JsonException("wrong").PlaceIn("{}"));

    // the third line, after four bytes: two letters of two bytes each are two columns, and the place is the third.
    [TestMethod]
    public void PlaceIn_CountsTheCharactersOfTheLine()
        => Assert.AreEqual<(long, long)?>((3L, 3L), new JsonException("wrong", "$", 2, 4).PlaceIn("{\n  \"A\": 1,\néé}"));

    // where the text is not known, or has no such line, the column is the reader's count of bytes.
    [TestMethod]
    [DataRow(null, DisplayName = "no text")]
    [DataRow("{}", DisplayName = "a text of one line")]
    public void PlaceIn_WithNoSuchLine_IsTheCountOfBytes(string? text)
        => Assert.AreEqual<(long, long)?>((3L, 5L), new JsonException("wrong", "$", 2, 4).PlaceIn(text));
}
