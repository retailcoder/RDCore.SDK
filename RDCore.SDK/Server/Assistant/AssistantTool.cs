using RDCore.SDK.Assistant;
using RDCore.SDK.Client;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace RDCore.SDK.Server.Assistant;

/// <summary>
/// A tool whose arguments and result are records: the arguments of a call are read as a <typeparamref name="TArguments"/>, and the tool answers with a
/// <typeparamref name="TResult"/>, as data and as text.
/// </summary>
/// <remarks>
/// <para>
/// The input schema is the JSON Schema of <typeparamref name="TArguments"/> and the output schema that of <typeparamref name="TResult"/>
/// (<see cref="JsonSchemaExporter"/>), so a schema always says what the tool reads and writes. A <see langword="required"/> member is a required argument.
/// Each argument is described in the language of the client's user interface (<see cref="ArgumentDescriptionOf"/>).
/// </para>
/// <para>
/// The arguments are read the way <see cref="RDCorePlatformJson.Options"/> reads a message, in any case, and an argument that is not a member of
/// <typeparamref name="TArguments"/> refuses the call: a misspelled argument is told, never ignored.
/// </para>
/// </remarks>
/// <typeparam name="TArguments">The arguments of a call.</typeparam>
/// <typeparam name="TResult">What a call answers.</typeparam>
public abstract class AssistantTool<TArguments, TResult> : IAssistantTool
    where TArguments : class
    where TResult : class
{
    private readonly ConcurrentDictionary<string, ToolDescriptor> _descriptors = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <summary>
    /// What a call does to the workspace.
    /// </summary>
    protected abstract ToolAnnotations Annotations { get; }

    /// <summary>
    /// What the tool does and when to call it, for an assistant to read.
    /// </summary>
    /// <param name="culture">The language of the client's user interface.</param>
    /// <returns>The description.</returns>
    protected abstract string DescriptionOf(CultureInfo culture);

    /// <summary>
    /// A short name of the tool for a person to read; <see langword="null"/>, by default, for none.
    /// </summary>
    /// <param name="culture">The language of the client's user interface.</param>
    /// <returns>The title.</returns>
    protected virtual string? TitleOf(CultureInfo culture) => null;

    /// <summary>
    /// What an argument is, for an assistant to read; <see langword="null"/>, by default, for an argument its name says enough of.
    /// </summary>
    /// <param name="argument">The argument's name, as the schema writes it (<c>includeDeclaration</c>).</param>
    /// <param name="culture">The language of the client's user interface.</param>
    /// <returns>The description.</returns>
    protected virtual string? ArgumentDescriptionOf(string argument, CultureInfo culture) => null;

    /// <summary>
    /// Does what a call asks.
    /// </summary>
    /// <param name="arguments">The arguments of the call.</param>
    /// <param name="token">Cancels the call.</param>
    /// <returns>What the call answers.</returns>
    /// <exception cref="AssistantToolRefusedException">The tool refuses the call.</exception>
    protected abstract Task<TResult> RunAsync(TArguments arguments, CancellationToken token);

    /// <summary>
    /// What a call answers, as text for an assistant to read: short, one line for each item, positions written <c>path:line:column</c>.
    /// </summary>
    /// <param name="result">What the call answers.</param>
    /// <param name="culture">The language of the client's user interface.</param>
    /// <returns>The text.</returns>
    protected abstract string Render(TResult result, CultureInfo culture);

    /// <inheritdoc/>
    /// <remarks>
    /// A descriptor is made once in each language, and the same instance is answered after that.
    /// </remarks>
    public ToolDescriptor Describe(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return _descriptors.GetOrAdd(culture.Name, _ => new ToolDescriptor
        {
            Name = Name,
            Title = TitleOf(culture),
            Description = DescriptionOf(culture),
            InputSchema = AssistantToolSchemas.Of(typeof(TArguments), argument => ArgumentDescriptionOf(argument, culture)),
            OutputSchema = AssistantToolSchemas.Of(typeof(TResult), _ => null),
            Annotations = Annotations,
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The text is rendered in the language of the current user interface (<see cref="CultureInfo.CurrentUICulture"/>), which a server takes from its
    /// client's <c>initialize</c>.
    /// </remarks>
    public async Task<ToolCallResult> CallAsync(JsonObject arguments, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        TArguments read;
        try
        {
            read = arguments.Deserialize<TArguments>(AssistantToolSchemas.ArgumentOptions)
                ?? throw new JsonException(null);
        }
        catch (JsonException exception)
        {
            throw new AssistantToolRefusedException(string.Format(CultureInfo.CurrentUICulture, AssistantMessages.ArgumentsNotValid, exception.Message));
        }

        var result = await RunAsync(read, token);
        var structured = JsonSerializer.SerializeToNode(result, RDCorePlatformJson.Options) as JsonObject;
        return ToolCallResult.Of(Render(result, CultureInfo.CurrentUICulture), structured);
    }
}

/// <summary>
/// The JSON Schemas of the records a tool reads and writes, and the options its arguments are read with.
/// </summary>
internal static class AssistantToolSchemas
{
    /// <summary>
    /// The options a tool reads its arguments with: those of a message, refusing a member the record does not have.
    /// </summary>
    public static JsonSerializerOptions ArgumentOptions { get; } = CreateArgumentOptions();

    private static JsonSerializerOptions CreateArgumentOptions()
    {
        var options = new JsonSerializerOptions(RDCorePlatformJson.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.MakeReadOnly();
        return options;
    }

    /// <summary>
    /// The schema of a record: an object, each member described where <paramref name="describe"/> says something of it.
    /// </summary>
    /// <param name="type">The record.</param>
    /// <param name="describe">What a member is, by its name in the schema; <see langword="null"/> for nothing.</param>
    /// <returns>The schema.</returns>
    public static JsonObject Of(Type type, Func<string, string?> describe)
    {
        var exporter = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) =>
            {
                if (context.PropertyInfo is { } property && node is JsonObject schema && describe(property.Name) is { Length: > 0 } description)
                {
                    schema.Insert(0, "description", description);
                }

                return node;
            },
        };

        var schema = ArgumentOptions.GetJsonSchemaAsNode(type, exporter) as JsonObject ?? [];
        schema["type"] = "object";
        return schema;
    }
}
