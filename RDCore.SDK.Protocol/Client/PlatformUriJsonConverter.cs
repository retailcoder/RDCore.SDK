using System.Text.Json;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Client;

// a Uri is written as it is spelled, a path or a URI, and read back as an absolute one first: off Windows, the RelativeOrAbsolute that System.Text.Json reads
// with makes a relative URI of a rooted path (/home/ws/Module1.bas), which Absolute reads as the file it names. Only what no absolute URI can be made of is
// relative. The location converter of SyntaxNodeJson, which payloads are written with, reads the same way.
internal sealed class PlatformUriJsonConverter : JsonConverter<Uri>
{
    public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("A Uri is a string.");
        return Uri.TryCreate(text, UriKind.Absolute, out var absolute) ? absolute : new Uri(text, UriKind.Relative);
    }

    public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options) => writer.WriteStringValue(value.OriginalString);
}
