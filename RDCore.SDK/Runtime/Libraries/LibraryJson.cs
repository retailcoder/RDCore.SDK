using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RDCore.SDK.Runtime.Libraries;

/// <summary>
/// Reads and writes the file a library is described in (<see cref="LibraryDescription"/>).
/// </summary>
/// <remarks>
/// The format is JSON with camel-case names and enumerations as strings: it is read by people in a review as much as by the platform. What is not stated is
/// left out when the file is written, so a description does not grow a line for every default; comments and trailing commas are accepted when it is read,
/// for the file that is edited by hand.
/// </remarks>
public static class LibraryJson
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new LibraryIdentityConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipEmptyLists } },
    };

    // a list that has nothing in it is not stated, as nothing else that is not stated is.
    private static void SkipEmptyLists(JsonTypeInfo type)
    {
        foreach (var property in type.Properties.Where(property => property.PropertyType.IsGenericType
            && property.PropertyType.GetGenericTypeDefinition() == typeof(ImmutableArray<>)))
        {
            property.ShouldSerialize = (_, value) => value is not null
                && value.GetType().GetProperty(nameof(ImmutableArray<int>.IsDefault))!.GetValue(value) is false
                && ((System.Collections.ICollection)value).Count > 0;
        }
    }

    /// <summary>
    /// The version of the format this writes. It reads it, and every version before it.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Version 7 states the members a hidden default member stands for (<see cref="MemberDescription.WrittenAs"/>).</item>
    /// <item>Version 6 states the classes an object of a class is as well (<see cref="ClassDescription.Implements"/>).</item>
    /// <item>Version 5 states what RD-VBA adds to a library that its type library does not declare (<see cref="MemberDescription.IsAddition"/>).</item>
    /// <item>Version 4 states what <c>TypeName</c> says of an object of a class, when it is not the class's name (<see cref="ClassDescription.TypeName"/>).</item>
    /// <item>Version 3 states the modules a library declares (<see cref="LibraryDescription.Modules"/>).</item>
    /// <item>Version 2 states a dependency as the library it was built against, <c>{ "name": "DAO", "guid": "{00025E04-...}", "version": "3.5" }</c>, as
    /// well as by its name alone (<c>"stdole"</c>).</item>
    /// <item>Version 1 states a dependency by its name alone.</item>
    /// </list>
    /// </remarks>
    public const int CurrentFormatVersion = 7;

    /// <summary>
    /// The oldest version of the format this reads.
    /// </summary>
    public const int OldestFormatVersion = 1;

    /// <summary>
    /// Reads a description.
    /// </summary>
    /// <param name="json">The text of the file.</param>
    /// <exception cref="InvalidDataException">
    /// The text is not a description this reads: it is not JSON of the format, has no name, is of a format version this does not know, or states what its
    /// format version does not have (a dependency's identity before version 2, modules before version 3, a class's type name before version 4, an addition
    /// before version 5, the classes an object of a class is as well before version 6, the members a hidden default member stands for before version 7).
    /// </exception>
    public static LibraryDescription Read(string json)
    {
        LibraryDescription? description;
        try
        {
            description = JsonSerializer.Deserialize<LibraryDescription>(json, _options);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The library description is not valid: {exception.Message}", exception);
        }

        if (description is null || string.IsNullOrWhiteSpace(description.Name))
        {
            throw new InvalidDataException("The library description states no name.");
        }

        if (description.FormatVersion is < OldestFormatVersion or > CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"The library description of '{description.Name}' is of format version {description.FormatVersion}, and this reads versions {OldestFormatVersion} to {CurrentFormatVersion}.");
        }

        if (description.FormatVersion < 2 && description.DependsOn.FirstOrDefault(dependency => dependency.Guid is not null) is { } identified)
        {
            throw new InvalidDataException(
                $"The library description of '{description.Name}' is of format version {description.FormatVersion}, which states a dependency by its name alone, and states '{identified}'.");
        }

        if (description.FormatVersion < 3 && !description.Modules.IsDefaultOrEmpty)
        {
            throw new InvalidDataException(
                $"The library description of '{description.Name}' is of format version {description.FormatVersion}, which states no modules, and states '{description.Modules[0].Name}'.");
        }

        if (description.FormatVersion < 4 && description.Classes.FirstOrDefault(declared => declared.TypeName is not null) is { } named)
        {
            throw new InvalidDataException(
                $"The library description of '{description.Name}' is of format version {description.FormatVersion}, which states no type name of a class, and states one for '{named.Name}'.");
        }

        if (description.FormatVersion < 5 && AdditionsOf(description).FirstOrDefault() is { } added)
        {
            throw new InvalidDataException(
                $"The library description of '{description.Name}' is of format version {description.FormatVersion}, which states no additions, and states '{added}' as one.");
        }

        if (description.FormatVersion < 6 && description.Classes.FirstOrDefault(declared => !declared.Implements.IsDefaultOrEmpty) is { } implementing)
        {
            throw new InvalidDataException(
                $"The library description of '{description.Name}' is of format version {description.FormatVersion}, which states no class an object "
                + $"of a class is as well, and states '{implementing.Implements[0]}' for '{implementing.Name}'.");
        }

        if (description.FormatVersion < 7 && WrittenAsOf(description).FirstOrDefault() is { } writtenAs)
        {
            throw new InvalidDataException(
                $"The library description of '{description.Name}' is of format version {description.FormatVersion}, which states no members a hidden "
                + $"default member stands for, and states them for '{writtenAs}'.");
        }

        // a name is one type or module of the library, whatever its case: VBA names are not case sensitive, and a library that declares one twice is not one a
        // project can be checked against.
        var twice = description.Classes.Select(declared => declared.Name).Concat(description.Enums.Select(declared => declared.Name))
            .Concat(description.Modules.Select(declared => declared.Name))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        return twice is null
            ? description
            : throw new InvalidDataException($"The library description of '{description.Name}' declares '{twice.Key}' more than once.");
    }

    /// <summary>
    /// Writes a description as the text of its file.
    /// </summary>
    /// <param name="description">The description.</param>
    public static string Write(LibraryDescription description) => JsonSerializer.Serialize(description, _options);

    // the names of the members of a description that state what a hidden default member stands for, wherever a member can be.
    private static IEnumerable<string> WrittenAsOf(LibraryDescription description)
        => description.Classes.SelectMany(declared => declared.Members.Where(member => member.WrittenAs is not null)
                .Select(member => $"{declared.Name}.{member.Name}"))
            .Concat(description.Modules.SelectMany(declared => declared.Members.Where(member => member.WrittenAs is not null)
                .Select(member => $"{declared.Name}.{member.Name}")));

    // the names of what a description states RD-VBA adds, wherever an addition can be.
    private static IEnumerable<string> AdditionsOf(LibraryDescription description)
        => description.Classes.Where(declared => declared.IsAddition).Select(declared => declared.Name)
            .Concat(description.Classes.SelectMany(declared => declared.Members.Where(member => member.IsAddition).Select(member => $"{declared.Name}.{member.Name}")))
            .Concat(description.Classes.SelectMany(declared => declared.Events.Where(raised => raised.IsAddition).Select(raised => $"{declared.Name}.{raised.Name}")))
            .Concat(description.Enums.Where(declared => declared.IsAddition).Select(declared => declared.Name))
            .Concat(description.Enums.SelectMany(declared => declared.Members.Where(member => member.IsAddition).Select(member => $"{declared.Name}.{member.Name}")))
            .Concat(description.Modules.SelectMany(declared => declared.Members.Where(member => member.IsAddition).Select(member => $"{declared.Name}.{member.Name}")))
            .Concat(description.Modules.SelectMany(declared => declared.Constants.Where(constant => constant.IsAddition).Select(constant => $"{declared.Name}.{constant.Name}")));

    // a dependency is its name, as a string, when it gives nothing else, and an object of its name, identifier and version when it does.
    private sealed class LibraryIdentityConverter : JsonConverter<LibraryIdentity>
    {
        public override LibraryIdentity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new LibraryIdentity(reader.GetString() ?? string.Empty);
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("A dependency is a name, or an object with a name.");
            }

            string? name = null, guid = null, version = null;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var property = reader.GetString();
                reader.Read();
                var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException($"The dependency's '{property}' is not a string.");
                switch (property?.ToLowerInvariant())
                {
                    case "name": name = value; break;
                    case "guid": guid = value; break;
                    case "version": version = value; break;
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new JsonException("A dependency states no name.");
            }

            if (guid is not null && !Guid.TryParse(guid, out _))
            {
                throw new JsonException($"The dependency '{name}' states '{guid}', which is not an identifier.");
            }

            var (major, minor) = LibraryIdentity.ParseVersion(version);
            if (version is not null && major is null)
            {
                throw new JsonException($"The dependency '{name}' states the version '{version}', which is not major.minor.");
            }

            return new LibraryIdentity(name, guid is null ? null : Guid.Parse(guid), major, minor);
        }

        public override void Write(Utf8JsonWriter writer, LibraryIdentity value, JsonSerializerOptions options)
        {
            if (value.Guid is not { } guid)
            {
                writer.WriteStringValue(value.Name);
                return;
            }

            writer.WriteStartObject();
            writer.WriteString("name", value.Name);
            writer.WriteString("guid", guid.ToString("B").ToUpperInvariant());
            if (value.Version is { } version)
            {
                writer.WriteString("version", version);
            }

            writer.WriteEndObject();
        }
    }
}
