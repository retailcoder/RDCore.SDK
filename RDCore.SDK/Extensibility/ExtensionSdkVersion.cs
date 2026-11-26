using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RDCore.SDK.Platform;

namespace RDCore.SDK.Extensibility;

// An extension manifest's SdkVersion: written as a PlatformVersion, and read from one, or from the four-part System.Version that manifests
// wrote before it, by its first three numbers. Anything else reads as null, which the manifest's validation flags as an incompatible SDK.
internal sealed class ExtensionSdkVersion : JsonConverter<PlatformVersion?>
{
    public override bool HandleNull => true;

    // an assembly reference's version: its major, minor and patch numbers, which is all that IsCompatibleWith reads.
    public static PlatformVersion Of(Version version) =>
        PlatformVersion.Parse(string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}"));

    public override PlatformVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }

        var text = reader.GetString();
        return PlatformVersion.TryParse(text, out var version) ? version
            : Version.TryParse(text, out var numeric) ? Of(numeric)
            : null;
    }

    public override void Write(Utf8JsonWriter writer, PlatformVersion? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Text);
        }
    }
}
