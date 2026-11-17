using RDCore.SDK.Runtime.Abstract.Execution;
using System.Globalization;
using System.Text;

namespace RDCore.SDK.Runtime;

/// <summary>
/// The default <see cref="IRuntimeEnvironmentProfile"/> implementation.
/// </summary>
/// <param name="Is64Bit">Whether the environment is 64-bit.</param>
/// <param name="Lcid">The environment LCID; <c>0</c> for the invariant locale.</param>
/// <param name="AnsiCodePage">The ANSI code page of the text of a file and of <c>Chr</c> / <c>Asc</c>.</param>
/// <param name="SupportsOptionCompareDatabase">Whether <c>Option Compare Database</c> is supported.</param>
/// <param name="DatabaseCompare">The comparison mode <c>Option Compare Database</c> stands for; <c>Text</c> unless said otherwise.</param>
/// <param name="ErlLineNumbering">What <c>Erl</c> counts as a line; the document line unless said otherwise.</param>
/// <param name="AllowDllImports">Whether a <c>Declare</c>'d library import may be called; <c>true</c> unless said otherwise.</param>
/// <param name="AllowAutomation">Whether a program may create and call the objects of a referenced library, a host application among them; <c>true</c> unless said otherwise.</param>
/// <param name="SourceLanguage">The language the code is written in; RD-VBA unless said otherwise.</param>
/// <param name="LegacyRegistrySettings">Whether the legacy settings members use the Windows Registry rather than the workspace application settings; <c>false</c> unless said otherwise.</param>
/// <param name="RegionalSettings">The regional settings a number or a date is written by as a string, and a string read by as one; en-US (<see cref="Runtime.RegionalSettings.Default"/>) unless said otherwise.</param>
/// <param name="ExecutionEngine">What runs the code of a program; <see cref="Server.Configuration.SdkEnvironmentOptions.DefaultExecutionEngine"/> unless said otherwise.</param>
/// <param name="Configuration">Whether a program is built to be debugged or to be run; a debug build unless said otherwise.</param>
/// <param name="ReleaseDropsDebugStatements">Whether a release build leaves out the <c>Debug</c> statements; <c>false</c> unless said otherwise.</param>
/// <param name="ErlAfterErrClear">What <c>Err.Clear</c> does to the line <c>Erl</c> reports; it resets it unless said otherwise.</param>
/// <param name="OperatingSystem">The operating system the environment runs on; Windows unless said otherwise (<see cref="Running"/> is the one this process runs on).</param>
public sealed record class RuntimeEnvironmentProfile(
    bool Is64Bit,
    int Lcid,
    int AnsiCodePage,
    bool SupportsOptionCompareDatabase,
    Model.Symbols.OptionCompare DatabaseCompare = Model.Symbols.OptionCompare.Text,
    Abstract.Execution.VBErlLineNumbering ErlLineNumbering = Abstract.Execution.VBErlLineNumbering.DocumentLine,
    bool AllowDllImports = true,
    Workspace.SupportedLanguage? SourceLanguage = null,
    bool AllowAutomation = true,
    bool LegacyRegistrySettings = false,
    RegionalSettings? RegionalSettings = null,
    VBExecutionEngine ExecutionEngine = Server.Configuration.SdkEnvironmentOptions.DefaultExecutionEngine,
    VBBuildConfiguration Configuration = VBBuildConfiguration.Debug,
    bool ReleaseDropsDebugStatements = false,
    VBErlAfterErrClear ErlAfterErrClear = VBErlAfterErrClear.Reset,
    VBOperatingSystem OperatingSystem = VBOperatingSystem.Windows) : IRuntimeEnvironmentProfile
{
    /// <summary>
    /// The operating system this process runs on: what the environment host's profile says (<see cref="From"/>).
    /// </summary>
    public static VBOperatingSystem Running { get; } =
        global::System.OperatingSystem.IsWindows() ? VBOperatingSystem.Windows
        : global::System.OperatingSystem.IsMacOS() || global::System.OperatingSystem.IsMacCatalyst() ? VBOperatingSystem.MacOS
        : global::System.OperatingSystem.IsLinux() ? VBOperatingSystem.Linux
        : VBOperatingSystem.Other;

    /// <inheritdoc/>
    public RegionalSettings Regional => RegionalSettings ?? Runtime.RegionalSettings.Default;

    /// <inheritdoc/>
    public Workspace.SupportedLanguage Language => SourceLanguage ?? Workspace.SupportedLanguages.RDVBA;

    /// <summary>
    /// A 64-bit Windows, invariant-culture, Windows-1252, no-<c>Option Compare Database</c> profile for
    /// design-time and tests, whatever the operating system they run on.
    /// </summary>
    public static IRuntimeEnvironmentProfile Default { get; } = new RuntimeEnvironmentProfile(
        Is64Bit: true,
        Lcid: 0,
        AnsiCodePage: 1252,
        SupportsOptionCompareDatabase: false);

    /// <summary>
    /// Builds a profile from bound <c>appsettings.json</c> options, for code written in <paramref name="language"/>, on the operating system this process
    /// runs on (<see cref="Running"/>).
    /// </summary>
    /// <param name="options">The <c>Environment</c> settings of the platform.</param>
    /// <param name="language">The language the code is written in; RD-VBA when <see langword="null"/>.</param>
    public static RuntimeEnvironmentProfile From(Server.Configuration.SdkEnvironmentOptions options, Workspace.SupportedLanguage? language = null)
        => new(options.Is64Bit, options.Lcid, options.AnsiCodePage, options.SupportsOptionCompareDatabase, options.DatabaseCompare,
            options.ErlLineNumbering, options.AllowDllImports, language, options.AllowAutomation,
            options.LegacyRegistrySettings, options.Regional.ToSettings(), options.ExecutionEngine, options.Configuration,
            options.ReleaseDropsDebugStatements, options.ErlAfterErrClear, Running);

    /// <summary>
    /// The profile <paramref name="environment"/> is, as a <see cref="RuntimeEnvironmentProfile"/>: itself when it is one, and otherwise one with each of its
    /// properties, which a <c>with</c> expression can then change one of.
    /// </summary>
    /// <param name="environment">Any profile.</param>
    public static RuntimeEnvironmentProfile Of(IRuntimeEnvironmentProfile environment)
        => environment as RuntimeEnvironmentProfile ?? new(
            environment.Is64Bit, environment.Lcid, environment.AnsiCodePage, environment.SupportsOptionCompareDatabase, environment.DatabaseCompare,
            environment.ErlLineNumbering, environment.AllowDllImports, environment.Language, environment.AllowAutomation, environment.LegacyRegistrySettings,
            environment.Regional, environment.ExecutionEngine, environment.Configuration, environment.ReleaseDropsDebugStatements,
            environment.ErlAfterErrClear, environment.OperatingSystem);

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Not how a value is Let-coerced to or from a <c>String</c>: a number or a date is written and read by <see cref="Regional"/>
    /// (<see cref="IRuntimeEnvironmentProfile.Culture"/>).
    /// </para>
    /// <para>
    /// The culture is found on the first read and held: the profile's <see cref="Lcid"/> never changes, so every read gives the culture of that
    /// <see cref="Lcid"/>, and <see cref="CultureInfo.InvariantCulture"/> for <c>0</c>. A copy made with another <see cref="Lcid"/> (a <c>with</c>
    /// expression) holds its own. An <see cref="Lcid"/> that names no culture throws on each read, as <see cref="CultureInfo.GetCultureInfo(int)"/>
    /// does, and nothing is held for it. What is held takes no part in the record's equality.
    /// </para>
    /// </remarks>
    public CultureInfo Culture => _culture.Of(Lcid);

    // the culture of the profile's Lcid, held from the first read. A copy of the record (with) shares the holder, which is why it is keyed by the Lcid.
    private readonly HeldCulture _culture = new();

    private sealed class HeldCulture
    {
        private Held? _held;

        public CultureInfo Of(int lcid)
        {
            var held = _held;
            if (held is null || held.Lcid != lcid)
            {
                held = new Held(lcid, lcid == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(lcid));
                _held = held;
            }

            return held.Culture;
        }

        // the culture follows from the Lcid, which the record compares: every profile has a holder, and holders are equal whatever they hold.
        public override bool Equals(object? obj) => obj is HeldCulture;

        public override int GetHashCode() => 0;

        private sealed record Held(int Lcid, CultureInfo Culture);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// 🚧 The provider supplies only the code pages .NET does not build in, so a code page it does build in (65001, 28591,
    /// 20127, 1200) is Latin-1 here, the same as one that does not exist. TODO.
    /// </remarks>
    public Encoding AnsiEncoding => CodePagesEncodingProvider.Instance.GetEncoding(AnsiCodePage) ?? Encoding.Latin1;
}
