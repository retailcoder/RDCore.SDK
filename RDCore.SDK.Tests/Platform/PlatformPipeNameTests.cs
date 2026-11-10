using RDCore.SDK.Client;
using RDCore.SDK.Platform;

namespace RDCore.Tests.Platform;

/// <summary>
/// The name of a pipe of the platform is a pipe on every supported system: on macOS, the socket .NET makes of it in the temporary
/// folder has a path of at most 104 characters, with room to spare.
/// </summary>
[TestClass]
public sealed class PlatformPipeNameTests
{
    // a Mac's $TMPDIR, 49 characters, and the cap on a socket's path there. Checked as a Mac has it, on every system.
    private const string MacTemporaryFolder = "/var/folders/h5/t85nd63d71x27_8gl5gw3l_w0000gn/T/";
    private const int MacSocketPathCap = 104;

    private static string MacSocket(string name) => $"{MacTemporaryFolder}CoreFxPipe_{name}";

    [TestMethod]
    [DataRow(CoreServerComponent.EnvironmentHost)]
    [DataRow(CoreServerComponent.LanguageServer)]
    [DataRow(CoreServerComponent.ParsingServer)]
    [DataRow(CoreServerComponent.Extension)]
    [DataRow(CoreServerComponent.ExternalHost)]
    [DataRow(CoreServerComponent.ClientApp)]
    public void TheNameOfAComponentsPipe_IsAtMostMaxLength_AndAPipeOnMacOS(CoreServerComponent component)
    {
        var name = PlatformPipeName.New(component);

        Assert.IsLessThanOrEqualTo(PlatformPipeName.MaxLength, name.Length, name);
        Assert.IsLessThanOrEqualTo(MacSocketPathCap, MacSocket(name).Length, MacSocket(name));
        StringAssert.Matches(name, new System.Text.RegularExpressions.Regex("^rdc-[a-z]+-[0-9a-z]{12}$"));
    }

    // what is left once a name is as long as one can be: room for a longer temporary folder, and for a suffix a component appends for a
    // second pipe of its own (".Calls").
    [TestMethod]
    public void TheLongestName_LeavesRoomOnMacOS()
    {
        var spare = MacSocketPathCap - MacSocket(new string('x', PlatformPipeName.MaxLength)).Length;

        Assert.IsGreaterThanOrEqualTo(16, spare);
    }

    [TestMethod]
    public void TwoNewNames_OfOneComponent_AreNotTheSame()
    {
        Assert.AreNotEqual(PlatformPipeName.New(CoreServerComponent.Extension), PlatformPipeName.New(CoreServerComponent.Extension));
    }

    [TestMethod]
    public void TheNamesOfTwoComponents_AreTold()
    {
        StringAssert.StartsWith(PlatformPipeName.New(CoreServerComponent.ParsingServer), "rdc-parse-");
        StringAssert.StartsWith(PlatformPipeName.New(CoreServerComponent.ExternalHost), "rdc-xhost-");
    }

    [TestMethod]
    public void AValueThatIsNoComponent_IsRefused()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PlatformPipeName.New((CoreServerComponent)99));
    }
}
