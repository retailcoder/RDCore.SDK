namespace RDCore.SDK.Platform;

public record class PlatformManifest
{
    public string PlatformVersion { get; set; } = string.Empty;
    public DateTime GeneratedUtc { get; set; } = default;

    public string ExtensionsDirectory { get; set; } = string.Empty;

    public string HostService { get; set; } = string.Empty;
    public string LangService { get; set; } = string.Empty;
    public string ParseServer { get; set; } = string.Empty;
}
