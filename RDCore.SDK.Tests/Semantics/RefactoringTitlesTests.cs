using System.Globalization;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// What a rename of a <c>Public</c> procedure of a standard module asks, and how it labels its edits, in each language of the user interface.
/// </summary>
[TestClass]
public sealed class RefactoringTitlesTests
{
    [TestMethod]
    [DataRow("en")]
    [DataRow("fr")]
    public void ARenameThatMayBreakOutsideCallers_NamesTheProcedure_AndSaysThatDecliningCancelsIt(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);

        StringAssert.Contains(RefactoringTitles.RenameOutsideCallersQuestion("DoIt", culture), "DoIt");
        StringAssert.Contains(RefactoringTitles.RenameOutsideCallersDescription("DoIt", culture), "DoIt");
        Assert.IsFalse(string.IsNullOrWhiteSpace(RefactoringTitles.RenameOutsideCallersLabel(culture)));
        Assert.AreNotEqual(RefactoringTitles.RenameOutsideCallersRename(culture), RefactoringTitles.RenameOutsideCallersCancel(culture));
    }

    [TestMethod]
    public void ARenameThatMayBreakOutsideCallers_IsAskedInFrench_InFrench()
        => Assert.AreNotEqual(
            RefactoringTitles.RenameOutsideCallersQuestion("DoIt", CultureInfo.GetCultureInfo("en")),
            RefactoringTitles.RenameOutsideCallersQuestion("DoIt", CultureInfo.GetCultureInfo("fr")));
}
