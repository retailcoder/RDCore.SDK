using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace RDCore.SDK.Forms;

/// <summary>
/// Changes the designer data of a Microsoft Forms form: the storage a VBA UserForm persists itself to, which the VBE exports
/// to the form's <c>.frx</c> file ([MS-OFORMS] 2.1).
/// </summary>
/// <remarks>
/// The writer runs on any platform: no OLE32 and no Microsoft Forms. It changes only what the change needs, and every byte
/// it does not need to change stays as it is: it reads the whole form first, as <see cref="MSFormsReader.Read"/> does, and
/// writes nothing for damaged data.
/// </remarks>
public static class MSFormsWriter
{
    private const uint Compressed = 0x80000000;

    /// <summary>
    /// The designer data in <paramref name="frx"/>, with the control named <paramref name="name"/> named
    /// <paramref name="newName"/>: the name in the control's site ([MS-OFORMS] 2.2.10.12) changes, and nothing else of the
    /// form does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A name of another length changes the site's size, and the count of bytes of the sites of the form stream that holds
    /// it (2.2.10.6); the form stream keeps the sectors of the compound file ([MS-CFB]) it still needs and takes more as it
    /// must, and a <c>.frx</c> blob header's length of the compound file follows. Every other stream is unchanged.
    /// </para>
    /// <para>
    /// A name the site stores compressed, one byte a character (2.4.14.2), is written compressed when every character of
    /// the new name has a one-byte form, and as UTF-16 otherwise.
    /// </para>
    /// </remarks>
    /// <param name="frx">The designer data, as <see cref="MSFormsReader.Read"/> takes it: with or without the <c>.frx</c>
    /// blob header.</param>
    /// <param name="name">The control's name. Names compare case-insensitively, as VBA's do.</param>
    /// <param name="newName">The control's new name.</param>
    /// <returns>The designer data with the new name, laid out as <paramref name="frx"/> is: with its blob header when it
    /// has one.</returns>
    /// <exception cref="MSFormsFormatException">The data is not a form's designer data, or a damaged one
    /// (<see cref="MSFormsFormatException.Problem"/>).</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="newName"/> is empty; no control, or
    /// more than one, is named <paramref name="name"/>; or another control is named <paramref name="newName"/>.</exception>
    /// <exception cref="NotSupportedException">The new name does not fit: the site would hold more than 65,535 bytes, the
    /// compound file would need more FAT sectors than its header lists, or the <c>.frx</c> holds data after the compound
    /// file, which a compound file of another length would move.</exception>
    public static byte[] RenameControl(ReadOnlySpan<byte> frx, string name, string newName)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(newName);
        MSFormsReader.Read(frx);

        var (start, length) = MSFormsReader.CompoundFileRange(frx);
        var file = CompoundFile.Open(frx.Slice(start, length).ToArray());
        var sites = new List<(CompoundFileEntry Stream, FormStream Form, RawSite Site)>();
        SitesOf(file, file.Root, MSFormsControlKind.Form, sites);

        var named = sites.Where(entry => IsNamed(entry.Site, name)).ToList();
        if (named.Count != 1)
        {
            throw new ArgumentException(Format(named.Count == 0 ? MSFormsMessages.ControlNotFound : MSFormsMessages.ControlNotUnique, name), nameof(name));
        }

        var (stream, form, site) = named[0];
        if (sites.Any(entry => !ReferenceEquals(entry.Site, site) && IsNamed(entry.Site, newName)))
        {
            throw new ArgumentException(Format(MSFormsMessages.NameTaken, newName), nameof(newName));
        }

        var editor = new CompoundFileEditor(file);
        editor.Replace(stream, Renamed(file.Read(stream), form, site, newName));
        var compoundFile = editor.ToArray();
        if (start == 0)
        {
            return compoundFile;
        }

        var after = frx[(start + length)..];
        if (compoundFile.Length != length && !after.IsEmpty)
        {
            throw new NotSupportedException(MSFormsMessages.DataAfterForm);
        }

        var result = new byte[start + compoundFile.Length + after.Length];
        frx[..start].CopyTo(result);
        compoundFile.CopyTo(result, start);
        after.CopyTo(result.AsSpan(start + compoundFile.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)compoundFile.Length);
        return result;
    }

    private static bool IsNamed(RawSite site, string name) => string.Equals(site.Name, name, StringComparison.OrdinalIgnoreCase);

    private static string Format(string message, params object[] values) => string.Format(CultureInfo.CurrentCulture, message, values);

    // every site of the form, with the form stream that holds it: the storages the reader reads, walked as it walks them
    private static void SitesOf(CompoundFile file, CompoundFileEntry storage, MSFormsControlKind kind, List<(CompoundFileEntry Stream, FormStream Form, RawSite Site)> into)
    {
        var stream = file.Find(storage, "f") is { Type: CompoundFileEntryType.Stream } f
            ? f
            : throw new MSFormsFormatException(MSFormsFormatProblem.MissingFormStream);
        var form = FormStream.Read(file.Read(stream), kind);
        foreach (var site in form.Sites)
        {
            into.Add((stream, form, site));
            var (siteKind, _, _) = MSFormsReader.KindOf(site, form.ClassTable, kind);
            if (!site.Site.Streamed && MSFormsReader.IsParent(siteKind) && file.Find(storage, $"i{site.Id:00}") is { Type: CompoundFileEntryType.Storage } own)
            {
                SitesOf(file, own, siteKind, into);
            }
        }
    }

    // the form stream with the site's Name (its first ExtraDataBlock value, padded to 4 bytes from the site's first byte)
    // replaced; the site's size counts what follows its first 4 bytes (2.2.10.12.1), CountOfBytes every site
    private static byte[] Renamed(byte[] stream, FormStream form, RawSite site, string newName)
    {
        var (countAt, textAt) = site.Block.StringsAt["Name"];
        var siteStart = site.Block.Start;
        var oldCount = BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan(countAt));
        var oldLength = (int)(oldCount & ~Compressed);
        var compressed = (oldCount & Compressed) != 0 && newName.All(character => character <= 0xFF);
        var text = compressed ? Encoding.Latin1.GetBytes(newName) : Encoding.Unicode.GetBytes(newName);

        var oldEnd = siteStart + Padded(textAt - siteStart + oldLength);
        var newEnd = siteStart + Padded(textAt - siteStart + text.Length);
        var delta = newEnd - oldEnd;
        var siteSize = BinaryPrimitives.ReadUInt16LittleEndian(stream.AsSpan(siteStart + 2)) + delta;
        if (siteSize > ushort.MaxValue)
        {
            throw new NotSupportedException(Format(MSFormsMessages.SiteTooLarge, site.Name, newName));
        }

        var result = new byte[stream.Length + delta];
        stream.AsSpan(0, textAt).CopyTo(result);
        text.CopyTo(result, textAt);
        if (text.Length == oldLength)
        {
            // a name of the same length keeps its padding as it is
            stream.AsSpan(textAt + oldLength, oldEnd - textAt - oldLength).CopyTo(result.AsSpan(textAt + text.Length));
        }

        stream.AsSpan(oldEnd).CopyTo(result.AsSpan(newEnd));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(countAt), (uint)text.Length | (compressed ? Compressed : 0));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(siteStart + 2), (ushort)siteSize);
        var countOfBytes = BinaryPrimitives.ReadUInt32LittleEndian(stream.AsSpan(form.CountOfBytesAt));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(form.CountOfBytesAt), (uint)(countOfBytes + delta));
        return result;
    }

    private static int Padded(int size) => (size + 3) & ~3;
}
