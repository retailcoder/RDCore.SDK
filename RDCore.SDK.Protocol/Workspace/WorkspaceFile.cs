using System.Text.Json;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Workspace;

/// <summary>
/// The <c>.rdworkspace</c> of a workspace: the folders of its projects, and the files of its root folder that are in it (<strong>RD-VBAL §2.2.2</strong>).
/// </summary>
/// <remarks>
/// <para>
/// A folder is a workspace when it holds a <c>.rdworkspace</c> that can be read, and it is then the workspace's root. Each of its <see cref="Folders"/> is
/// directly under the root and holds a project's <c>.rdproj</c> (<see cref="ProjectFile"/>), as a Visual Basic 6 project group (<c>.vbg</c>) lists its
/// projects. It is written <c>{ "Folders": [ "NavalLib", "NavalConsole" ], "Files": [ "README.md" ] }</c>.
/// </para>
/// <para>
/// It is read leniently, as a file written by hand needs, and is never refused for what a member holds: a list that is <c>null</c>, or not a list, reads as
/// empty, and an item that is not a string, or is empty or blank, is left out. The members this model does not know are written back as they were read
/// (<see cref="ExtensionData"/>).
/// </para>
/// </remarks>
public sealed record class WorkspaceFile
{
    /// <summary>
    /// The name of the file, at the workspace root.
    /// </summary>
    public const string FileName = ".rdworkspace";

    /// <summary>
    /// The folders of the workspace's projects, by their names, each directly under the workspace root, in the order the file lists them.
    /// </summary>
    [JsonConverter(typeof(WorkspaceNamesJsonConverter))]
    public string[] Folders { get; init; } = [];

    /// <summary>
    /// The files of the workspace root that are in the workspace, by their names, in the order the file lists them, such as a <c>README.md</c> or a license.
    /// </summary>
    [JsonConverter(typeof(WorkspaceNamesJsonConverter))]
    public string[] Files { get; init; } = [];

    /// <summary>
    /// The members of the file this model does not know, as they were read, which are written back.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    /// <summary>
    /// The workspace with <paramref name="folder"/> added last to its folders, unless it already lists it in any letter case.
    /// </summary>
    /// <param name="folder">The name of the folder, directly under the workspace root.</param>
    public WorkspaceFile WithFolder(string folder)
        => Folders.Contains(folder, StringComparer.OrdinalIgnoreCase) ? this : this with { Folders = [.. Folders, folder] };

    /// <summary>
    /// The workspace without <paramref name="folder"/>, listed in any letter case.
    /// </summary>
    /// <param name="folder">The name of the folder.</param>
    public WorkspaceFile WithoutFolder(string folder)
        => this with { Folders = [.. Folders.Where(listed => !string.Equals(listed, folder, StringComparison.OrdinalIgnoreCase))] };

    /// <summary>
    /// Reads a <c>.rdworkspace</c>: UTF-8, with or without a byte order mark, its member names matched case-sensitively.
    /// </summary>
    /// <param name="stream">The file's content.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The workspace the file describes.</returns>
    /// <exception cref="JsonException">The file is not JSON, or not a JSON object; the exception says where.</exception>
    public static async Task<WorkspaceFile> ReadAsync(Stream stream, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return await JsonSerializer.DeserializeAsync<WorkspaceFile>(stream, cancellationToken: token)
            ?? throw new JsonException($"A {FileName} is a JSON object, not null.");
    }
}

// a list of names as files have it: an array's strings that name something; anything else, or a null, empty or blank item, names nothing.
internal sealed class WorkspaceNamesJsonConverter : JsonConverter<string[]>
{
    public override bool HandleNull => true;

    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            reader.Skip();
            return [];
        }

        var names = new List<string>();
        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            var name = reader.TokenType is JsonTokenType.String ? reader.GetString() : null;
            reader.Skip();
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return [.. names];
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var name in value ?? [])
        {
            writer.WriteStringValue(name);
        }

        writer.WriteEndArray();
    }
}
