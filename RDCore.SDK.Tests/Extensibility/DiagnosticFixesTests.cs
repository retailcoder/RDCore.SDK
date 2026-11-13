using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RDCore.SDK.Extensibility.Diagnostics;

namespace RDCore.Tests.Extensibility;

/// <summary>
/// The fixes of a finding travel in its diagnostic's <c>data</c>, and are read back from the diagnostic a client sends with a code action request: an item to
/// add to the list of a setting as any other fix; the metadata of an error is no fix.
/// </summary>
[TestClass]
public sealed class DiagnosticFixesTests
{
    [TestMethod]
    public void AnItemToAddToAList_TravelsWithTheDiagnostic()
    {
        var fixes = new DiagnosticFixes
        {
            Rename = new RenameFix { Suggestions = ["Index"] },
            Setting = new SettingFix { Title = "Allow the name 'i'", Key = "Diagnostics.MeaningfulNames.AllowedNames", Value = "i" },
        };

        // as the client sends it back: data as JSON it read and wrote.
        var read = DiagnosticFixes.Of(new Diagnostic { Data = JToken.Parse(fixes.ToData().ToString()) });

        Assert.AreEqual("Diagnostics.MeaningfulNames.AllowedNames", read?.Setting?.Key);
        Assert.AreEqual("i", read?.Setting?.Value);
        Assert.AreEqual("Allow the name 'i'", read?.Setting?.Title);
        CollectionAssert.AreEqual(new[] { "Index" }, read?.Rename?.Suggestions.ToArray());
        Assert.IsNull(read?.Edit);
    }

    [TestMethod]
    public void AnItemToAddToAList_IsAFixOfItsOwn()
    {
        var read = DiagnosticFixes.Of(new Diagnostic { Data = JObject.Parse("""{ "setting": { "title": "Allow", "key": "K", "value": "v" } }""") });

        Assert.AreEqual("v", read?.Setting?.Value);
        Assert.IsNull(read?.Rename);
    }

    [TestMethod]
    public void TheMetadataOfAnError_IsNoFix()
        => Assert.IsNull(DiagnosticFixes.Of(new Diagnostic { Data = JObject.Parse("""{ "errorNumber": 13 }""") }));
}
