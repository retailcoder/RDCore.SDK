using System.Text.Json;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Workspace;

/// <summary>
/// What a project builds into: a program, a console program, or a library (<strong>RD-VBAL §2.2.3.1</strong>).
/// </summary>
public enum ProjectOutputType
{
    /// <summary>
    /// A program, which <c>rdc run</c> and <c>rdc build</c> start at its entry point (<see cref="ProjectBuild.EntryPoint"/>), and whose forms and dialogs
    /// are windows. A project file with no <c>build</c> section, or a section with no <c>outputType</c>, is one.
    /// </summary>
    Application,

    /// <summary>
    /// A library: code that other projects and hosts call into, which has no entry point and is not run.
    /// </summary>
    Library,

    /// <summary>
    /// A console program, started at its entry point as an <see cref="Application"/> is: its run gets the console's user interface, where
    /// <c>InputBox</c> prompts and <c>MsgBox</c> shows its message in the console, as the shell does.
    /// </summary>
    /// <remarks>
    /// It is written <c>ConsoleApplication</c>, and is in the format 1.2 (<see cref="ProjectFile.SchemaVersion"/>). A platform of the format 1.1 knows
    /// <c>outputType</c> but not this value: it reads the project as an <see cref="Application"/>, and writes it as one.
    /// </remarks>
    ConsoleApplication,
}

/// <summary>
/// The <c>build</c> section of a <c>.rdproj</c> project (<see cref="RDCoreProject.Build"/>, <strong>RD-VBAL §2.2.3.1</strong>): what the project builds into,
/// and where its program starts.
/// </summary>
/// <remarks>
/// <para>
/// Its members are written <c>outputType</c> and <c>entryPoint</c>, as files have them: <c>"build": { "outputType": "Library" }</c>,
/// <c>"build": { "outputType": "ConsoleApplication" }</c>, <c>"build": { "entryPoint": "Module1.Main" }</c>.
/// </para>
/// <para>
/// It is read as a project file written by hand may have it, and never makes a project file unreadable. An <c>outputType</c> other than <c>Library</c> or
/// <c>ConsoleApplication</c>, whatever its case, is <see cref="ProjectOutputType.Application"/>, and an <c>entryPoint</c> that is not a string, or is
/// empty or blank, is none. What is written back is what was read, not the text it was read from: the <c>outputType</c> of an
/// <see cref="ProjectOutputType.Application"/>, and an entry point that is none, are left out. A <c>build</c> that is not an object reads as no section,
/// and is not written back.
/// </para>
/// </remarks>
public sealed record class ProjectBuild
{
    /// <summary>
    /// What the project builds into. <see cref="ProjectOutputType.Application"/> unless the section says <c>Library</c> or <c>ConsoleApplication</c>.
    /// </summary>
    [JsonPropertyName("outputType")]
    [JsonConverter(typeof(ProjectOutputTypeJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ProjectOutputType OutputType { get; init; }

    /// <summary>
    /// The procedure the program starts with, as <c>Module.Procedure</c> or a lone <c>Procedure</c>; <see langword="null"/> for the <c>Sub Main</c> of the
    /// one standard module that declares one. <c>ProgramEntryPoint.Of</c> finds the procedure it names.
    /// </summary>
    [JsonPropertyName("entryPoint")]
    [JsonConverter(typeof(ProjectEntryPointJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EntryPoint { get; init; }

    /// <summary>
    /// The members of the section this model does not know, as they were read, which are written back (<see cref="ProjectFile.ExtensionData"/>).
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    /// <summary>
    /// Whether the project builds into a library, which has no entry point (<see cref="ProjectOutputType.Library"/>).
    /// </summary>
    [JsonIgnore]
    public bool IsLibrary => OutputType is ProjectOutputType.Library;

    /// <summary>
    /// Whether the project's program runs in the console, whose user interface its run gets (<see cref="ProjectOutputType.ConsoleApplication"/>).
    /// </summary>
    [JsonIgnore]
    public bool IsConsoleApplication => OutputType is ProjectOutputType.ConsoleApplication;
}

// the section as RDCoreProject reads it: an object is the section; anything else, such as a string, is no section.
internal sealed class ProjectBuildJsonConverter : JsonConverter<ProjectBuild?>
{
    public override bool HandleNull => true;

    public override ProjectBuild? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        return JsonSerializer.Deserialize<ProjectBuild>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, ProjectBuild? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        JsonSerializer.Serialize(writer, value, options);
    }
}

// Library and ConsoleApplication, whatever their case, are what they name; anything else is a program, so that a section written by hand always reads.
internal sealed class ProjectOutputTypeJsonConverter : JsonConverter<ProjectOutputType>
{
    public override ProjectOutputType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var named = reader.TokenType is JsonTokenType.String ? reader.GetString() : null;
        reader.Skip();
        return Names(ProjectOutputType.Library) ? ProjectOutputType.Library
            : Names(ProjectOutputType.ConsoleApplication) ? ProjectOutputType.ConsoleApplication
            : ProjectOutputType.Application;

        bool Names(ProjectOutputType type) => string.Equals(named, type.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public override void Write(Utf8JsonWriter writer, ProjectOutputType value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}

// a string that names something; anything else, or an empty or blank string, names nothing.
internal sealed class ProjectEntryPointJsonConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var named = reader.TokenType is JsonTokenType.String ? reader.GetString() : null;
        reader.Skip();
        return string.IsNullOrWhiteSpace(named) ? null : named;
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
