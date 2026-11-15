using RDCore.SDK.Extensibility.Diagnostics;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Platform.Components;

namespace RDCore.Tests.Platform;

/// <summary>
/// <see cref="HostSemanticsParams"/>, <see cref="HostProjectAnalyzeParams"/> and <see cref="HostProjectReleaseParams"/> as the transport carries them: the
/// address of a module arrives with its project's root and its fragment, a request that names a module by its name alone arrives without one, and the root
/// of a project composed for analysis stays the absolute one it was.
/// </summary>
[TestClass]
public sealed class HostSemanticsSerializationTests
{
    [TestMethod]
    public void AModuleUri_IsTheSameAddress_AcrossTheWire()
    {
        var root = Wire.FileUri(Path.Combine(Path.GetTempPath(), "rdcore-wire", "Service"));
        var module = SymbolUri.Module(root, "Shared");

        var read = Wire.RoundTrip(new HostSemanticsParams { ModuleUri = module, Phase = AnalysisPhase.Static });

        Assert.IsTrue(read.ModuleUri!.IsAbsoluteUri, read.ModuleUri.OriginalString);
        Assert.AreEqual(module.AbsoluteUri, read.ModuleUri.AbsoluteUri);
        Assert.AreEqual("#Shared", read.ModuleUri.Fragment);
        Assert.AreEqual(AnalysisPhase.Static, read.Phase);
    }

    // two modules of one project differ only by their fragment, which Uri.Equals ignores: AbsoluteUri tells them apart.
    [TestMethod]
    public void TwoModulesOfOneRoot_StayTwoAddresses_AcrossTheWire()
    {
        var root = Wire.FileUri(Path.Combine(Path.GetTempPath(), "rdcore-wire", "Service"));
        var shared = SymbolUri.Module(root, "Shared");
        var other = SymbolUri.Module(root, "Other");

        var readShared = Wire.RoundTrip(new HostSemanticsParams { ModuleUri = shared });
        var readOther = Wire.RoundTrip(new HostSemanticsParams { ModuleUri = other });

        Assert.AreNotEqual(readShared.ModuleUri!.AbsoluteUri, readOther.ModuleUri!.AbsoluteUri);
        Assert.AreEqual(shared.AbsoluteUri, readShared.ModuleUri.AbsoluteUri);
        Assert.AreEqual(other.AbsoluteUri, readOther.ModuleUri.AbsoluteUri);
    }

    [TestMethod]
    public void AnAnalysisProjectsRoot_IsTheSameAbsoluteUri_AcrossTheWire()
    {
        var root = Wire.FileUri(Path.Combine(Path.GetTempPath(), "rdcore-wire", "Client"));

        var analyze = Wire.RoundTrip(new HostProjectAnalyzeParams { WorkspaceRoot = root });
        var release = Wire.RoundTrip(new HostProjectReleaseParams { WorkspaceRoot = root });

        Assert.IsTrue(analyze.WorkspaceRoot.IsAbsoluteUri, analyze.WorkspaceRoot.OriginalString);
        Assert.AreEqual(root.AbsoluteUri, analyze.WorkspaceRoot.AbsoluteUri);
        Assert.AreEqual(root.AbsoluteUri, release.WorkspaceRoot.AbsoluteUri);
    }

    [TestMethod]
    public void WhatAnAnalysisProjectRequestDid_IsTheSame_AcrossTheWire()
    {
        var analyzed = Wire.RoundTrip(new HostProjectAnalyzeResult { Composed = true, ModuleCount = 3 });
        var released = Wire.RoundTrip(new HostProjectReleaseResult { Released = true });

        Assert.IsTrue(analyzed.Composed);
        Assert.AreEqual(3, analyzed.ModuleCount);
        Assert.IsTrue(released.Released);
    }

    [TestMethod]
    public void AModuleName_ArrivesWithoutAModuleUri()
    {
        var read = Wire.RoundTrip(new HostSemanticsParams { ModuleName = "Module1" });

        Assert.AreEqual("Module1", read.ModuleName);
        Assert.IsNull(read.ModuleUri);
    }
}
