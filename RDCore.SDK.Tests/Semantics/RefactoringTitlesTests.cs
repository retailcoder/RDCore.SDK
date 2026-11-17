using System.Globalization;
using RDCore.SDK.Semantics.Refactoring;

namespace RDCore.SDK.Tests.Semantics;

/// <summary>
/// What a rename of a <c>Public</c> procedure of a standard module asks, how it labels its edits, and why a rename is refused, in each language of the user
/// interface.
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

    [TestMethod]
    public void EveryRefusalOfARename_HasItsOwnText_InEachLanguage()
    {
        foreach (var kind in Enum.GetValues<RenameRefusalKind>())
        {
            var english = RefactoringTitles.RenameRefusal(kind, "Foo", culture: CultureInfo.GetCultureInfo("en"));
            var french = RefactoringTitles.RenameRefusal(kind, "Foo", culture: CultureInfo.GetCultureInfo("fr"));

            Assert.AreNotEqual(kind.ToString(), english, $"{kind} has no English text.");
            Assert.AreNotEqual(english, french, $"{kind} has no French text.");
        }
    }

    [TestMethod]
    [DataRow("en", "'Foo' already means Module1.Foo at Module2 4:12: the rename would change what the code refers to.")]
    [DataRow("fr", "« Foo » désigne déjà Module1.Foo à Module2 4:12 : le renommage changerait ce à quoi le code fait référence.")]
    public void ARefusedRename_NamesTheNameWhatElseItMeans_AndWhere(string language, string expected)
        => Assert.AreEqual(expected, RefactoringTitles.RenameRefusal(RenameRefusalKind.Conflict, "Foo", "Module1.Foo", "Module2 4:12", CultureInfo.GetCultureInfo(language)));

    [TestMethod]
    [DataRow("en", "'AModuleNameOfMoreThanThirtyOneCharacters' is too long for the name of a module, which is at most 31 characters long.")]
    [DataRow("fr", "« AModuleNameOfMoreThanThirtyOneCharacters » est trop long pour le nom d'un module, qui compte au plus 31 caractères.")]
    public void AModuleNameTooLong_NamesTheNewName_AndTheMostCharactersAModuleNameHas(string language, string expected)
        => Assert.AreEqual(expected,
            RefactoringTitles.RenameRefusal(RenameRefusalKind.ModuleNameTooLong, "AModuleNameOfMoreThanThirtyOneCharacters", "31", culture: CultureInfo.GetCultureInfo(language)));
}
