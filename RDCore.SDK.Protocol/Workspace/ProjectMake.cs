using System.Text.Json;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Workspace;

/// <summary>
/// The <c>Make</c> section of a <c>.rdproj</c> project (<see cref="RDCoreProject.Make"/>, <strong>RD-VBAL §2.2.3.1</strong>): what Visual Basic 6.0's
/// Project Properties have on their Make tab, the program's title, its version and its six version strings, which the program reads from its
/// <c>App</c> object.
/// </summary>
/// <remarks>
/// <para>
/// Its members are written as the properties of <c>App</c> are named: <c>"Make": { "Title": "Inventory", "Major": 1, "Minor": 2, "Revision": 0,
/// "CompanyName": "Contoso" }</c>. Each is optional, and <see langword="null"/> when the section does not have it.
/// </para>
/// <para>
/// It is read as a project file written by hand may have it, and never makes a project file unreadable. A version number is read only when it is a whole
/// number from 0 to 9999, as Visual Basic 6.0's Make tab takes it, and a title or a version string only when it is a string; a member read otherwise is
/// <see langword="null"/>. What is written back is what was read, not the text it was read from: a <see langword="null"/> member is left out. A
/// <c>Make</c> that is not an object reads as no section (<see cref="RDCoreProject.Make"/> is <see langword="null"/>), and is not written back.
/// </para>
/// </remarks>
public sealed record class ProjectMake
{
    /// <summary>
    /// The program's title: <c>App.Title</c> when the program starts.
    /// </summary>
    [JsonConverter(typeof(ProjectTextJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// The major version number: <c>App.Major</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectVersionNumberJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? Major { get; init; }

    /// <summary>
    /// The minor version number: <c>App.Minor</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectVersionNumberJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? Minor { get; init; }

    /// <summary>
    /// The revision number: <c>App.Revision</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectVersionNumberJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? Revision { get; init; }

    /// <summary>
    /// The version string <c>Comments</c>: <c>App.Comments</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectTextJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comments { get; init; }

    /// <summary>
    /// The version string <c>CompanyName</c>: <c>App.CompanyName</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectTextJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompanyName { get; init; }

    /// <summary>
    /// The version string <c>FileDescription</c>: <c>App.FileDescription</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectTextJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileDescription { get; init; }

    /// <summary>
    /// The version string <c>LegalCopyright</c>: <c>App.LegalCopyright</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectTextJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegalCopyright { get; init; }

    /// <summary>
    /// The version string <c>LegalTrademarks</c>: <c>App.LegalTrademarks</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectTextJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegalTrademarks { get; init; }

    /// <summary>
    /// The version string <c>ProductName</c>: <c>App.ProductName</c>.
    /// </summary>
    [JsonConverter(typeof(ProjectTextJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProductName { get; init; }

    /// <summary>
    /// The members of the section this model does not know, as they were read, which are written back (<see cref="ProjectFile.ExtensionData"/>).
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// the section as RDCoreProject reads it: an object is the section; anything else, such as a string, reads as none.
internal sealed class ProjectMakeJsonConverter : JsonConverter<ProjectMake?>
{
    public override bool HandleNull => true;

    public override ProjectMake? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        return JsonSerializer.Deserialize<ProjectMake>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, ProjectMake? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, options);
    }
}

// a version number is a whole number from 0 to 9999, as VB6's Make tab takes it; anything else, such as "1", 1.5 or 10000, reads as none.
internal sealed class ProjectVersionNumberJsonConverter : JsonConverter<short?>
{
    private const short MaxVersionNumber = 9999;

    public override bool HandleNull => true;

    public override short? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        short? number = reader.TokenType is JsonTokenType.Number && reader.TryGetInt16(out var value) && value is >= 0 and <= MaxVersionNumber ? value : null;
        reader.Skip();
        return number;
    }

    public override void Write(Utf8JsonWriter writer, short? value, JsonSerializerOptions options)
    {
        if (value is not { } number)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(number);
    }
}

// a string, kept as it is written, empty or not; anything else is none.
internal sealed class ProjectTextJsonConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType is JsonTokenType.String ? reader.GetString() : null;
        reader.Skip();
        return text;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}
