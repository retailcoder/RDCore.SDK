using RDCore.SDK.Assistant;

namespace RDCore.Tests.Assistant;

/// <summary>
/// A document's change is said as <c>diff -u</c> says it: hunks of the changed lines with three lines of context, line numbers from 1, and the last line's
/// missing line break said. Texts with the same lines have no diff, whatever ends their lines.
/// </summary>
[TestClass]
public sealed class UnifiedDiffTests
{
    private static string Numbered(int count, Func<int, string>? line = null)
        => string.Concat(Enumerable.Range(1, count).Select(number => (line?.Invoke(number) ?? $"L{number}") + "\r\n"));

    [TestMethod]
    public void TheSameText_HasNoDiff()
    {
        Assert.AreEqual(string.Empty, UnifiedDiff.Of("Module1.bas", Numbered(5), Numbered(5)));
    }

    [TestMethod]
    public void TextsThatDifferInTheirLineBreaksOnly_HaveNoDiff()
    {
        Assert.AreEqual(string.Empty, UnifiedDiff.Of("Module1.bas", "a\r\nb\r\n", "a\nb\r"));
    }

    [TestMethod]
    public void ALineChanged_IsRemovedThenAdded_WithThreeLinesOfContextAroundIt()
    {
        var diff = UnifiedDiff.Of("src/Module1.bas", Numbered(10), Numbered(10, number => number == 5 ? "X" : $"L{number}"));

        Assert.AreEqual("--- src/Module1.bas\n+++ src/Module1.bas\n@@ -2,7 +2,7 @@\n L2\n L3\n L4\n-L5\n+X\n L6\n L7\n L8\n", diff);
    }

    [TestMethod]
    public void ALineAdded_CountsInTheNewSpanOnly_AndTheContextIsWhatTheTextHas()
    {
        var diff = UnifiedDiff.Of("Module1.bas", "a\r\nb\r\n", "a\r\nx\r\nb\r\n");

        Assert.AreEqual("--- Module1.bas\n+++ Module1.bas\n@@ -1,2 +1,3 @@\n a\n+x\n b\n", diff);
    }

    [TestMethod]
    public void AnEmptyText_IsSaidToStartAtLineZero()
    {
        Assert.AreEqual("--- Module1.bas\n+++ Module1.bas\n@@ -0,0 +1 @@\n+a\n", UnifiedDiff.Of("Module1.bas", string.Empty, "a\r\n"));
    }

    [TestMethod]
    public void ALastLineWithNoLineBreak_IsSaidSo()
    {
        var diff = UnifiedDiff.Of("Module1.bas", "a\r\nb", "a\r\nc\r\n");

        Assert.AreEqual("--- Module1.bas\n+++ Module1.bas\n@@ -1,2 +1,2 @@\n a\n-b\n\\ No newline at end of file\n+c\n", diff);
    }

    [TestMethod]
    [DataRow(2, 9, 1, DisplayName = "six lines between: one hunk")]
    [DataRow(2, 10, 2, DisplayName = "seven lines between: two hunks")]
    public void ChangesWithMoreThanTwiceTheContextBetweenThem_AreHunksOfTheirOwn(int first, int second, int hunks)
    {
        var diff = UnifiedDiff.Of("Module1.bas", Numbered(20), Numbered(20, number => number == first || number == second ? "X" : $"L{number}"));

        Assert.AreEqual(hunks, diff.Split('\n').Count(line => line.StartsWith("@@", StringComparison.Ordinal)), diff);
    }

    [TestMethod]
    public void NoContext_ShowsTheChangedLinesAlone()
    {
        var diff = UnifiedDiff.Of("Module1.bas", Numbered(5), Numbered(5, number => number == 3 ? "X" : $"L{number}"), context: 0);

        Assert.AreEqual("--- Module1.bas\n+++ Module1.bas\n@@ -3 +3 @@\n-L3\n+X\n", diff);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => UnifiedDiff.Of("Module1.bas", "a", "b", context: -1));
    }
}
