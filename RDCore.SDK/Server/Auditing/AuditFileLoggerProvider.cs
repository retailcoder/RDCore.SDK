using System.Globalization;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace RDCore.SDK.Server.Auditing;

/// <summary>
/// Writes the entries of the audit log to files, one line of JSON an entry, a file a day (in UTC) for each process:
/// <c>audit-&lt;yyyyMMdd&gt;-&lt;component&gt;-&lt;process&gt;.jsonl</c>.
/// </summary>
/// <remarks>
/// <para>
/// A file of its own for each process, because several processes audit at once, and lines that several processes append to one file are not written whole.
/// The day of an entry is the day of its <see cref="AuditEntry.Time"/>. An entry's members are written in camel case, its enumerations by name, and a member
/// it does not know is left out.
/// </para>
/// <para>
/// It writes only what the audit log gives it (<see cref="AuditLogState"/>), and nothing of any other logger's. The folder is made when the first entry is
/// written. A file that cannot be written throws, which the audit log counts as a dropped entry.
/// </para>
/// </remarks>
/// <param name="fileSystem">The file system the files are written to.</param>
/// <param name="directory">The folder of the files.</param>
/// <param name="component">The component whose entries they are, which names the files.</param>
/// <param name="process">The identifier of its process, which names the files.</param>
public sealed class AuditFileLoggerProvider(IFileSystem fileSystem, string directory, string component, int process) : ILoggerProvider
{
    private readonly Lock _writing = new();
    private string? _path;
    private StreamWriter? _file;
    private bool _disposed;

    /// <summary>
    /// The name of the file of the entries of a day.
    /// </summary>
    /// <param name="day">The day, in UTC.</param>
    public string FileNameOf(DateOnly day)
        => string.Create(CultureInfo.InvariantCulture, $"audit-{day:yyyyMMdd}-{component}-{process}.jsonl");

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new FileLogger(this);

    private void Write(AuditEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, AuditJson.Default.AuditEntry);
        var path = fileSystem.Path.Combine(directory, FileNameOf(DateOnly.FromDateTime(entry.Time.UtcDateTime)));
        lock (_writing)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_file is null || _path != path)
            {
                _file?.Dispose();
                _file = null;
                fileSystem.Directory.CreateDirectory(directory);
                _file = new StreamWriter(fileSystem.File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                _path = path;
            }

            // a line at a time, written through: what a process that ends abruptly wrote is in the file.
            _file.Write(line);
            _file.Write('\n');
            _file.Flush();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_writing)
        {
            _disposed = true;
            _file?.Dispose();
            _file = null;
        }
    }

    private sealed class FileLogger(AuditFileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is AuditLogState { Entry: var entry })
            {
                provider.Write(entry);
            }
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AuditEntry))]
internal sealed partial class AuditJson : JsonSerializerContext;
