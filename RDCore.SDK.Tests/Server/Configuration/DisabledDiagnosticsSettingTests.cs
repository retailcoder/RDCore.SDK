using Microsoft.Extensions.Configuration;
using RDCore.SDK.Server.Configuration;
using System.IO.Abstractions.TestingHelpers;

namespace RDCore.Tests.Server.Configuration;

/// <summary>
/// <c>Analysis.DisabledDiagnostics</c>: the codes whose diagnostics are not reported, a setting of the platform that changes while it runs. Each is a code of
/// an analyzer's diagnostic, of the platform or of an extension; a code of the language's own errors is no value of it, when it is set and when it is read. A
/// code is added to the list a user set, or removed from it, one at a time.
/// </summary>
[TestClass]
public sealed class DisabledDiagnosticsSettingTests
{
    private const string Key = "Analysis.DisabledDiagnostics";
    private static readonly string File = Path.Combine(Path.GetTempPath(), "rdcore-user", UserSettingsStore.FileName);

    private readonly MockFileSystem _fileSystem = new();

    private UserSettingsStore Sut => new(_fileSystem, File, SettingsSchema.Platform);

    private string[] Listed() => [.. new ConfigurationBuilder().AddJsonStream(_fileSystem.File.OpenRead(File)).Build()
        .GetSection("Configuration:Analysis:DisabledDiagnostics").GetChildren().Select(item => item.Value!)];

    [TestMethod]
    public void TheSetting_IsAListOfDiagnosticCodes_ThatChangesWhileThePlatformRuns()
    {
        Assert.IsTrue(SettingsSchema.Platform.TryFind(Key, out var setting));
        Assert.AreEqual(SettingValueKind.DiagnosticCodeList, setting.Kind);
        Assert.AreEqual(SettingReach.Live, setting.Reach);
        Assert.IsTrue(setting.IsSettable);
    }

    [TestMethod]
    [DataRow("rdc01001, XYZ00042", "RDC01001|XYZ00042", DisplayName = "codes, in any case")]
    [DataRow("", "", DisplayName = "none")]
    [DataRow("RDC01001, VBC00001", null, DisplayName = "a compile error")]
    [DataRow("VBR00006", null, DisplayName = "a run-time error")]
    [DataRow("VBA01000", null, DisplayName = "an error the program raises")]
    [DataRow("RDC1001", null, DisplayName = "no code")]
    public void AValue_IsCodesOfDiagnosticsThatCanBeDisabled(string text, string? codes)
    {
        Assert.IsTrue(SettingsSchema.Platform.TryFind(Key, out var setting));

        var parsed = SettingsSchema.TryParse(setting, text, out var value);

        Assert.AreEqual(codes is not null, parsed);
        if (codes is not null)
        {
            Assert.AreEqual(codes, string.Join('|', (string[])value!));
        }
    }

    [TestMethod]
    public void ACode_IsAddedToTheList_OnceAndOnlyOnce_AndRemovedFromIt()
    {
        Assert.IsTrue(Sut.Add(Key, "RDC01001").IsSet);
        Assert.IsTrue(Sut.Add(Key, "xyz00042").IsSet);
        Assert.IsTrue(Sut.Add(Key, "rdc01001").IsSet, "already there: not added twice");
        CollectionAssert.AreEqual(new[] { "RDC01001", "XYZ00042" }, Listed());

        Assert.IsTrue(Sut.Remove(Key, "rdc01001").IsSet);
        Assert.IsTrue(Sut.Remove(Key, "RDC09999").IsSet, "not there: nothing to remove");
        CollectionAssert.AreEqual(new[] { "XYZ00042" }, Listed());
    }

    [TestMethod]
    public void TheLastCodeRemoved_LeavesAnEmptyList()
    {
        Sut.Add(Key, "RDC01001");

        Assert.IsTrue(Sut.Remove(Key, "RDC01001").IsSet);

        CollectionAssert.AreEqual(new[] { "Analysis.DisabledDiagnostics=" }, Sut.Read().Select(set => $"{set.Setting.Key}={set.Value}").ToArray());
    }

    [TestMethod]
    [DataRow(Key, "VBC00001", UserSettingProblem.InvalidValue, DisplayName = "a language error")]
    [DataRow("Server.Verbose", "True", UserSettingProblem.NotAList, DisplayName = "a setting that is no list")]
    [DataRow("Analysis.Nothing", "RDC01001", UserSettingProblem.UnknownSetting, DisplayName = "no such setting")]
    public void WhatIsNotAnItemOfAList_IsNotAdded(string key, string value, UserSettingProblem problem)
    {
        Assert.AreEqual(problem, Sut.Add(key, value).Problem);
        Assert.AreEqual(problem, Sut.Remove(key, value).Problem);
        Assert.IsFalse(_fileSystem.File.Exists(File));
    }

    [TestMethod]
    public void AnItemOfAList_IsAddedToWhatTheUserSet_AsAnyListIs()
    {
        Sut.Set("Platform.Extensions.Allowed", "A");

        Assert.IsTrue(Sut.Add("Platform.Extensions.Allowed", "B").IsSet);
        Assert.IsTrue(Sut.Add("platform.extensions.allowed", "a").IsSet, "already there, in another case");

        CollectionAssert.AreEqual(new[] { "Platform.Extensions.Allowed=A, B" }, Sut.Read().Select(set => $"{set.Setting.Key}={set.Value}").ToArray());
    }

    [TestMethod]
    public void ACodeThatCannotBeDisabled_WrittenByHand_IsNotRead()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rdcore-disabled-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, UserSettingsStore.FileName);
            System.IO.File.WriteAllText(path, """{ "Configuration": { "Analysis": { "DisabledDiagnostics": [ "VBC00001", "RDC01001", "junk", "xyz00042" ] } } }""");

            var options = new SdkAppOptions();
            new ConfigurationBuilder().AddUserSettings(path, SettingsSchema.Platform).Build().GetSection(SettingsSchema.Root).Bind(options);

            CollectionAssert.AreEquivalent(new[] { "RDC01001", "XYZ00042" }, options.Analysis.DisabledCodes.Select(code => code.ToString()).ToArray());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // the last code removed re-enables it while the platform runs: an empty list the user set is read as a list of none, not as nothing said, which a running
    // component would take for the value it started with.
    [TestMethod]
    public void AnEmptyListTheUserSet_IsReadAsNone_AndAComponentThatStartedWithCodes_HasNoneLive()
    {
        var folder = Path.Combine(Path.GetTempPath(), "rdcore-disabled-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, UserSettingsStore.FileName);
            System.IO.File.WriteAllText(path, """{ "Configuration": { "Analysis": { "DisabledDiagnostics": [ ] } } }""");
            Assert.AreEqual(string.Empty, new ConfigurationBuilder().AddUserSettings(path, SettingsSchema.Platform).Build()["Configuration:Analysis:DisabledDiagnostics"]);

            var options = new SdkAppOptions { Analysis = new() { DisabledDiagnostics = ["RDC01001"] } };
            var live = new LiveSettings([new LiveSettingsTarget(SettingsSchema.Platform, string.Empty, options)], SettingsSchema.Platform, path);

            live.Apply(new Dictionary<string, string> { ["Analysis:DisabledDiagnostics:0"] = "RDC01002" });
            CollectionAssert.AreEqual(new[] { "RDC01002" }, options.Analysis.DisabledDiagnostics);

            live.Apply(new Dictionary<string, string> { ["Analysis:DisabledDiagnostics"] = string.Empty });
            Assert.IsEmpty(options.Analysis.DisabledCodes);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void TheCodesDisabledForADocumentOfAWorkspace_AreThoseOfTheUsersListAndOfTheWorkspaces()
    {
        var options = new SdkAnalysisOptions { DisabledDiagnostics = ["RDC01001"] };

        CollectionAssert.AreEquivalent(new[] { "RDC01001", "RDC01002", "XYZ00042" },
            options.DisabledCodesWith(["rdc01002", "RDC01001", "XYZ00042", "VBC00001"]).Select(code => code.ToString()).ToArray());
    }

    [TestMethod]
    public void TheCodesDisabled_AreThoseThatCanBe_WhereverTheListIsSaid()
    {
        var options = new SdkAnalysisOptions { DisabledDiagnostics = ["rdc01001", "VBR00006", "RDC01001", "nonsense"] };

        CollectionAssert.AreEqual(new[] { "RDC01001" }, options.DisabledCodes.Select(code => code.ToString()).ToArray());
    }
}
