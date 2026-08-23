using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;

namespace RDCore.SDK.Platform;

public record class PlatformManifest
{
    public string PlatformVersion { get; init; }
    public DateTime GeneratedUtc { get; init; }

    public string ExtensionsDirectory { get; init; }

    public string HostService { get; init; }
    public string LangService { get; init; }
    public string ParseServer { get; init; }
}

public interface IPlatformCompositionService
{
    string RootPath { get; }
    PlatformManifest GetManifest();
}
