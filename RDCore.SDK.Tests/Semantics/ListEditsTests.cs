using RDCore.SDK.Model.Source;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// The items of a list separated by commas, a procedure's parameters or a call's arguments, the edit that removes one of them, and the edits that write them as others.
/// </summary>
[TestClass]
public sealed class ListEditsTests
{
    // the list of a text: inside its first parentheses, or after the name a call statement calls (the text's first word).
    private static SourceRange ListOf(string text)
    {
        var lines = text.Split("\r\n");
        var open = text.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
        {
            return new SourceRange(new SourcePosition(0, text.IndexOf(' ', StringComparison.Ordinal) is var space and >= 0 ? space : text.Length),
                new SourcePosition(lines.Length - 1, lines[^1].Length));
        }

        var close = lines[^1].LastIndexOf(')');
        return new SourceRange(new SourcePosition(0, open + 1), new SourcePosition(lines.Length - 1, close));
    }

    private static string Removed(string text, int index)
        => DeclarationEdits.ApplyTo(text, [ListEdits.RemoveItem(text, ListOf(text), index)]);

    [TestMethod]
    public void ACommaInAString_InParentheses_OrInBrackets_SeparatesNoItem()
    {
        const string text = "F(\"a, b\", G(1, 2), [c, d])";

        var items = ListEdits.ItemsOf(text, ListOf(text));

        CollectionAssert.AreEqual(new[] { "\"a, b\"", "G(1, 2)", "[c, d]" }, items.Select(item => text[item.Start.Character..item.End.Character]).ToArray());
    }

    [TestMethod]
    public void AnOmittedItem_IsEmpty_AtTheCommaAfterIt()
    {
        const string text = "F a, , c";

        var items = ListEdits.ItemsOf(text, ListOf(text));

        Assert.HasCount(3, items);
        Assert.AreEqual(new SourceRange(new SourcePosition(0, 5), new SourcePosition(0, 5)), items[1]);
    }

    [TestMethod]
    public void AComment_EndsTheList()
    {
        const string text = "F a, b ' c, d";

        Assert.HasCount(2, ListEdits.ItemsOf(text, ListOf(text)));
    }

    [TestMethod]
    public void AListOfBlanks_HasNoItem()
        => Assert.IsEmpty(ListEdits.ItemsOf("Sub F( )", ListOf("Sub F( )")));

    [TestMethod]
    [DataRow("Sub F(a, b, c)", 0, "Sub F(b, c)", DisplayName = "the first")]
    [DataRow("Sub F(a, b, c)", 1, "Sub F(a, c)", DisplayName = "one in the middle")]
    [DataRow("Sub F(a, b, c)", 2, "Sub F(a, b)", DisplayName = "the last")]
    [DataRow("Sub F(a)", 0, "Sub F()", DisplayName = "the only one, in parentheses")]
    [DataRow("F a, b", 0, "F b", DisplayName = "the first argument of a call statement")]
    [DataRow("F a, b", 1, "F a", DisplayName = "the last argument of a call statement")]
    [DataRow("F a", 0, "F", DisplayName = "the only argument of a call statement")]
    [DataRow("F a, , c", 0, "F , c", DisplayName = "before an omitted argument")]
    [DataRow("F a, , c", 1, "F a, c", DisplayName = "an omitted argument")]
    [DataRow("F a, , c", 2, "F a", DisplayName = "the last, after an omitted argument")]
    [DataRow("F(, b)", 1, "F()", DisplayName = "the last, after omitted arguments only")]
    [DataRow("Sub F(a, _\r\n      b)", 1, "Sub F(a)", DisplayName = "the last, on a continued line")]
    [DataRow("Sub F(a, _\r\n      b)", 0, "Sub F(b)", DisplayName = "the first, before a continued line")]
    public void AnItem_IsRemovedWithTheCommaThatSeparatesIt(string text, int index, string removed)
        => Assert.AreEqual(removed, Removed(text, index));

    [TestMethod]
    public void AnItemThatIsNotOneOfTheList_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ListEdits.RemoveItem("F a, b", ListOf("F a, b"), 2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ListEdits.RemoveItem("F a, b", ListOf("F a, b"), -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ListEdits.ItemsOf("F a", new SourceRange(new SourcePosition(0, 0), new SourcePosition(2, 0))));
    }

    private static string Replaced(string text, params string[] items)
        => DeclarationEdits.ApplyTo(text, [.. ListEdits.ReplaceItems(text, ListOf(text), items)]);

    [TestMethod]
    [DataRow("Sub F(a, b, c)", "c|a|b", "Sub F(c, a, b)", DisplayName = "in another order")]
    [DataRow("F a, b", "b|a", "F b, a", DisplayName = "the arguments of a call statement")]
    [DataRow("F a, b", "|a", "F , a", DisplayName = "an argument that becomes omitted")]
    [DataRow("F a, , c", "a|c|", "F a, c, ", DisplayName = "an omitted argument that is given one, after a blank")]
    [DataRow("F a,, c", "a|c|", "F a, c, ", DisplayName = "an omitted argument that is given one, after its comma")]
    [DataRow("F , b", "b|", "F b, ", DisplayName = "an omitted first argument that is given one")]
    [DataRow("F a", "|b|a", "F , b, a", DisplayName = "more items than the list has")]
    [DataRow("F a, b, c", "c", "F c", DisplayName = "fewer items than the list has")]
    [DataRow("F(a, b)", "", "F()", DisplayName = "no item")]
    [DataRow("Sub F(a, _\r\n      b)", "b|a", "Sub F(b, _\r\n      a)", DisplayName = "on continued lines")]
    public void TheItems_AreWrittenWhereTheListHasThem(string text, string items, string replaced)
        => Assert.AreEqual(replaced, Replaced(text, items.Length == 0 ? [] : items.Split('|')));

    [TestMethod]
    public void ItemsAsTheyAreWritten_AreNoEdit()
        => Assert.IsEmpty(ListEdits.ReplaceItems("F a, , c", ListOf("F a, , c"), ["a", "", "c"]));
}
