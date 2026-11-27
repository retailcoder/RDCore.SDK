using RDCore.SDK.Runtime;
using RDCore.SDK.Runtime.Abstract.Execution;
using RDCore.SDK.Server.Configuration;

namespace RDCore.Tests.Runtime;

/// <summary>
/// The execution policy: a rule for each category of call that reaches outside the program, which the program's own code
/// (<see cref="ExecutionCategory.Workspace"/>) has none of, and which the <c>AllowDllImports</c> and <c>AllowAutomation</c> settings make stricter, never
/// weaker.
/// </summary>
[TestClass]
public sealed class ExecutionPolicyTests
{
    // every category a rule can be set for: Workspace has none.
    public static IEnumerable<object[]> RuledCategories
        => Enum.GetValues<ExecutionCategory>().Where(category => category != ExecutionCategory.Workspace).Select(category => new object[] { category });

    private static ExecutionPolicy OnlyRuling(ExecutionCategory category, ExecutionRule rule)
    {
        var options = new SdkExecutionPolicyOptions();
        var section = typeof(SdkExecutionPolicyOptions).GetProperty(category.ToString())!;
        section.SetValue(options, new SdkExecutionRuleOptions { Mode = rule.Mode, Audit = rule.Audit });
        return options.ToPolicy();
    }

    // the value an unset category has, which the policy never refuses and never audits.
    [TestMethod]
    public void TheWorkspacesOwnCode_IsCategoryZero()
        => Assert.AreEqual(ExecutionCategory.Workspace, (ExecutionCategory)Enum.ToObject(typeof(ExecutionCategory), 0));

    [TestMethod]
    public void TheDefaultPolicy_AllowsEveryCategory_AndRecordsNothing()
    {
        Assert.IsTrue(ExecutionPolicy.Default.IsPassThrough);
        foreach (var category in Enum.GetValues<ExecutionCategory>())
        {
            Assert.AreEqual(ExecutionRule.PassThrough, ExecutionPolicy.Default[category], category.ToString());
        }
    }

    [TestMethod]
    [DynamicData(nameof(RuledCategories))]
    public void TheRuleOfACategory_IsTheOneSetForIt_AndNoOtherCategorysRule(ExecutionCategory category)
    {
        var rule = new ExecutionRule(ExecutionPolicyMode.Halt, ExecutionAuditLevel.All);

        var policy = OnlyRuling(category, rule);

        Assert.AreEqual(rule, policy[category]);
        foreach (var other in Enum.GetValues<ExecutionCategory>().Where(other => other != category))
        {
            Assert.AreEqual(ExecutionRule.PassThrough, policy[other], other.ToString());
        }
    }

    [TestMethod]
    public void TheWorkspacesOwnCode_HasNoRule_WhateverThePolicySays()
    {
        var policy = new SdkExecutionPolicyOptions
        {
            LibraryImports = new() { Mode = ExecutionPolicyMode.Halt, Audit = ExecutionAuditLevel.All },
            FileIO = new() { Mode = ExecutionPolicyMode.Block, Audit = ExecutionAuditLevel.All },
        }.ToPolicy();

        Assert.AreEqual(ExecutionRule.PassThrough, policy[ExecutionCategory.Workspace]);
    }

    [TestMethod]
    public void ACategoryThePolicyDoesNotKnow_Throws()
    {
        var unknown = (ExecutionCategory)(Enum.GetValues<ExecutionCategory>().Max(category => (int)category) + 1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ExecutionPolicy.Default[unknown]);
    }

    [TestMethod]
    public void APolicyThatOnlySaysWhetherFileIOIsThere_IsNotPassThrough()
    {
        var policy = new SdkExecutionPolicyOptions { FileIO = new() { Mode = ExecutionPolicyMode.Block } }.ToPolicy();

        Assert.IsFalse(policy.IsPassThrough);
    }

    [TestMethod]
    [DataRow(ExecutionAuditLevel.None, false, false)]
    [DataRow(ExecutionAuditLevel.None, true, false)]
    [DataRow(ExecutionAuditLevel.Refused, false, false)]
    [DataRow(ExecutionAuditLevel.Refused, true, true)]
    [DataRow(ExecutionAuditLevel.All, false, true)]
    [DataRow(ExecutionAuditLevel.All, true, true)]
    public void ARule_RecordsTheCallsItsAuditLevelSays(ExecutionAuditLevel audit, bool refused, bool recorded)
        => Assert.AreEqual(recorded, new ExecutionRule(ExecutionPolicyMode.Allow, audit).Records(refused));

    [TestMethod]
    [DataRow(ExecutionPolicyMode.Allow, ExecutionPolicyMode.Block, ExecutionPolicyMode.Block)]
    [DataRow(ExecutionPolicyMode.Block, ExecutionPolicyMode.Block, ExecutionPolicyMode.Block)]
    [DataRow(ExecutionPolicyMode.Halt, ExecutionPolicyMode.Block, ExecutionPolicyMode.Halt)]
    [DataRow(ExecutionPolicyMode.Block, ExecutionPolicyMode.Allow, ExecutionPolicyMode.Block)]
    public void TheStricterMode_Wins(ExecutionPolicyMode mode, ExecutionPolicyMode atLeast, ExecutionPolicyMode expected)
    {
        var rule = new ExecutionRule(mode, ExecutionAuditLevel.Refused).AtLeast(atLeast);

        Assert.AreEqual(expected, rule.Mode);
        Assert.AreEqual(ExecutionAuditLevel.Refused, rule.Audit);
    }

    [TestMethod]
    [DataRow(false, true, DisplayName = "AllowDllImports false")]
    [DataRow(true, false, DisplayName = "AllowAutomation false")]
    [DataRow(false, false, DisplayName = "both false")]
    public void ASettingThatAllowsNoCalls_BlocksItsCategory_AndKeepsItsAuditLevel(bool allowDllImports, bool allowAutomation)
    {
        var policy = new SdkExecutionPolicyOptions
        {
            LibraryImports = new() { Audit = ExecutionAuditLevel.All },
            Automation = new() { Audit = ExecutionAuditLevel.Refused },
        }.ToPolicy();

        var allowing = policy.Allowing(allowDllImports, allowAutomation);

        Assert.AreEqual(allowDllImports ? ExecutionPolicyMode.Allow : ExecutionPolicyMode.Block, allowing.LibraryImports.Mode);
        Assert.AreEqual(allowAutomation ? ExecutionPolicyMode.Allow : ExecutionPolicyMode.Block, allowing.Automation.Mode);
        Assert.AreEqual(ExecutionAuditLevel.All, allowing.LibraryImports.Audit);
        Assert.AreEqual(ExecutionAuditLevel.Refused, allowing.Automation.Audit);
    }

    [TestMethod]
    public void ASettingThatAllowsNoCalls_LeavesAHaltAsItIs()
    {
        var policy = new SdkExecutionPolicyOptions { LibraryImports = new() { Mode = ExecutionPolicyMode.Halt } }.ToPolicy();

        Assert.AreEqual(ExecutionPolicyMode.Halt, policy.Allowing(allowDllImports: false, allowAutomation: false).LibraryImports.Mode);
    }

    [TestMethod]
    public void AProfile_AppliesItsAllowSettingsOverItsPolicy()
    {
        var profile = RuntimeEnvironmentProfile.From(new SdkEnvironmentOptions
        {
            AllowDllImports = false,
            ExecutionPolicy = new()
            {
                LibraryImports = new() { Audit = ExecutionAuditLevel.Refused },
                ProcessLaunch = new() { Mode = ExecutionPolicyMode.Halt },
            },
        });

        Assert.AreEqual(new ExecutionRule(ExecutionPolicyMode.Block, ExecutionAuditLevel.Refused), profile.ExecutionPolicy.LibraryImports);
        Assert.AreEqual(ExecutionPolicyMode.Halt, profile.ExecutionPolicy.ProcessLaunch.Mode);
        Assert.AreEqual(ExecutionPolicyMode.Allow, profile.ExecutionPolicy.Automation.Mode);
    }

    [TestMethod]
    public void AProfileMadeOfAnotherProfile_HasItsPolicy()
    {
        var profile = RuntimeEnvironmentProfile.From(new SdkEnvironmentOptions
        {
            AllowAutomation = false,
            ExecutionPolicy = new() { Registry = new() { Mode = ExecutionPolicyMode.Block, Audit = ExecutionAuditLevel.All } },
        });

        Assert.AreEqual(profile.ExecutionPolicy, RuntimeEnvironmentProfile.Of(new PolicyForwardingProfile(profile)).ExecutionPolicy);
    }

#pragma warning disable CS0618 // the obsolete switches, which read the policy
    [TestMethod]
    public void AProfilesAllowSwitches_SayWhatItsPolicyMakes()
    {
        var policy = new SdkExecutionPolicyOptions { LibraryImports = new() { Mode = ExecutionPolicyMode.Halt } }.ToPolicy();
        var profile = new RuntimeEnvironmentProfile(Is64Bit: true, 0, 1252, false, Policy: policy);

        Assert.IsFalse(profile.AllowDllImports, "its policy halts library imports");
        Assert.IsTrue(profile.AllowAutomation);
        Assert.IsFalse((profile with { AllowAutomation = false }).AllowAutomation);
        Assert.AreEqual(ExecutionPolicyMode.Block, (profile with { AllowAutomation = false }).ExecutionPolicy.Automation.Mode);
        Assert.IsFalse((profile with { AllowDllImports = true }).AllowDllImports, "a switch makes the policy stricter, never weaker");
    }
#pragma warning restore CS0618

    [TestMethod]
    public void AProfileWithNoPolicyOfItsOwn_HasTheDefault_UnderItsAllowSettings()
    {
        IRuntimeEnvironmentProfile profile = new ForwardingProfile(RuntimeEnvironmentProfile.From(new SdkEnvironmentOptions
        {
            AllowDllImports = false,
            ExecutionPolicy = new() { Registry = new() { Mode = ExecutionPolicyMode.Halt } },
        }));

        Assert.AreEqual(ExecutionPolicy.Default.Allowing(allowDllImports: false, allowAutomation: true), profile.ExecutionPolicy);
    }

    // a profile that is not a RuntimeEnvironmentProfile, which Of copies member by member; it has the interface's execution policy.
#pragma warning disable CS0618 // the obsolete switches a profile still implements
    private class ForwardingProfile(RuntimeEnvironmentProfile inner) : IRuntimeEnvironmentProfile
    {
        protected RuntimeEnvironmentProfile Inner => inner;
        public bool Is64Bit => inner.Is64Bit;
        public VBOperatingSystem OperatingSystem => inner.OperatingSystem;
        public int Lcid => inner.Lcid;
        public System.Globalization.CultureInfo Culture => inner.Culture;
        public int AnsiCodePage => inner.AnsiCodePage;
        public System.Text.Encoding AnsiEncoding => inner.AnsiEncoding;
        public bool SupportsOptionCompareDatabase => inner.SupportsOptionCompareDatabase;
        public SDK.Model.Symbols.OptionCompare DatabaseCompare => inner.DatabaseCompare;
        public VBErlLineNumbering ErlLineNumbering => inner.ErlLineNumbering;
        public VBErlAfterErrClear ErlAfterErrClear => inner.ErlAfterErrClear;
        public SDK.Workspace.SupportedLanguage Language => inner.Language;
        public bool AllowDllImports => inner.AllowDllImports;
        public bool AllowAutomation => inner.AllowAutomation;
        public VBExecutionEngine ExecutionEngine => inner.ExecutionEngine;
        public VBBuildConfiguration Configuration => inner.Configuration;
        public bool ReleaseDropsDebugStatements => inner.ReleaseDropsDebugStatements;
        public bool LegacyRegistrySettings => inner.LegacyRegistrySettings;
    }
#pragma warning restore CS0618

    // the same, with the execution policy of the profile it forwards to.
    private sealed class PolicyForwardingProfile(RuntimeEnvironmentProfile inner) : ForwardingProfile(inner), IRuntimeEnvironmentProfile
    {
        public ExecutionPolicy ExecutionPolicy => Inner.ExecutionPolicy;
    }
}
