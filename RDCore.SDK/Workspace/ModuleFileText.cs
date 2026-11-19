using RDCore.SDK.Platform.Protocol;
using RDCore.SDK.Semantics.Refactoring;
using System.Globalization;
using System.IO.Abstractions;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;

namespace RDCore.SDK.Workspace;

/// <summary>
/// The text of a module file as it is on disk: decoded in the encoding the file is written in, and written back in it, byte for byte where the text is not
/// edited.
/// </summary>
/// <remarks>
/// <para>
/// A file's encoding is read from its byte order mark: UTF-8, UTF-16 (little or big endian), or UTF-32 (little endian). A file without one is UTF-8 when its
/// bytes are valid UTF-8, and is otherwise in the ANSI code page the caller names (<see cref="DefaultAnsiCodePage"/> when it has no setting to take it from).
/// The code page of the operating system is never used: Linux and macOS have none, and a file would read differently on each machine.
/// </para>
/// <para>
/// The text keeps everything the file has: its byte order mark or its absence, each line's ending (a carriage return and a line feed, a line feed, or a carriage
/// return, mixed as they are), and a newline at the end or its absence. An edit is written with the line endings of the line it is made on
/// (<see cref="Edit"/>), and a text the file's encoding cannot write is refused, never written with a replacement character.
/// </para>
/// </remarks>
public sealed partial class ModuleFileText
{
    /// <summary>
    /// The ANSI code page a file without a byte order mark that is not UTF-8 is read in when no setting names one: Windows-1252, the code page Office for
    /// Windows writes Western European module files in.
    /// </summary>
    public const int DefaultAnsiCodePage = 1252;

    private static readonly ResourceManager Messages = new("RDCore.SDK.Workspace.WorkspaceFileMessages", typeof(ModuleFileText).Assembly);

    // the file's bytes, when the text is as they decode to: written back as they are.
    private readonly byte[]? _bytes;
    private readonly byte[] _preamble;

    private ModuleFileText(string text, Encoding encoding, byte[] preamble, bool isLossless, byte[]? bytes)
    {
        Text = text;
        Encoding = encoding;
        _preamble = preamble;
        IsLossless = isLossless;
        _bytes = bytes;
    }

    /// <summary>
    /// The text, with the line endings the file has.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The encoding the file is written in. It refuses a character it cannot write, and a byte it has no character for.
    /// </summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// <see langword="true"/> when the file starts with a byte order mark, which <see cref="Encode"/> writes again.
    /// </summary>
    public bool HasByteOrderMark => _preamble.Length > 0;

    /// <summary>
    /// <see langword="true"/> when every byte of the file decodes in its encoding. A file that is not (bytes not valid in the encoding its byte order mark
    /// names, or a byte the ANSI code page has no character for) is read with a replacement character for each such byte, and is not edited
    /// (<see cref="Edit"/>).
    /// </summary>
    public bool IsLossless { get; }

    /// <summary>
    /// Decodes the bytes of a module file.
    /// </summary>
    /// <param name="bytes">The bytes of the file.</param>
    /// <param name="ansiCodePage">
    /// The code page a file without a byte order mark that is not valid UTF-8 is in: the environment's (<c>Environment.AnsiCodePage</c>), or
    /// <see cref="DefaultAnsiCodePage"/>.
    /// </param>
    /// <returns>The text, and the encoding it is written back in.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ansiCodePage"/> is not the code page of an encoding .NET has.</exception>
    public static ModuleFileText Decode(byte[] bytes, int ansiCodePage)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var ansi = AnsiEncoding(ansiCodePage);
        var (encoding, preambleLength) = ByteOrderMarkOf(bytes) ?? (System.Text.Unicode.Utf8.IsValid(bytes) ? (new UTF8Encoding(false, throwOnInvalidBytes: true), 0) : (ansi, 0));
        var preamble = bytes[..preambleLength];
        try
        {
            return new ModuleFileText(encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength), encoding, preamble, isLossless: true, bytes);
        }
        catch (DecoderFallbackException)
        {
            var replacing = (Encoding)encoding.Clone();
            replacing.DecoderFallback = DecoderFallback.ReplacementFallback;
            return new ModuleFileText(replacing.GetString(bytes, preambleLength, bytes.Length - preambleLength), encoding, preamble, isLossless: false, bytes);
        }
    }

    /// <summary>
    /// Reads a module file.
    /// </summary>
    /// <param name="fileSystem">The file system the file is on.</param>
    /// <param name="path">The file's path.</param>
    /// <param name="ansiCodePage">The code page a file without a byte order mark that is not valid UTF-8 is in (<see cref="Decode"/>).</param>
    /// <param name="token">A token that cancels the read.</param>
    /// <returns>The file's text, and the encoding it is written back in.</returns>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ansiCodePage"/> is not the code page of an encoding .NET has.</exception>
    public static async Task<ModuleFileText> ReadAsync(IFileSystem fileSystem, string path, int ansiCodePage, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return Decode(await fileSystem.File.ReadAllBytesAsync(path, token), ansiCodePage);
    }

    /// <summary>
    /// The text with edits made, in the same encoding.
    /// </summary>
    /// <remarks>
    /// The line breaks an edit writes are written with the ending of the line it starts on; on the last line, which has none, with the ending of the line before
    /// it; and in a text of one line, with a carriage return and a line feed. The rest of the text is not touched.
    /// </remarks>
    /// <param name="edits">The edits, in any order, each of the text before any of them; they do not overlap (<strong>LSP 3.17</strong> §TextEdit).</param>
    /// <returns>The edited text.</returns>
    /// <exception cref="ModuleFileTextException">
    /// The file is not <see cref="IsLossless">lossless</see>, or the edited text has a character its encoding cannot write.
    /// </exception>
    /// <exception cref="ArgumentException">Two of the edits overlap.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An edit is of a line the text does not have.</exception>
    public ModuleFileText Edit(IReadOnlyList<RefactorTextEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        if (!IsLossless)
        {
            throw new ModuleFileTextException(ModuleFileTextFailure.NotLossless, Message(nameof(ModuleFileTextFailure.NotLossless), Encoding.WebName));
        }

        if (edits.Count == 0)
        {
            return this;
        }

        var lines = TextLines.Of(Text);
        var written = edits.Select(edit => edit with { NewText = LineBreaks().Replace(edit.NewText, NewLineAt(lines, edit.Range.Start.Line)) }).ToList();
        var edited = new ModuleFileText(DeclarationEdits.ApplyTo(Text, written), Encoding, _preamble, isLossless: true, bytes: null);
        _ = edited.Encode();
        return edited;
    }

    /// <summary>
    /// The bytes of the file: the byte order mark it was read with, if any, then the text in its encoding. Unedited, they are the bytes it was read from.
    /// </summary>
    /// <returns>The bytes to write.</returns>
    /// <exception cref="ModuleFileTextException">The text has a character its encoding cannot write.</exception>
    public byte[] Encode()
    {
        if (_bytes is not null)
        {
            return _bytes;
        }

        try
        {
            return [.. _preamble, .. Encoding.GetBytes(Text)];
        }
        catch (EncoderFallbackException exception)
        {
            var line = TextLines.Of(Text).PositionOf(exception.Index).Line + 1;
            var character = exception.CharUnknownHigh != default ? $"{exception.CharUnknownHigh}{exception.CharUnknownLow}" : exception.CharUnknown.ToString();
            throw new ModuleFileTextException(ModuleFileTextFailure.NotRepresentable,
                Message(nameof(ModuleFileTextFailure.NotRepresentable), Encoding.WebName, line, character));
        }
    }

    /// <summary>
    /// Writes the file: its bytes (<see cref="Encode"/>) go to a file beside it, which then replaces it, so that the file is never seen half written.
    /// </summary>
    /// <param name="fileSystem">The file system the file is on.</param>
    /// <param name="path">The file's path.</param>
    /// <param name="token">A token that cancels the write.</param>
    /// <exception cref="ModuleFileTextException">The text has a character its encoding cannot write; nothing is written.</exception>
    /// <exception cref="IOException">The file could not be written; it is as it was.</exception>
    public async Task WriteAsync(IFileSystem fileSystem, string path, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(path);
        var bytes = Encode();
        var directory = fileSystem.Path.GetDirectoryName(fileSystem.Path.GetFullPath(path)) ?? string.Empty;
        var temporary = fileSystem.Path.Combine(directory, $".{fileSystem.Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await fileSystem.File.WriteAllBytesAsync(temporary, bytes, token);
            fileSystem.File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (fileSystem.File.Exists(temporary))
            {
                fileSystem.File.Delete(temporary);
            }
        }
    }

    // the line breaks an edit writes on a line: that line's ending, the one before it on the last line, a carriage return and a line feed in a text of one line.
    private static string NewLineAt(TextLines lines, int line)
        => line < lines.Count && lines.NewLineOf(line) is { Length: > 0 } ending ? ending
            : line > 0 && line - 1 < lines.Count ? lines.NewLineOf(line - 1)
            : "\r\n";

    private static string Message(string name, params object[] arguments)
        => string.Format(CultureInfo.CurrentUICulture, Messages.GetString(name, CultureInfo.CurrentUICulture)!, arguments);

    private static Encoding AnsiEncoding(int codePage)
    {
        try
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                ?? Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new ArgumentOutOfRangeException(nameof(codePage), codePage, exception.Message);
        }
    }

    // the encoding a byte order mark says, and its length; UTF-32 LE before UTF-16 LE, whose mark it starts with.
    private static (Encoding Encoding, int Length)? ByteOrderMarkOf(byte[] bytes) => bytes switch
    {
        [0xEF, 0xBB, 0xBF, ..] => (new UTF8Encoding(false, throwOnInvalidBytes: true), 3),
        [0xFF, 0xFE, 0x00, 0x00, ..] => (new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true), 4),
        [0xFF, 0xFE, ..] => (new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true), 2),
        [0xFE, 0xFF, ..] => (new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true), 2),
        _ => null,
    };

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineBreaks();
}
